using System;
using System.Threading;
using System.Threading.Tasks;
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
        private string _currentRoomCode;

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
            if (_busy)
            {
                return;
            }

            var coordinator = MatchSignalRCoordinator.Instance;
            if (coordinator == null || !coordinator.IsConnected)
            {
                SetStatus("Match coordinator not available.");
                return;
            }

            var desiredReady = !_localReady;
            _localReady = desiredReady;
            RefreshReadyButtonText();
            SetStatus(desiredReady ? "Sending ready..." : "Removing ready...");

            await coordinator.SetReadyAsync(desiredReady);
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

                _currentRoomCode = reservation.RoomCode;
                RefreshJoinCodeText();
                _hasActiveMatch = true;
                RefreshButtonVisibility();

                MatchStateMachine.InitializeMatch(reservation.MatchId, _authService.CurrentPlayerId, RemotePlayerPlaceholderId);

                var connected = await coordinator.ConnectAsync(
                    reservation.MatchId,
                    _authService.CurrentPlayerId,
                    reservation.ReconnectToken,
                    reservation.SeatIndex);

                if (!connected)
                {
                    throw new InvalidOperationException("Could not connect to the match.");
                }

                SetStatus(reservation.WaitingForOpponent
                    ? $"Room ready. Code: {reservation.RoomCode}"
                    : "Opponent found. Press Ready.");
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
            if (GamePlayStateManager.Instance != null && GamePlayStateManager.Instance.HasValidDeck)
            {
                return GamePlayStateManager.Instance.GetSelectedDeck().deckId;
            }

            if (!string.IsNullOrWhiteSpace(DeckSelectionScreen.SelectedDeckId))
            {
                return DeckSelectionScreen.SelectedDeckId;
            }

            var decks = await _deckService.GetPlayerDecksAsync();
            if (decks == null || decks.Count == 0)
            {
                throw new InvalidOperationException("No valid decks found for this player.");
            }

            var fallbackDeck = decks[0];
            if (GamePlayStateManager.Instance != null)
            {
                GamePlayStateManager.Instance.SetSelectedDeck(fallbackDeck.deckId, fallbackDeck.cardIds);
            }

            GameLogger.Info("MatchmakingUI", $"No deck explicitly selected, using '{fallbackDeck.displayName}'.");
            return fallbackDeck.deckId;
        }

        private void HandleSnapshotChanged(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _hasActiveMatch = true;
            _currentRoomCode = snapshot.roomCode;
            _localReady = GetSeat(snapshot, snapshot.localSeatIndex)?.ready ?? false;

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
            MatchStateMachine.PlayerDisconnected(RemotePlayerPlaceholderId);
            RefreshButtonVisibility();
        }

        private void SyncMatchState(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var localSeat = GetSeat(snapshot, snapshot.localSeatIndex);
            var remoteSeat = GetSeat(snapshot, snapshot.localSeatIndex == 0 ? 1 : 0);
            var localReady = localSeat?.ready ?? false;
            var remoteReady = remoteSeat?.ready ?? false;
            var remoteConnected = remoteSeat?.connected ?? false;

            if (MatchStateMachine.CurrentMatch == null ||
                !string.Equals(MatchStateMachine.CurrentMatch.matchId, snapshot.matchId, StringComparison.Ordinal))
            {
                MatchStateMachine.InitializeMatch(snapshot.matchId, _authService.CurrentPlayerId, RemotePlayerPlaceholderId);
            }

            MatchStateMachine.SetPlayerReady(_authService.CurrentPlayerId, localReady);
            MatchStateMachine.SetPlayerReady(RemotePlayerPlaceholderId, remoteReady);

            if (snapshot.phase == 2)
            {
                MatchStateMachine.StartMatch();
            }
            else if (snapshot.phase != 0 && !remoteConnected && snapshot.connectedPlayers < 2)
            {
                MatchStateMachine.PlayerDisconnected(RemotePlayerPlaceholderId);
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
        }

        private string BuildStatusText(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Not connected.";
            }

            return snapshot.phase switch
            {
                0 => "Waiting for opponent...",
                1 => _localReady ? "Waiting for the other player..." : "Connected. Press Ready.",
                2 => $"Match in progress. Turn {snapshot.turnNumber}.",
                3 => snapshot.winnerSeatIndex == snapshot.localSeatIndex ? "Victory!" : "Match completed.",
                4 => "Match abandoned.",
                _ => "Connected."
            };
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
            MatchStateMachine.EndMatch();
            RefreshJoinCodeText();
            RefreshReadyButtonText();
            RefreshButtonVisibility();
            SetStatus("Not in session");
        }

        private void RefreshButtonVisibility()
        {
            var inSession = _hasActiveMatch || (MatchSignalRCoordinator.Instance != null && MatchSignalRCoordinator.Instance.IsConnected);

            SetActive(hideWhenInSession, !inSession);
            SetActive(showWhenInSession, inSession);

            if (quickMatchButton != null) quickMatchButton.gameObject.SetActive(!inSession);
            if (createPrivateButton != null) createPrivateButton.gameObject.SetActive(!inSession);
            if (joinByCodeButton != null) joinByCodeButton.gameObject.SetActive(!inSession);
            if (advancedQueueButton != null) advancedQueueButton.gameObject.SetActive(!inSession);
            if (leaveButton != null) leaveButton.gameObject.SetActive(inSession);
            if (readyButton != null) readyButton.gameObject.SetActive(inSession);
            if (copyJoinCodeButton != null) copyJoinCodeButton.gameObject.SetActive(inSession && !string.IsNullOrWhiteSpace(_currentRoomCode));
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
