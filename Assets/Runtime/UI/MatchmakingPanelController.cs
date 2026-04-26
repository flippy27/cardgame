using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Matchmaking UI backed by the CardDuel backend.
    /// Uses HTTP for reservation creation and SignalR for real-time match updates.
    /// </summary>
    public sealed class MatchmakingPanelController : MonoBehaviour
    {
        private const string RemotePlayerPlaceholderId = "__remote__";

        public MpsGameSessionService sessionService;
        public InputField joinCodeField;
        public InputField privateMatchNameField;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI joinCodeText;
        public Button localMatchButton;
        public Button quickMatchButton;
        public Button createPrivateButton;
        public Button joinByCodeButton;
        public Button advancedQueueButton;
        public Button leaveButton;
        public Button readyButton;
        public Button copyJoinCodeButton;
        public Button deckBuildingButton;
        public GameObject[] hideWhenInSession;
        public GameObject[] showWhenInSession;

        private CancellationTokenSource _matchmakerCts;
        private AuthService _authService;
        private CardGameApiClient _apiClient;
        private DeckManagementService _deckService;
        private MatchmakingService _matchmakingService;
        private MatchSignalRCoordinator _signalRCoordinator;
        private bool _busy;
        private bool _hasActiveMatch;
        private bool _localReady;
        private bool _readyRequestInFlight;
        private string _currentRoomCode;
        private MatchmakingApiClient.QueueMode _currentMode = MatchmakingApiClient.QueueMode.Casual;
        private string _currentRulesetName;
        private bool _waitingForOpponent = true;
        private int _connectedPlayers;
        private bool _loadedMainGame;

        private void Awake()
        {
            ResolveDependencies();
            AttachCoordinator(MatchSignalRCoordinator.Instance);
        }

        private void OnEnable()
        {
            AddListener(localMatchButton, HandleLocalMatch);
            AddListener(quickMatchButton, HandleQuickMatch);
            AddListener(createPrivateButton, HandleCreatePrivate);
            AddListener(joinByCodeButton, HandleJoinByCode);
            AddListener(advancedQueueButton, HandleAdvancedQueue);
            AddListener(leaveButton, HandleLeave);
            AddListener(readyButton, HandleReadyToggle);
            AddListener(copyJoinCodeButton, HandleCopyJoinCode);
            AddListener(deckBuildingButton, HandleDeckBuilding);
            RefreshJoinCodeText();
            RefreshReadyButtonText();
            RefreshButtonVisibility();
        }

        private void OnDisable()
        {
            RemoveListener(localMatchButton, HandleLocalMatch);
            RemoveListener(quickMatchButton, HandleQuickMatch);
            RemoveListener(createPrivateButton, HandleCreatePrivate);
            RemoveListener(joinByCodeButton, HandleJoinByCode);
            RemoveListener(advancedQueueButton, HandleAdvancedQueue);
            RemoveListener(leaveButton, HandleLeave);
            RemoveListener(readyButton, HandleReadyToggle);
            RemoveListener(copyJoinCodeButton, HandleCopyJoinCode);
            RemoveListener(deckBuildingButton, HandleDeckBuilding);
        }

        private void OnDestroy()
        {
            AttachCoordinator(null);
            _matchmakerCts?.Cancel();
            _matchmakerCts?.Dispose();
        }

        private void ResolveDependencies()
        {
            var baseUrl = ConfigManager.GetApiBaseUrl();

            _authService = ServiceLocator.TryResolve<AuthService>(out var authService)
                ? authService
                : new AuthService(baseUrl);

            _apiClient = ServiceLocator.TryResolve<CardGameApiClient>(out var apiClient)
                ? apiClient
                : new CardGameApiClient(baseUrl);

            _apiClient.SetAuthService(_authService);
            _deckService = ServiceLocator.TryResolve<DeckManagementService>(out var deckService)
                ? deckService
                : new DeckManagementService(_apiClient, _authService);

            _matchmakingService = GameService.Instance?.Matchmaking ?? new MatchmakingService(new MatchmakingApiClient(baseUrl), _authService);
        }

        private void AttachCoordinator(MatchSignalRCoordinator coordinator)
        {
            if (_signalRCoordinator == coordinator)
            {
                return;
            }

            if (_signalRCoordinator != null)
            {
                _signalRCoordinator.SnapshotChanged -= HandleSnapshotChanged;
                _signalRCoordinator.ErrorOccurred -= HandleCoordinatorError;
                _signalRCoordinator.ConnectionEstablished -= HandleConnectionEstablished;
                _signalRCoordinator.ConnectionLost -= HandleConnectionLost;
            }

            _signalRCoordinator = coordinator;

            if (_signalRCoordinator != null)
            {
                _signalRCoordinator.SnapshotChanged += HandleSnapshotChanged;
                _signalRCoordinator.ErrorOccurred += HandleCoordinatorError;
                _signalRCoordinator.ConnectionEstablished += HandleConnectionEstablished;
                _signalRCoordinator.ConnectionLost += HandleConnectionLost;
            }
        }

        private MatchSignalRCoordinator EnsureCoordinator()
        {
            if (MatchSignalRCoordinator.Instance != null)
            {
                AttachCoordinator(MatchSignalRCoordinator.Instance);
                return _signalRCoordinator;
            }

            var coordinatorGo = new GameObject("MatchSignalRCoordinator");
            var coordinator = coordinatorGo.AddComponent<MatchSignalRCoordinator>();
            AttachCoordinator(coordinator);
            MatchCoordinatorFactory.Instance.SetPreferredType(MatchCoordinatorFactory.CoordinatorType.SignalR);
            return coordinator;
        }

        private void HandleLocalMatch()
        {
            SetStatus("Starting local match...");
            GameLogger.Info("UI", "Local match selected");
            GameModeManager.Instance?.SetLocalMode();
            SceneBootstrap.LoadMainGame();
        }

        private async void HandleQuickMatch()
        {
            await ReserveAndConnectAsync("Quick matching...", deckId => _matchmakingService.QueueCasual(deckId));
        }

        private async void HandleCreatePrivate()
        {
            var matchName = privateMatchNameField != null && !string.IsNullOrWhiteSpace(privateMatchNameField.text)
                ? privateMatchNameField.text.Trim()
                : "Private Match";

            await ReserveAndConnectAsync("Creating private match...", deckId => _matchmakingService.CreatePrivate(deckId, matchName));
        }

        private async void HandleJoinByCode()
        {
            var roomCode = joinCodeField != null ? joinCodeField.text?.Trim().ToUpperInvariant() : string.Empty;
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                SetStatus("Enter a join code first.");
                return;
            }

            await ReserveAndConnectAsync("Joining private match...", deckId => _matchmakingService.JoinPrivate(deckId, roomCode));
        }

        private async void HandleAdvancedQueue()
        {
            _matchmakerCts?.Cancel();
            _matchmakerCts?.Dispose();
            _matchmakerCts = new CancellationTokenSource();
            await ReserveAndConnectAsync("Searching queue...", deckId => _matchmakingService.QueueCasual(deckId));
        }

        private async void HandleLeave()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            SetStatus("Leaving match...");

            try
            {
                if (_signalRCoordinator != null)
                {
                    var snapshot = _signalRCoordinator.CurrentSnapshot;
                    if (snapshot != null && snapshot.phase != 3 && snapshot.phase != 4)
                    {
                        await _signalRCoordinator.ForfeitAsync();
                    }

                    await _signalRCoordinator.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                GameLogger.Warning("MatchmakingUI", $"Leave flow completed with warning: {ex.Message}");
            }
            finally
            {
                ResetSessionState();
                _busy = false;
            }
        }

        private async void HandleReadyToggle()
        {
            if (_busy || _readyRequestInFlight)
            {
                return;
            }

            var coordinator = MatchSignalRCoordinator.Instance;
            if (coordinator == null || !coordinator.IsConnected)
            {
                SetStatus("Match coordinator not available.");
                return;
            }

            var snapshot = coordinator.CurrentSnapshot;
            if (snapshot == null || snapshot.phase == 0 || snapshot.connectedPlayers < 2)
            {
                SetStatus("Waiting for opponent before readying up.");
                return;
            }

            var desiredReady = !_localReady;
            SetStatus(desiredReady ? "Sending ready..." : "Removing ready...");
            _readyRequestInFlight = true;
            if (readyButton != null)
            {
                readyButton.interactable = false;
            }

            try
            {
                await coordinator.SetReadyAsync(desiredReady);

                var updatedSnapshot = coordinator.CurrentSnapshot;
                var updatedLocalSeatIndex = updatedSnapshot != null
                    ? SnapshotConverter.ResolveLocalSeatIndex(updatedSnapshot, updatedSnapshot.localSeatIndex)
                    : -1;
                var updatedLocalSeat = updatedSnapshot != null ? GetSeat(updatedSnapshot, updatedLocalSeatIndex) : null;
                var confirmedReady = updatedLocalSeat?.ready ?? _localReady;

                if (updatedSnapshot == null)
                {
                    SetStatus("Ready request sent. Waiting for server update...");
                }
                else if (confirmedReady == desiredReady)
                {
                    SetStatus(desiredReady ? "Ready confirmed." : "Ready removed.");
                }
                else
                {
                    SetStatus("Ready state did not change on the server.");
                }
            }
            finally
            {
                _readyRequestInFlight = false;
                if (readyButton != null)
                {
                    readyButton.interactable = true;
                }
            }
        }

        private void HandleCopyJoinCode()
        {
            if (string.IsNullOrWhiteSpace(_currentRoomCode))
            {
                SetStatus("No join code available.");
                return;
            }

            GUIUtility.systemCopyBuffer = _currentRoomCode;
            SetStatus("Join code copied.");
        }

        private void HandleDeckBuilding()
        {
            if (_busy)
            {
                return;
            }

            if (_authService == null || !_authService.IsAuthenticated)
            {
                SetStatus("Login before opening deck building.");
                return;
            }

            SceneBootstrap.LoadDeckBuilding();
        }

        private async Task ReserveAndConnectAsync(string workingMessage, Func<string, Task<MatchReservation>> reserveFunc)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            SetStatus(workingMessage);

            try
            {
                if (!_authService.IsAuthenticated)
                {
                    throw new InvalidOperationException("You must login before matchmaking.");
                }

                var deckId = await ResolveDeckIdAsync();
                var reservation = await reserveFunc(deckId);
                var coordinator = EnsureCoordinator();

                GameModeManager.Instance?.SetOnlineMode();
                _currentRoomCode = reservation.RoomCode;
                _currentMode = reservation.Mode;
                _waitingForOpponent = reservation.WaitingForOpponent;
                _connectedPlayers = reservation.WaitingForOpponent ? 1 : 2;
                _currentRulesetName = reservation.Rules?.displayName;
                RefreshJoinCodeText();
                _hasActiveMatch = true;
                _loadedMainGame = false;
                RefreshButtonVisibility();

                MatchStateMachine.InitializeMatch(reservation.MatchId, _authService.CurrentPlayerId, RemotePlayerPlaceholderId);

                if (GamePlayStateManager.Instance != null)
                {
                    var resolvedRulesetId = !string.IsNullOrWhiteSpace(reservation.RulesetId)
                        ? reservation.RulesetId
                        : reservation.Rules?.rulesetId;
                    GamePlayStateManager.Instance.SetMatchInfo(reservation.MatchId, _authService.CurrentPlayerId, RemotePlayerPlaceholderId);
                    GamePlayStateManager.Instance.SetMatchRules(resolvedRulesetId, reservation.Rules);
                }

                var connected = await coordinator.ConnectAsync(
                    reservation.MatchId,
                    _authService.CurrentPlayerId,
                    reservation.ReconnectToken,
                    reservation.SeatIndex);

                if (!connected)
                {
                    throw new InvalidOperationException("Could not connect to the match.");
                }

                var reservationStatus = reservation.WaitingForOpponent
                    ? BuildWaitingForOpponentStatus()
                    : "Opponent found. Press Ready.";
                SetStatus(BuildStatusWithRules(reservationStatus));
            }
            catch (Exception ex)
            {
                ResetSessionState();
                SetStatus($"Error: {ex.Message}");
                GameLogger.Error("MatchmakingUI", $"Reservation flow failed: {ex.Message}");
            }
            finally
            {
                _busy = false;
                RefreshButtonVisibility();
            }
        }

        private async Task<string> ResolveDeckIdAsync()
        {
            var decks = await _deckService.GetPlayerDecksAsync();
            if (decks == null || decks.Count == 0)
            {
                return await CreateEmergencyDeckAsync();
            }

            var validCatalogIds = await FetchValidCatalogCardIdsAsync();

            string requestedDeckId = null;

            if (GamePlayStateManager.Instance != null && GamePlayStateManager.Instance.HasValidDeck)
            {
                requestedDeckId = GamePlayStateManager.Instance.GetSelectedDeck().deckId;
            }
            else if (!string.IsNullOrWhiteSpace(DeckSelectionScreen.SelectedDeckId))
            {
                requestedDeckId = DeckSelectionScreen.SelectedDeckId;
            }

            if (!string.IsNullOrWhiteSpace(requestedDeckId))
            {
                var selectedDeck = decks.Find(deck => string.Equals(deck.deckId, requestedDeckId, StringComparison.Ordinal));
                if (selectedDeck != null)
                {
                    return await EnsureQueueReadyDeckAsync(selectedDeck, validCatalogIds);
                }

                GameLogger.Warning("MatchmakingUI", $"Selected deck '{requestedDeckId}' is unavailable or invalid for matchmaking. Falling back to the player's first valid server deck.");
            }

            var fallbackDeck = decks.FirstOrDefault();
            if (fallbackDeck == null)
            {
                GameLogger.Warning("MatchmakingUI", $"Player '{_authService.CurrentPlayerId}' has no playable decks against the current catalog. Creating an emergency deck.");
                return await CreateEmergencyDeckAsync(validCatalogIds);
            }

            GameLogger.Info("MatchmakingUI", $"No deck explicitly selected, using '{fallbackDeck.displayName}'.");
            return await EnsureQueueReadyDeckAsync(fallbackDeck, validCatalogIds);
        }

        private async Task<HashSet<string>> FetchValidCatalogCardIdsAsync()
        {
            var cards = await _apiClient.FetchAllCards();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (!string.IsNullOrWhiteSpace(card?.cardId))
                    {
                        ids.Add(card.cardId);
                    }
                }
            }

            if (ids.Count == 0)
            {
                throw new InvalidOperationException("Card catalog is empty or unavailable.");
            }

            return ids;
        }

        private async Task<string> CreateEmergencyDeckAsync(HashSet<string> validCatalogIds = null)
        {
            validCatalogIds ??= await FetchValidCatalogCardIdsAsync();

            var emergencyCardIds = validCatalogIds.Take(20).ToList();
            if (emergencyCardIds.Count < 20)
            {
                throw new InvalidOperationException("Unable to build an emergency deck from the current catalog.");
            }

            var deckId = $"deck_{_authService.CurrentPlayerId}_autofix";
            var displayName = "Emergency Match Deck";
            await _apiClient.UpsertDeckAsync(_authService.CurrentPlayerId, deckId, displayName, emergencyCardIds);

            if (GamePlayStateManager.Instance != null)
            {
                GamePlayStateManager.Instance.SetSelectedDeck(deckId, emergencyCardIds);
            }

            GameLogger.Warning("MatchmakingUI", $"Created emergency fallback deck '{deckId}' with {emergencyCardIds.Count} cards.");
            return deckId;
        }

        private async Task<string> EnsureQueueReadyDeckAsync(DeckDto sourceDeck, HashSet<string> validCatalogIds)
        {
            var sourceDisplayName = string.IsNullOrWhiteSpace(sourceDeck.displayName) ? sourceDeck.deckName : sourceDeck.displayName;
            var deckCards = await _apiClient.FetchCardsByDeckAsync(_authService.CurrentPlayerId, sourceDeck.deckId);
            GameLogger.Info("MatchmakingUI", $"Deck '{sourceDeck.deckId}' resolved to {deckCards?.Count ?? 0} cards via /api/v1/cards/by-deck.");

            if (IsResolvedDeckQueueReady(deckCards))
            {
                var resolvedCardIds = deckCards.Select(card => card.cardId).ToList();
                if (GamePlayStateManager.Instance != null)
                {
                    GamePlayStateManager.Instance.SetSelectedDeck(sourceDeck.deckId, resolvedCardIds);
                }

                GameLogger.Info("MatchmakingUI", $"Using server-resolved deck '{sourceDisplayName}' ({sourceDeck.deckId}) for matchmaking.");
                return sourceDeck.deckId;
            }

            var sanitizedCardIds = BuildQueueReadyCardIds(sourceDeck, deckCards, validCatalogIds);

            if (sanitizedCardIds.Count < 20)
            {
                GameLogger.Warning("MatchmakingUI", $"Deck '{sourceDeck.deckId}' did not yield enough valid cards. Falling back to emergency deck creation.");
                return await CreateEmergencyDeckAsync(validCatalogIds);
            }

            var queueDeckId = sourceDeck.deckId;
            var sourceMatchesSanitized =
                sourceDeck.cardIds != null &&
                sourceDeck.cardIds.Count == sanitizedCardIds.Count &&
                sourceDeck.cardIds.SequenceEqual(sanitizedCardIds);

            if (!sourceMatchesSanitized)
            {
                queueDeckId = $"queue_{_authService.CurrentPlayerId}";
                var queueDeckName = $"{sourceDisplayName} (Queue)";
                await _apiClient.UpsertDeckAsync(_authService.CurrentPlayerId, queueDeckId, queueDeckName, sanitizedCardIds);
                GameLogger.Warning("MatchmakingUI", $"Sanitized deck '{sourceDeck.deckId}' into '{queueDeckId}' with {sanitizedCardIds.Count} valid cards for matchmaking.");
            }
            else
            {
                GameLogger.Info("MatchmakingUI", $"Using validated selected deck '{sourceDisplayName}' ({sourceDeck.deckId}).");
            }

            if (GamePlayStateManager.Instance != null)
            {
                GamePlayStateManager.Instance.SetSelectedDeck(queueDeckId, sanitizedCardIds);
            }

            return queueDeckId;
        }

        private static bool IsResolvedDeckQueueReady(List<ServerCardDefinition> deckCards)
        {
            if (deckCards == null || deckCards.Count < 20 || deckCards.Count > 30)
            {
                return false;
            }

            return deckCards
                .Where(card => !string.IsNullOrWhiteSpace(card?.cardId))
                .GroupBy(card => card.cardId)
                .All(group => group.Count() <= 3);
        }

        private static List<string> BuildQueueReadyCardIds(DeckDto sourceDeck, List<ServerCardDefinition> resolvedDeckCards, HashSet<string> validCatalogIds)
        {
            var sanitized = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            void TryAdd(string cardId)
            {
                if (string.IsNullOrWhiteSpace(cardId) || !validCatalogIds.Contains(cardId))
                {
                    return;
                }

                counts.TryGetValue(cardId, out var count);
                if (count >= 3 || sanitized.Count >= 30)
                {
                    return;
                }

                counts[cardId] = count + 1;
                sanitized.Add(cardId);
            }

            if (resolvedDeckCards != null)
            {
                foreach (var card in resolvedDeckCards)
                {
                    TryAdd(card?.cardId);
                }
            }

            if (sourceDeck?.cardIds != null)
            {
                foreach (var cardId in sourceDeck.cardIds)
                {
                    TryAdd(cardId);
                }
            }

            if (sanitized.Count < 20)
            {
                foreach (var cardId in validCatalogIds)
                {
                    TryAdd(cardId);
                    if (sanitized.Count >= 20)
                    {
                        break;
                    }
                }
            }

            return sanitized;
        }

        private static bool IsDeckPlayable(DeckDto deck, HashSet<string> validCatalogIds)
        {
            if (deck == null || deck.cardIds == null)
            {
                return false;
            }

            if (deck.cardIds.Count < 20 || deck.cardIds.Count > 30)
            {
                return false;
            }

            foreach (var group in deck.cardIds.GroupBy(id => id))
            {
                if (group.Count() > 3)
                {
                    return false;
                }
            }

            return deck.cardIds.All(id => !string.IsNullOrWhiteSpace(id) && validCatalogIds.Contains(id));
        }

        private void HandleSnapshotChanged(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _hasActiveMatch = true;
            _currentRoomCode = snapshot.roomCode;
            _currentMode = (MatchmakingApiClient.QueueMode)snapshot.mode;
            _currentRulesetName = snapshot.rules?.displayName ?? _currentRulesetName;
            _waitingForOpponent = snapshot.connectedPlayers < 2 || snapshot.phase == 0;
            _connectedPlayers = snapshot.connectedPlayers;
            var resolvedLocalSeatIndex = SnapshotConverter.ResolveLocalSeatIndex(snapshot, snapshot.localSeatIndex);
            _localReady = GetSeat(snapshot, resolvedLocalSeatIndex)?.ready ?? false;
            _readyRequestInFlight = false;

            RefreshJoinCodeText();
            RefreshReadyButtonText();
            RefreshButtonVisibility();
            SyncMatchState(snapshot);
            SetStatus(BuildStatusText(snapshot));
        }

        private void HandleCoordinatorError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                SetStatus(message);
            }
        }

        private void HandleConnectionEstablished()
        {
            _hasActiveMatch = true;
            RefreshButtonVisibility();
        }

        private void HandleConnectionLost()
        {
            SetStatus("Connection lost. Trying fallback...");
            MatchStateMachine.PlayerDisconnected(MatchStateMachine.CurrentMatch?.player2Id ?? RemotePlayerPlaceholderId);
            RefreshButtonVisibility();
        }

        private void SyncMatchState(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var resolvedLocalSeatIndex = SnapshotConverter.ResolveLocalSeatIndex(snapshot, snapshot.localSeatIndex);
            var localSeat = GetSeat(snapshot, resolvedLocalSeatIndex);
            var remoteSeatIndex = resolvedLocalSeatIndex == 0 ? 1 : resolvedLocalSeatIndex == 1 ? 0 : -1;
            var remoteSeat = GetSeat(snapshot, remoteSeatIndex);
            var localReady = localSeat?.ready ?? false;
            var remoteReady = remoteSeat?.ready ?? false;
            var remoteConnected = remoteSeat?.connected ?? false;
            var localPlayerId = SnapshotConverter.ResolveLocalPlayerId(snapshot, resolvedLocalSeatIndex) ?? _authService.CurrentPlayerId;
            var remotePlayerId = SnapshotConverter.ResolveRemotePlayerId(snapshot, resolvedLocalSeatIndex) ?? RemotePlayerPlaceholderId;
            var resolvedRulesetId = !string.IsNullOrWhiteSpace(snapshot.rulesetId)
                ? snapshot.rulesetId
                : snapshot.rules?.rulesetId;
            GamePlayStateManager.Instance?.SetMatchRules(resolvedRulesetId, snapshot.rules);

            if (MatchStateMachine.CurrentMatch == null ||
                !string.Equals(MatchStateMachine.CurrentMatch.matchId, snapshot.matchId, StringComparison.Ordinal))
            {
                MatchStateMachine.InitializeMatch(snapshot.matchId, localPlayerId, remotePlayerId);
            }
            else
            {
                MatchStateMachine.SyncPlayerIds(localPlayerId, remotePlayerId);
            }

            MatchStateMachine.SetPlayerReady(localPlayerId, localReady);
            MatchStateMachine.SetPlayerReady(remotePlayerId, remoteReady);

            if (snapshot.phase == 2)
            {
                MatchStateMachine.StartMatch();
            }
            else if (snapshot.phase != 0 && !remoteConnected && snapshot.connectedPlayers < 2)
            {
                MatchStateMachine.PlayerDisconnected(remotePlayerId);
            }
            else if (remoteConnected && MatchStateMachine.CurrentState == MatchState.PlayerDisconnected)
            {
                MatchStateMachine.AttemptReconnect(_authService.CurrentPlayerId);
            }

            if (snapshot.phase == 3 || snapshot.phase == 4)
            {
                MatchStateMachine.EndMatch();
                _hasActiveMatch = false;
            }

            if (snapshot.phase == 2 && !_loadedMainGame)
            {
                _loadedMainGame = true;
                GameModeManager.Instance?.SetOnlineMode();

                if (GamePlayStateManager.Instance != null)
                {
                    GamePlayStateManager.Instance.SetMatchInfo(snapshot.matchId, localPlayerId, remotePlayerId);
                }

                SceneBootstrap.LoadMainGame();
            }
        }

        private string BuildStatusText(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Not connected.";
            }

            if (snapshot.connectedPlayers < 2 || snapshot.phase == 0)
            {
                return BuildStatusWithRules(BuildWaitingForOpponentStatus());
            }

            var baseStatus = snapshot.phase switch
            {
                1 => _localReady ? "Waiting for the other player..." : "Connected. Press Ready.",
                2 => $"Match in progress. Turn {snapshot.turnNumber}.",
                3 => snapshot.winnerSeatIndex == SnapshotConverter.ResolveLocalSeatIndex(snapshot, snapshot.localSeatIndex) ? "Victory!" : "Match completed.",
                4 => "Match abandoned.",
                _ => "Connected."
            };

            return BuildStatusWithRules(baseStatus);
        }

        private static SeatSnapshot GetSeat(MatchSnapshot snapshot, int index)
        {
            if (snapshot?.seats == null || index < 0 || index >= snapshot.seats.Length)
            {
                return null;
            }

            return snapshot.seats[index];
        }

        private void ResetSessionState()
        {
            _hasActiveMatch = false;
            _localReady = false;
            _currentRoomCode = string.Empty;
            _waitingForOpponent = true;
            _connectedPlayers = 0;
            _currentMode = MatchmakingApiClient.QueueMode.Casual;
            _currentRulesetName = null;
            _loadedMainGame = false;
            MatchStateMachine.EndMatch();
            RefreshJoinCodeText();
            RefreshReadyButtonText();
            RefreshButtonVisibility();
            SetStatus("Not in session");
        }

        private void RefreshButtonVisibility()
        {
            var inSession = _hasActiveMatch || (MatchSignalRCoordinator.Instance != null && MatchSignalRCoordinator.Instance.IsConnected);
            var canReadyUp = inSession && !_waitingForOpponent && _connectedPlayers >= 2;
            var showJoinCode = inSession &&
                               _currentMode == MatchmakingApiClient.QueueMode.Private &&
                               !string.IsNullOrWhiteSpace(_currentRoomCode);

            SetActive(hideWhenInSession, !inSession);
            SetActive(showWhenInSession, inSession);

            if (quickMatchButton != null) quickMatchButton.gameObject.SetActive(!inSession);
            if (createPrivateButton != null) createPrivateButton.gameObject.SetActive(!inSession);
            if (joinByCodeButton != null) joinByCodeButton.gameObject.SetActive(!inSession);
            if (advancedQueueButton != null) advancedQueueButton.gameObject.SetActive(!inSession);
            if (deckBuildingButton != null) deckBuildingButton.gameObject.SetActive(!inSession);
            if (leaveButton != null) leaveButton.gameObject.SetActive(inSession);
            if (readyButton != null) readyButton.gameObject.SetActive(canReadyUp);
            if (copyJoinCodeButton != null) copyJoinCodeButton.gameObject.SetActive(showJoinCode);
            if (joinCodeText != null) joinCodeText.gameObject.SetActive(showJoinCode);
            if (readyButton != null) readyButton.interactable = canReadyUp && !_readyRequestInFlight;
        }

        private void RefreshReadyButtonText()
        {
            if (readyButton == null)
            {
                return;
            }

            var legacyLabel = readyButton.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
            {
                legacyLabel.text = _localReady ? "Unready" : "Ready";
            }

            var tmpLabel = readyButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpLabel != null)
            {
                tmpLabel.text = _localReady ? "Unready" : "Ready";
            }
        }

        private void RefreshJoinCodeText()
        {
            if (joinCodeText != null)
            {
                joinCodeText.text = string.IsNullOrWhiteSpace(_currentRoomCode)
                    ? "Join Code: -"
                    : $"Join Code: {_currentRoomCode}";
            }
        }

        private string BuildWaitingForOpponentStatus()
        {
            return _currentMode == MatchmakingApiClient.QueueMode.Private
                ? "Waiting for opponent to join..."
                : "Searching for opponent...";
        }

        private string BuildStatusWithRules(string baseStatus)
        {
            if (string.IsNullOrWhiteSpace(_currentRulesetName))
            {
                return baseStatus;
            }

            return $"{baseStatus}\nRuleset: {_currentRulesetName}";
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static void SetActive(GameObject[] targets, bool active)
        {
            if (targets == null)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (target != null)
                {
                    target.SetActive(active);
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
