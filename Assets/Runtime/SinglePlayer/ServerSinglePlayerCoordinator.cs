using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.SinglePlayer
{
    /// <summary>
    /// Server-authoritative single-player vs AI. Instead of simulating the battle on the client
    /// (the old DuelRuntime path), this opens a REAL private server match: the human takes seat 0
    /// through the normal MatchSignalRCoordinator (identical to multiplayer), and the AI account
    /// joins seat 1. The AI is driven from the server's MatchSnapshot over HTTP, sending PlayCard /
    /// EndTurn for its seat. The server MatchEngine is the single source of truth for both seats.
    /// </summary>
    public sealed class ServerSinglePlayerCoordinator : MonoBehaviour
    {
        [Header("AI account (provides the opponent deck + drives seat 1)")]
        [SerializeField] private string aiDeckAccountEmail = "playertwo@flippy.com";
        [SerializeField] private string aiDeckAccountPassword = "123456";

        [Header("AI")]
        public AiDifficulty aiDifficulty = AiDifficulty.Medium;
        public float aiFirstActionDelay = 0.6f;
        public float aiActionDelay = 0.4f;
        public int maxAiActionsPerTurn = 12;

        public static ServerSinglePlayerCoordinator Instance { get; private set; }

        public bool IsActive => _started;

        private readonly SimpleCardAiAgent _ai = new();
        private MatchplayApiClient _matchplay;
        private MatchSignalRCoordinator _humanCoordinator;
        private string _baseUrl;
        private string _matchId;
        private string _humanId;
        private string _aiId;
        private string _humanToken;
        private string _aiToken;
        private int _aiSeatIndex = 1;
        private bool _started;
        private Coroutine _aiLoop;
        private int _lastAiTurnHandled = -1;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (_humanCoordinator != null)
            {
                _humanCoordinator.SnapshotChanged -= OnHumanSnapshot;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public async Task<bool> StartMatchAsync()
        {
            if (_started)
            {
                return true;
            }
            _started = true;

            try
            {
                _baseUrl = ConfigManager.GetApiBaseUrl();

                // 1) Human session (wait briefly for editor auto-login to finish).
                var auth = await WaitForAuthenticatedSessionAsync();
                if (auth == null)
                {
                    Debug.LogError("[ServerSP] No authenticated human session; cannot start AI match.");
                    return false;
                }
                _humanId = auth.CurrentPlayerId;
                _humanToken = SecureTokenStorage.GetToken();

                if (!ServiceLocator.TryResolve<CardGameApiClient>(out var api) || api == null)
                {
                    api = new CardGameApiClient(_baseUrl);
                }

                var humanDeckId = await ResolveFirstDeckIdAsync(api, _humanId, "human");
                if (string.IsNullOrWhiteSpace(humanDeckId))
                {
                    return false;
                }

                // 2) AI session: login as the AI account, capture its token + first deck.
                var aiDeckId = await LoginAiAndResolveDeckAsync(api);
                if (string.IsNullOrWhiteSpace(aiDeckId))
                {
                    return false;
                }
                RestoreHumanToken();

                // 3) Human creates the private match (seat 0).
                var mm = new MatchmakingApiClient(_baseUrl);
                var humanRes = await mm.CreatePrivateMatch(_humanId, humanDeckId, "AI Match");
                if (humanRes == null || string.IsNullOrWhiteSpace(humanRes.matchId))
                {
                    Debug.LogError("[ServerSP] CreatePrivateMatch failed.");
                    return false;
                }
                _matchId = humanRes.matchId;

                GamePlayStateManager.Instance?.SetMatchInfo(_matchId, _humanId, _aiId);
                GamePlayStateManager.Instance?.SetMatchRules(humanRes.rulesetId, humanRes.rules);
                GameModeManager.Instance?.SetOnlineMode();

                // 4) AI joins the room (seat 1) using its own token.
                WithAiToken();
                MatchReservationDto aiRes;
                try
                {
                    aiRes = await mm.JoinPrivateMatch(_aiId, aiDeckId, humanRes.roomCode);
                }
                finally
                {
                    RestoreHumanToken();
                }
                if (aiRes == null)
                {
                    Debug.LogError("[ServerSP] AI JoinPrivateMatch failed.");
                    return false;
                }
                _aiSeatIndex = aiRes.seatIndex;

                // 5) Human connects through the normal coordinator (publishes snapshots to the bus
                //    that GameplayPresenter3D renders — exactly like multiplayer).
                _humanCoordinator = MatchSignalRCoordinator.Instance;
                if (_humanCoordinator == null)
                {
                    _humanCoordinator = new GameObject("MatchSignalRCoordinator").AddComponent<MatchSignalRCoordinator>();
                }
                var connected = await _humanCoordinator.ConnectAsync(_matchId, _humanId, humanRes.reconnectToken, humanRes.seatIndex);
                if (!connected)
                {
                    Debug.LogError("[ServerSP] Human failed to connect to the match.");
                    return false;
                }
                _humanCoordinator.SnapshotChanged += OnHumanSnapshot;

                // 6) Ready up both seats. AI over HTTP, human over its connection.
                _matchplay = new MatchplayApiClient(_baseUrl);
                WithAiToken();
                try
                {
                    await _matchplay.SetReady(_matchId, _aiId, true);
                }
                finally
                {
                    RestoreHumanToken();
                }
                await _humanCoordinator.SetReadyAsync(true);

                Debug.Log($"[ServerSP] Server-authoritative AI match started (match {_matchId}, AI seat {_aiSeatIndex}).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerSP] Failed to start server AI match: {ex.Message}");
                return false;
            }
        }

        // The human coordinator's snapshot tells us whose turn it is. When it is the AI's turn, drive it.
        private void OnHumanSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null || snapshot.duelEnded || snapshot.phase != 2)
            {
                return;
            }
            if (snapshot.activeSeatIndex == _aiSeatIndex && _aiLoop == null && _lastAiTurnHandled != snapshot.turnNumber)
            {
                _lastAiTurnHandled = snapshot.turnNumber;
                _aiLoop = StartCoroutine(RunAiTurn());
            }
        }

        private IEnumerator RunAiTurn()
        {
            yield return new WaitForSeconds(aiFirstActionDelay);

            var safety = Mathf.Max(1, maxAiActionsPerTurn);
            while (safety-- > 0)
            {
                var step = ActAiStepAsync();
                while (!step.IsCompleted)
                {
                    yield return null;
                }

                if (step.Exception != null || !step.Result)
                {
                    break; // turn ended (or error)
                }

                yield return new WaitForSeconds(aiActionDelay);
            }

            _aiLoop = null;
        }

        // Fetches the AI seat's own snapshot, picks a move and sends it. Returns true to keep playing
        // this turn, false when the turn ended. All HTTP runs with the AI token swapped in.
        private async Task<bool> ActAiStepAsync()
        {
            WithAiToken();
            try
            {
                var snapshot = await _matchplay.GetSnapshot(_matchId, _aiId);
                if (snapshot == null || snapshot.duelEnded || snapshot.activeSeatIndex != _aiSeatIndex)
                {
                    return false;
                }

                var move = _ai.BuildMove(snapshot, _aiSeatIndex, aiDifficulty);
                if (move.IsEndTurn || string.IsNullOrWhiteSpace(move.RuntimeCardKey))
                {
                    await _matchplay.EndTurn(_matchId, _aiId);
                    return false;
                }

                await _matchplay.PlayCard(_matchId, _aiId, move.RuntimeCardKey, (int)move.Slot);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerSP] AI step failed: {ex.Message}; ending AI turn.");
                try { await _matchplay.EndTurn(_matchId, _aiId); } catch { /* best effort */ }
                return false;
            }
            finally
            {
                RestoreHumanToken();
            }
        }

        private async Task<AuthService> WaitForAuthenticatedSessionAsync()
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (ServiceLocator.TryResolve<AuthService>(out var authService) && authService != null && authService.IsAuthenticated)
                {
                    return authService;
                }
                await Task.Delay(200);
            }
            return null;
        }

        private async Task<string> ResolveFirstDeckIdAsync(CardGameApiClient api, string playerId, string label)
        {
            var decks = await api.FetchPlayerDecksAsync(playerId);
            var deck = decks?
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.deckId))
                .OrderByDescending(item => item.isActive)
                .FirstOrDefault();
            if (deck == null)
            {
                Debug.LogError($"[ServerSP] {label} player '{playerId}' has no server decks.");
                return null;
            }
            return deck.deckId;
        }

        // Logs in as the AI account WITHOUT clobbering the human session, captures the AI token + id,
        // and resolves the AI's first deck. Leaves the AI token active (caller restores the human token).
        private async Task<string> LoginAiAndResolveDeckAsync(CardGameApiClient api)
        {
            var authClient = new AuthApiClient(_baseUrl);

            // Clear global session so the login call doesn't carry the human bearer token.
            SecureTokenStorage.SaveToken(null);
            SecureTokenStorage.SavePlayerId(string.Empty);

            var auth = await authClient.Login(aiDeckAccountEmail, aiDeckAccountPassword);
            if (auth == null || string.IsNullOrWhiteSpace(auth.token) || string.IsNullOrWhiteSpace(auth.resolvedUserId))
            {
                Debug.LogError($"[ServerSP] Could not login AI account '{aiDeckAccountEmail}'.");
                RestoreHumanToken();
                return null;
            }
            _aiId = auth.resolvedUserId;
            _aiToken = auth.token;

            WithAiToken();
            return await ResolveFirstDeckIdAsync(api, _aiId, "AI");
        }

        private void WithAiToken()
        {
            SecureTokenStorage.SaveToken(_aiToken);
            SecureTokenStorage.SavePlayerId(_aiId);
        }

        private void RestoreHumanToken()
        {
            SecureTokenStorage.SaveToken(_humanToken);
            SecureTokenStorage.SavePlayerId(_humanId);
        }
    }
}
