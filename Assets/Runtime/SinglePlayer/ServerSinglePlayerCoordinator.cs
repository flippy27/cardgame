using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI;

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
        // Cards the AI failed to play THIS turn (e.g. a non-unit that needs a target it can't pick,
        // or an illegal slot). Excluded from further attempts so the AI moves on to a playable card
        // instead of giving up the whole turn. Reset at the start of each AI turn.
        private readonly HashSet<string> _aiFailedCardKeys = new();

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

            // Loading overlay so the player never stares at an empty board while the private match is
            // created, the SignalR connection is established and the first snapshot arrives. Hidden by
            // GameplayPresenter3D once the first hand/board is actually presented.
            var loading = UI.MatchLoadingOverlay.Show("Preparing battle...");
            loading.SetProgress(0.05f);

            var success = false;
            try
            {
                _baseUrl = ConfigManager.GetApiBaseUrl();

                // 1) Human session (wait briefly for editor auto-login to finish).
                loading.SetStatus("Signing in...");
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

                // Honor the deck the player picked in the pre-battle deck selector; fall back to
                // their first/active server deck if nothing was selected.
                var humanDeckId = GamePlayStateManager.Instance?.GetSelectedDeck().deckId;
                if (string.IsNullOrWhiteSpace(humanDeckId))
                {
                    humanDeckId = await ResolveFirstDeckIdAsync(api, _humanId, "human");
                }
                if (string.IsNullOrWhiteSpace(humanDeckId))
                {
                    return false;
                }

                // Warm the card composites for the human deck on a coroutine WHILE the rest of the start
                // sequence (AI login, match create/join, connect) runs. This spreads the CPU compositing
                // across the loading screen so the opening-hand RefreshHand burst hits a warm cache
                // instead of compositing every card synchronously the instant the board appears — the
                // single biggest match-start hitch on low-end devices. Requests are resolved NOW (before
                // any AI-token swap) from the selected-deck card ids + the in-memory catalog, so the
                // coroutine itself does no network I/O and can't race the token swaps below.
                var warmRequests = BuildDeckWarmRequests();
                if (warmRequests.Count > 0)
                {
                    StartCoroutine(WarmComposites(warmRequests));
                }

                // 2) AI session: login as the AI account, capture its token + first deck.
                loading.SetStatus("Summoning opponent...");
                loading.SetProgress(0.25f);
                var aiDeckId = await LoginAiAndResolveDeckAsync(api);
                if (string.IsNullOrWhiteSpace(aiDeckId))
                {
                    return false;
                }
                RestoreHumanToken();

                // 3) Human creates the private match (seat 0).
                loading.SetStatus("Creating match...");
                loading.SetProgress(0.45f);
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
                MatchmakingApiClient.MatchReservationDto aiRes;
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
                loading.SetStatus("Connecting to match...");
                loading.SetProgress(0.65f);
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

                // Both seats ready: wait for the first snapshot to be presented. The overlay is hidden
                // by GameplayPresenter3D once the first hand/board is rendered (see HideLoadingOverlayIfNeeded);
                // this just advances the bar so it doesn't sit frozen during the snapshot round-trip.
                loading.SetStatus("Dealing cards...");
                loading.SetProgress(0.85f);

                Debug.Log($"[ServerSP] Server-authoritative AI match started (match {_matchId}, AI seat {_aiSeatIndex}).");
                success = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerSP] Failed to start server AI match: {ex.Message}");
                return false;
            }
            finally
            {
                // On any failure path (early returns or exception) hide the overlay so the player isn't
                // stuck behind it. On success the overlay stays up and is hidden by GameplayPresenter3D
                // once the first hand/board is actually presented.
                if (!success)
                {
                    UI.MatchLoadingOverlay.HideCurrent();
                }
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
            _aiFailedCardKeys.Clear();

            // Wait until the human's battle presentation has finished animating, so the AI doesn't
            // appear to act "first" / on top of the player's own attacks resolving.
            while (GameplayPresenter3D.Instance != null && GameplayPresenter3D.Instance.IsPlayingBattlePresentation)
            {
                yield return null;
            }

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

                var move = _ai.BuildMove(snapshot, _aiSeatIndex, aiDifficulty, _aiFailedCardKeys);
                if (move.IsEndTurn || string.IsNullOrWhiteSpace(move.RuntimeCardKey))
                {
                    await _matchplay.EndTurn(_matchId, _aiId);
                    return false;
                }

                try
                {
                    await _matchplay.PlayCard(_matchId, _aiId, move.RuntimeCardKey, (int)move.Slot);
                    return true;
                }
                catch (Exception playEx)
                {
                    // This specific card couldn't be played (a non-unit that needs a target the simple
                    // AI doesn't pick, or an illegal slot). Exclude it and keep going so the AI still
                    // plays its other cards (units) this turn instead of stalling with a full hand.
                    Debug.LogWarning($"[ServerSP] AI could not play {move.RuntimeCardKey}: {playEx.Message}; trying another card.");
                    _aiFailedCardKeys.Add(move.RuntimeCardKey);
                    return true;
                }
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

        // Resolves the composite-warm requests for the selected deck SYNCHRONOUSLY from the in-memory
        // card catalog (loaded at login). Returns the hand+board composite requests for each distinct
        // deck card. No network I/O, so it's safe to call before the AI-token swaps. Empty if the deck
        // card ids or the catalog aren't available (then warming is simply skipped).
        private List<(string cardId, int cardType, int cardRarity, int cardFaction, int unitType, bool hasArmor, bool board)> BuildDeckWarmRequests()
        {
            var requests = new List<(string cardId, int cardType, int cardRarity, int cardFaction, int unitType, bool hasArmor, bool board)>();

            var cardIds = GamePlayStateManager.Instance?.GetSelectedDeck().cardIds;
            if (cardIds == null || cardIds.Count == 0)
            {
                return requests; // no selected-deck card ids cached → skip warming (lazy path still works).
            }

            var catalog = GameService.Instance?.CardCatalog;
            if (catalog == null)
            {
                return requests;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cardId in cardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId) || !seen.Add(cardId))
                {
                    continue;
                }
                if (!catalog.TryGetCard(cardId, out var card) || card == null)
                {
                    continue;
                }
                var hasArmor = card.armor > 0;
                // Hand composite (the opening hand) first; board composite for when it gets played.
                requests.Add((cardId, card.cardType, card.cardRarity, card.cardFaction, card.unitType, hasArmor, false));
                requests.Add((cardId, card.cardType, card.cardRarity, card.cardFaction, card.unitType, hasArmor, true));
            }

            return requests;
        }

        // Pre-builds the framed card composites a couple per frame, so the CPU compositing is spread
        // across the loading screen instead of hitting all at once when the opening hand is dealt. The
        // composites are cached in CardArtLibrary, so the in-match RefreshHand just looks them up. Pure
        // CPU, no network I/O.
        private IEnumerator WarmComposites(
            List<(string cardId, int cardType, int cardRarity, int cardFaction, int unitType, bool hasArmor, bool board)> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                yield break;
            }

            // A couple of composites per frame keeps each frame cheap on old hardware while the loading
            // overlay's spinner stays smooth.
            const int perFrame = 2;
            var built = 0;
            for (var i = 0; i < requests.Count; i++)
            {
                CardArtLibrary.WarmCompositeAt(requests, i);
                if (++built >= perFrame)
                {
                    built = 0;
                    var overlay = UI.MatchLoadingOverlay.Instance;
                    if (overlay != null)
                    {
                        // Nudge the bar between 0.85..0.98 so it visibly fills as warming progresses.
                        overlay.SetProgress(0.85f + 0.13f * ((i + 1f) / requests.Count));
                    }
                    yield return null;
                }
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
