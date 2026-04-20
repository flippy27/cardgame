using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking.Examples
{
    /// <summary>
    /// Example: Complete game flow with SignalR real-time gameplay.
    /// Shows login → matchmaking → SignalR connection → gameplay.
    /// </summary>
    public sealed class GameflowWithSignalRExample : MonoBehaviour
    {
        [SerializeField] private string testEmail = "player1@flippy.com";
        [SerializeField] private string testPassword = "password123";
        [SerializeField] private string testDeckId = "starter-deck";

        private AuthService _authService;
        private MatchmakingService _matchmakingService;
        private MatchSignalRCoordinator _signalRCoordinator;

        public async void StartGameFlow()
        {
            try
            {
                Debug.Log("[GameFlow] Starting complete game flow with SignalR");

                // Step 1: Authenticate
                await AuthenticateAsync();

                // Step 2: Queue for match
                var reservation = await QueueMatchAsync();

                // Step 3: Initialize SignalR coordinator
                InitializeSignalRCoordinator(reservation);

                // Step 4: Connect to match
                await ConnectToMatchAsync(reservation);

                Debug.Log("[GameFlow] Ready for gameplay!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameFlow] Failed: {ex.Message}");
            }
        }

        private async Task AuthenticateAsync()
        {
            Debug.Log("[GameFlow] Authenticating...");

            var baseUrl = ConfigManager.GetApiBaseUrl();
            _authService = new AuthService(baseUrl);

            if (_authService.IsAuthenticated)
            {
                Debug.Log($"[GameFlow] Already authenticated as {_authService.CurrentPlayerId}");
                return;
            }

            bool success = await _authService.Login(testEmail, testPassword);
            if (!success)
            {
                throw new InvalidOperationException("Authentication failed");
            }

            Debug.Log($"[GameFlow] Authenticated as {_authService.CurrentPlayerId}");
        }

        private async Task<MatchReservation> QueueMatchAsync()
        {
            Debug.Log("[GameFlow] Queueing for casual match...");

            var baseUrl = ConfigManager.GetApiBaseUrl();
            var matchmakingClient = new MatchmakingApiClient(baseUrl);
            _matchmakingService = new MatchmakingService(matchmakingClient, _authService);

            var reservation = await _matchmakingService.QueueCasual(testDeckId);

            Debug.Log($"[GameFlow] Got match reservation:");
            Debug.Log($"  - MatchId: {reservation.MatchId}");
            Debug.Log($"  - RoomCode: {reservation.RoomCode}");
            Debug.Log($"  - SeatIndex: {reservation.SeatIndex}");

            return reservation;
        }

        private void InitializeSignalRCoordinator(MatchReservation reservation)
        {
            Debug.Log("[GameFlow] Initializing SignalR coordinator...");

            // Create coordinator
            var coordinatorGo = new GameObject("MatchSignalRCoordinator");
            _signalRCoordinator = coordinatorGo.AddComponent<MatchSignalRCoordinator>();

            // Subscribe to events
            _signalRCoordinator.SnapshotChanged += OnSnapshotChanged;
            _signalRCoordinator.ErrorOccurred += OnError;
            _signalRCoordinator.ConnectionEstablished += OnConnectionEstablished;
            _signalRCoordinator.ConnectionLost += OnConnectionLost;

            Debug.Log("[GameFlow] SignalR coordinator initialized");
        }

        private async Task ConnectToMatchAsync(MatchReservation reservation)
        {
            Debug.Log("[GameFlow] Connecting to match via SignalR...");

            bool connected = await _signalRCoordinator.ConnectAsync(
                reservation.MatchId,
                _authService.CurrentPlayerId,
                reservation.ReconnectToken,
                reservation.SeatIndex
            );

            if (!connected)
            {
                throw new InvalidOperationException("Failed to connect to match");
            }

            Debug.Log("[GameFlow] Connected to match");
        }

        private void OnSnapshotChanged(MatchSnapshot snapshot)
        {
            Debug.Log($"[GameFlow] Snapshot: phase={snapshot.phase}, turn={snapshot.turnNumber}");

            if (snapshot.phase == (int)Core.MatchPhase.WaitingForReady)
            {
                Debug.Log("[GameFlow] Match waiting for ready. Auto-readying...");
                _ = _signalRCoordinator.SetReadyAsync(true);
            }
        }

        private void OnConnectionEstablished()
        {
            Debug.Log("[GameFlow] ✅ SignalR connection established!");
        }

        private void OnConnectionLost()
        {
            Debug.LogWarning("[GameFlow] ⚠️  SignalR connection lost!");
        }

        private void OnError(string message)
        {
            Debug.LogError($"[GameFlow] Error: {message}");
        }

        // Example gameplay commands
        public void ExamplePlayCard(string handKey, int slotIndex)
        {
            if (_signalRCoordinator != null && _signalRCoordinator.IsConnected)
            {
                _ = _signalRCoordinator.PlayCardAsync(handKey, slotIndex);
            }
        }

        public void ExampleEndTurn()
        {
            if (_signalRCoordinator != null && _signalRCoordinator.IsConnected)
            {
                _ = _signalRCoordinator.EndTurnAsync();
            }
        }

        public void ExampleForfeit()
        {
            if (_signalRCoordinator != null && _signalRCoordinator.IsConnected)
            {
                _ = _signalRCoordinator.ForfeitAsync();
            }
        }
    }
}
