using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.SinglePlayer
{
    /// <summary>
    /// Controlador local para jugar contra IA sin depender de NGO.
    /// Esta versión adapta el snapshot al flujo nuevo del presenter
    /// (matchPhase / ready / connected / winner / status).
    /// </summary>
    public sealed class LocalSinglePlayerCoordinator : MonoBehaviour
    {
        [Header("Setup")]
        public bool autoStartOnStart = false;
        public int localPlayerIndex = 0;
        public DuelRulesProfile rulesProfile;
        public DeckDefinition localPlayerDeck;
        public DeckDefinition enemyDeck;

        [Header("Server Decks")]
        [SerializeField] private bool useServerDecks = true;
        [SerializeField] private bool allowInspectorDeckFallback;
        [SerializeField] private string aiDeckAccountEmail = "playertwo@flippy.com";
        [SerializeField] private string aiDeckAccountPassword = "123456";

        [Header("AI")]
        public AiDifficulty aiDifficulty = AiDifficulty.Medium;
        public float aiFirstActionDelay = 0.45f;
        public float aiActionDelay = 0.35f;
        public int maxAiActionsPerTurn = 8;

        private readonly SimpleCardAiAgent _aiAgent = new();
        private DuelRuntime _runtime;
        private Coroutine _aiRoutine;
        private bool _startingMatch;
        private string _currentMatchId = "local-ai-match";
        private string _localPlayerId = LocalSinglePlayerId;
        private string _aiPlayerId = LocalAiPlayerId;
        private const string LocalSinglePlayerMatchIdPrefix = "local-ai-match";
        private const string LocalSinglePlayerId = "local-player";
        private const string LocalAiPlayerId = "local-ai";

        public static LocalSinglePlayerCoordinator Instance { get; private set; }

        public bool IsActive => _runtime != null;
        public DuelSnapshotDto LatestSnapshot { get; private set; }
        public DuelRuntime DuelRuntime => _runtime;
        public DuelState DuelState => _runtime?.State;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            TryAutoLoadDefaults();

            if (autoStartOnStart)
            {
                StartMatch();
            }
        }

        private void TryAutoLoadDefaults()
        {
            if (rulesProfile == null)
            {
                var found = Resources.Load<DuelRulesProfile>("DuelRulesProfile");
                if (found) rulesProfile = found;
            }

            if (localPlayerDeck == null)
            {
                var found = Resources.LoadAll<DeckDefinition>("Decks");
                if (found.Length > 0) localPlayerDeck = found[0];
            }

            if (enemyDeck == null)
            {
                var found = Resources.LoadAll<DeckDefinition>("Decks");
                if (found.Length > 1) enemyDeck = found[1];
                else if (found.Length > 0) enemyDeck = found[0];
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Reinicia la partida local completa.
        /// </summary>
        public async void StartMatch()
        {
            if (_startingMatch)
            {
                return;
            }

            _startingMatch = true;
            try
            {
                TryAutoLoadDefaults();

                if (useServerDecks)
                {
                    var loaded = await TryLoadServerDecksAsync();
                    if (!loaded && !allowInspectorDeckFallback)
                    {
                        Debug.LogError("[LocalAI] Could not load real server decks. Local AI match will not start because fallback decks are disabled.");
                        return;
                    }
                }

                if (rulesProfile == null || localPlayerDeck == null || enemyDeck == null)
                {
                    Debug.LogWarning("LocalSinglePlayerCoordinator requires rules and both decks.");
                    return;
                }

                if (_aiRoutine != null)
                {
                    StopCoroutine(_aiRoutine);
                    _aiRoutine = null;
                }

                _currentMatchId = $"{LocalSinglePlayerMatchIdPrefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                _runtime = new DuelRuntime(rulesProfile);
                _runtime.StartGame(localPlayerDeck, enemyDeck);
                GamePlayStateManager.Instance?.SetMatchInfo(_currentMatchId, _localPlayerId, _aiPlayerId);

                PublishSnapshot();
                TryStartAiTurnIfNeeded();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAI] Failed to start local AI match: {ex.Message}");
            }
            finally
            {
                _startingMatch = false;
            }
        }

        private async Task<bool> TryLoadServerDecksAsync()
        {
            if (!ServiceLocator.TryResolve<CardGameApiClient>(out var apiClient))
            {
                apiClient = new CardGameApiClient(ConfigManager.GetApiBaseUrl());
            }

            if (!ServiceLocator.TryResolve<AuthService>(out var authService) || !authService.IsAuthenticated)
            {
                Debug.LogError("[LocalAI] Cannot load local player's server deck: no authenticated player.");
                return false;
            }

            _localPlayerId = authService.CurrentPlayerId;
            localPlayerDeck = await FetchFirstServerDeckAsCurrentSessionAsync(apiClient, _localPlayerId, "Local Player");
            enemyDeck = await FetchFirstServerDeckAsAccountAsync(apiClient, aiDeckAccountEmail, aiDeckAccountPassword, "AI");

            return localPlayerDeck != null && enemyDeck != null;
        }

        private async Task<DeckDefinition> FetchFirstServerDeckAsCurrentSessionAsync(CardGameApiClient apiClient, string playerId, string label)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError($"[LocalAI] {label} player id is missing.");
                return null;
            }

            return await FetchFirstServerDeckForPlayerAsync(apiClient, playerId, label);
        }

        private async Task<DeckDefinition> FetchFirstServerDeckAsAccountAsync(CardGameApiClient apiClient, string email, string password, string label)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Debug.LogError($"[LocalAI] {label} account credentials are missing.");
                return null;
            }

            var backupToken = SecureTokenStorage.GetToken();
            var backupPlayerId = SecureTokenStorage.GetPlayerId();
            var backupEmail = SecureTokenStorage.GetEmail();
            var backupExpiry = SecureTokenStorage.GetTokenExpiry();

            try
            {
                // Login as the AI account without leaking the local player's bearer token into auth.
                SecureTokenStorage.SaveToken(null);
                SecureTokenStorage.SavePlayerId(string.Empty);
                SecureTokenStorage.SaveEmail(string.Empty);
                SecureTokenStorage.SaveTokenExpiry(0);

                var authClient = new AuthApiClient(ConfigManager.GetApiBaseUrl());
                var auth = await authClient.Login(email, password);
                if (auth == null || string.IsNullOrWhiteSpace(auth.token) || string.IsNullOrWhiteSpace(auth.resolvedUserId))
                {
                    Debug.LogError($"[LocalAI] Could not login {label} account '{email}'.");
                    return null;
                }

                _aiPlayerId = auth.resolvedUserId;
                SecureTokenStorage.SaveToken(auth.token);
                SecureTokenStorage.SavePlayerId(auth.resolvedUserId);
                SecureTokenStorage.SaveEmail(email);
                SecureTokenStorage.SaveTokenExpiry(DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());

                return await FetchFirstServerDeckForPlayerAsync(apiClient, auth.resolvedUserId, label);
            }
            finally
            {
                RestoreSecureSession(backupToken, backupPlayerId, backupEmail, backupExpiry);
            }
        }

        private async Task<DeckDefinition> FetchFirstServerDeckForPlayerAsync(CardGameApiClient apiClient, string playerId, string label)
        {
            var decks = await apiClient.FetchPlayerDecksAsync(playerId);
            var deck = decks?
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.deckId))
                .OrderByDescending(item => item.isActive)
                .FirstOrDefault();

            if (deck == null)
            {
                Debug.LogError($"[LocalAI] {label} player '{playerId}' has no server decks.");
                return null;
            }

            var cards = await apiClient.FetchCardsByDeckAsync(playerId, deck.deckId);
            if ((cards == null || cards.Count == 0) && deck.cardIds != null && deck.cardIds.Count > 0)
            {
                cards = await FetchCardsOneByOneAsync(apiClient, deck.cardIds);
            }

            if (cards == null || cards.Count == 0)
            {
                Debug.LogError($"[LocalAI] {label} deck '{deck.deckId}' resolved to 0 cards.");
                return null;
            }

            var runtimeDeck = BuildRuntimeDeck(deck, cards);
            Debug.Log($"[LocalAI] Loaded {label} server deck '{runtimeDeck.displayName}' ({runtimeDeck.deckId}) with {runtimeDeck.GetTotalCards()} cards for player '{playerId}'.");
            return runtimeDeck;
        }

        private static async Task<List<ServerCardDefinition>> FetchCardsOneByOneAsync(CardGameApiClient apiClient, IEnumerable<string> cardIds)
        {
            var results = new List<ServerCardDefinition>();
            foreach (var cardId in cardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    continue;
                }

                try
                {
                    var card = await apiClient.FetchCard(cardId);
                    if (card != null)
                    {
                        results.Add(card);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LocalAI] Could not fetch card '{cardId}' for local AI deck: {ex.Message}");
                }
            }

            return results;
        }

        private static DeckDefinition BuildRuntimeDeck(DeckDto deck, List<ServerCardDefinition> cards)
        {
            var runtimeDeck = ScriptableObject.CreateInstance<DeckDefinition>();
            runtimeDeck.hideFlags = HideFlags.DontSave;
            runtimeDeck.deckId = deck.deckId;
            runtimeDeck.displayName = string.IsNullOrWhiteSpace(deck.displayName) ? deck.deckName : deck.displayName;
            runtimeDeck.description = deck.description;
            runtimeDeck.deckType = "Server";
            runtimeDeck.cards = cards
                .Where(card => card != null && !string.IsNullOrWhiteSpace(card.cardId))
                .Select(card => new DeckDefinition.DeckCard
                {
                    card = BuildRuntimeCard(card),
                    quantity = 1
                })
                .ToArray();

            return runtimeDeck;
        }

        private static CardDefinition BuildRuntimeCard(ServerCardDefinition serverCard)
        {
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.hideFlags = HideFlags.DontSave;
            card.cardId = serverCard.cardId;
            card.displayName = FirstNonEmpty(serverCard.displayName, serverCard.name, serverCard.cardId);
            card.description = serverCard.description;
            card.manaCost = serverCard.manaCost;
            card.attack = serverCard.attack;
            card.health = serverCard.health;
            card.armor = serverCard.armor;
            card.cardType = ParseEnum(serverCard.cardType, CardType.Unit);
            card.rarity = ParseEnum(serverCard.rarity, CardRarity.Common);
            card.unitType = ParseUnitType(serverCard.unitType);
            card.attackMotionLevel = serverCard.battlePresentation?.attackMotionLevel ?? 0;
            card.attackShakeLevel = serverCard.battlePresentation?.attackShakeLevel ?? 0;
            card.attackDeliveryType = FirstNonEmpty(serverCard.attackDeliveryType, serverCard.battlePresentation?.attackDeliveryType);
            card.abilities = BuildRuntimeAbilities(serverCard.abilities);
            return card;
        }

        private static AbilityDefinition[] BuildRuntimeAbilities(CardAbilityDto[] abilities)
        {
            if (abilities == null || abilities.Length == 0)
            {
                return Array.Empty<AbilityDefinition>();
            }

            return abilities
                .Where(ability => ability != null && !string.IsNullOrWhiteSpace(ability.abilityId))
                .Select(ability =>
                {
                    var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
                    definition.hideFlags = HideFlags.DontSave;
                    definition.abilityId = ability.abilityId;
                    definition.displayName = FirstNonEmpty(ability.displayName, ability.abilityId);
                    return definition;
                })
                .ToArray();
        }

        private static UnitType ParseUnitType(int unitType)
        {
            return unitType switch
            {
                1 => UnitType.Ranged,
                2 => UnitType.Magic,
                _ => UnitType.Melee
            };
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static void RestoreSecureSession(string token, string playerId, string email, long expiry)
        {
            SecureTokenStorage.SaveToken(token);
            SecureTokenStorage.SavePlayerId(playerId);
            SecureTokenStorage.SaveEmail(email);
            SecureTokenStorage.SaveTokenExpiry(expiry);
        }

        /// <summary>
        /// Juega una carta del humano local.
        /// </summary>
        public bool RequestPlayCard(string runtimeCardKey, BoardSlot slot)
        {
            if (_runtime == null)
            {
                return false;
            }

            if (_runtime.TryPlayCard(localPlayerIndex, runtimeCardKey, slot))
            {
                PublishSnapshot();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Termina el turno del humano local.
        /// </summary>
        public bool RequestEndTurn()
        {
            Debug.Log("[LocalAI] RequestEndTurn called");
            if (_runtime == null)
            {
                Debug.LogWarning("[LocalAI] RequestEndTurn rejected: runtime is null");
                return false;
            }

            if (_runtime.TryEndTurn(localPlayerIndex))
            {
                Debug.Log("[LocalAI] RequestEndTurn succeeded, publishing snapshot");
                PublishSnapshot();
                TryStartAiTurnIfNeeded();
                return true;
            }

            Debug.LogWarning("[LocalAI] RequestEndTurn failed: TryEndTurn returned false");
            return false;
        }

        public bool RequestDiscardCard(string runtimeCardKey)
        {
            Debug.Log($"[LocalAI] RequestDiscardCard called for {runtimeCardKey}");
            if (_runtime == null)
            {
                Debug.LogWarning("[LocalAI] RequestDiscardCard rejected: runtime is null");
                return false;
            }

            if (_runtime.DiscardCard(localPlayerIndex, runtimeCardKey))
            {
                Debug.Log("[LocalAI] RequestDiscardCard succeeded, publishing snapshot");
                PublishSnapshot();
                return true;
            }

            Debug.LogWarning("[LocalAI] RequestDiscardCard failed: DiscardCard returned false");
            return false;
        }

        private void TryStartAiTurnIfNeeded()
        {
            if (_runtime == null || _runtime.State.DuelEnded)
            {
                return;
            }

            if (_runtime.State.ActivePlayerIndex == localPlayerIndex)
            {
                return;
            }

            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
            }

            _aiRoutine = StartCoroutine(RunAiTurn());
        }

        private IEnumerator RunAiTurn()
        {
            yield return new WaitForSeconds(aiFirstActionDelay);

            var aiPlayerIndex = 1 - localPlayerIndex;
            var safety = Mathf.Max(1, maxAiActionsPerTurn);

            while (safety-- > 0 &&
                   _runtime != null &&
                   !_runtime.State.DuelEnded &&
                   _runtime.State.ActivePlayerIndex == aiPlayerIndex)
            {
                var aiPlayer = _runtime.State.GetPlayer(aiPlayerIndex);
                if (aiPlayer != null && aiPlayer.Hand.Count > CardConstants.MaxHandSize)
                {
                    var cardToDiscard = aiPlayer.Hand[UnityEngine.Random.Range(0, aiPlayer.Hand.Count)];
                    _runtime.DiscardCard(aiPlayerIndex, cardToDiscard.RuntimeHandKey);
                    PublishSnapshot();
                    yield return new WaitForSeconds(aiActionDelay);
                    continue;
                }

                var move = _aiAgent.BuildMove(_runtime, aiPlayerIndex, aiDifficulty);

                if (move.IsEndTurn)
                {
                    _runtime.TryEndTurn(aiPlayerIndex);
                    PublishSnapshot();
                    _aiRoutine = null;
                    yield break;
                }

                if (_runtime.TryPlayCard(aiPlayerIndex, move.RuntimeCardKey, move.Slot))
                {
                    PublishSnapshot();
                }
                else
                {
                    _runtime.TryEndTurn(aiPlayerIndex);
                    PublishSnapshot();
                    _aiRoutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(aiActionDelay);
            }

            if (_runtime != null &&
                !_runtime.State.DuelEnded &&
                _runtime.State.ActivePlayerIndex == aiPlayerIndex)
            {
                _runtime.TryEndTurn(aiPlayerIndex);
                PublishSnapshot();
            }

            _aiRoutine = null;
        }

        private void PublishSnapshot()
        {
            if (_runtime == null)
            {
                return;
            }

            var snapshot = _runtime.CreateSnapshot(localPlayerIndex);

            // Adaptación al presenter nuevo.
            snapshot.matchPhase = _runtime.State.DuelEnded
                ? MatchPhase.Completed
                : MatchPhase.InProgress;
            snapshot.matchId = _currentMatchId;
            snapshot.activePlayerId = snapshot.activePlayerIndex == localPlayerIndex ? _localPlayerId : _aiPlayerId;
            snapshot.isLocalPlayersTurn = snapshot.activePlayerIndex == localPlayerIndex;
            if (snapshot.players != null)
            {
                if (localPlayerIndex >= 0 && localPlayerIndex < snapshot.players.Length)
                {
                    snapshot.players[localPlayerIndex].playerId = _localPlayerId;
                }

                var aiPlayerIndex = 1 - localPlayerIndex;
                if (aiPlayerIndex >= 0 && aiPlayerIndex < snapshot.players.Length)
                {
                    snapshot.players[aiPlayerIndex].playerId = _aiPlayerId;
                }
            }

            snapshot.localPlayerReady = true;
            snapshot.remotePlayerReady = true;
            snapshot.localPlayerConnected = true;
            snapshot.remotePlayerConnected = true;
            snapshot.connectedPlayers = 2;
            snapshot.reconnectGraceRemainingSeconds = 0f;
            snapshot.matchSeed = 0;

            if (_runtime.State.DuelEnded)
            {
                snapshot.winnerPlayerIndex = ResolveWinnerIndex(snapshot);
                snapshot.statusMessage = snapshot.winnerPlayerIndex == localPlayerIndex
                    ? "Victory"
                    : "Defeat";
            }
            else
            {
                snapshot.winnerPlayerIndex = -1;
                snapshot.statusMessage = snapshot.activePlayerIndex == localPlayerIndex
                    ? "Your turn"
                    : "Enemy turn";
            }

            LatestSnapshot = snapshot;
            BattleSnapshotBus.Publish(JsonUtility.ToJson(snapshot));
        }

        private int ResolveWinnerIndex(DuelSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                return -1;
            }

            return snapshot.endReason switch
            {
                DuelEndReason.EnemyHeroDefeated => localPlayerIndex,
                DuelEndReason.LocalHeroDefeated => 1 - localPlayerIndex,
                DuelEndReason.OpponentDisconnected => localPlayerIndex,
                _ => ResolveByHeroHealth(snapshot)
            };
        }

        private int ResolveByHeroHealth(DuelSnapshotDto snapshot)
        {
            if (snapshot.players == null || snapshot.players.Length < 2)
            {
                return -1;
            }

            var a = snapshot.players[0];
            var b = snapshot.players[1];

            if (a.heroHealth > b.heroHealth) return 0;
            if (b.heroHealth > a.heroHealth) return 1;
            return -1;
        }
    }
}
