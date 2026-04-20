using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking.Examples
{
    /// <summary>
    /// Example: Queue for match and start HTTP-based game.
    /// Shows complete flow from authentication to gameplay.
    /// </summary>
    public sealed class HttpMatchIntegrationExample : MonoBehaviour
    {
        [SerializeField] private string testEmail = "player1@example.com";
        [SerializeField] private string testPassword = "password123";
        [SerializeField] private string testDeckId = "starter-deck";

        private AuthService _authService;
        private MatchmakingService _matchmakingService;
        private MatchHttpCoordinator _coordinator;

        public async void StartIntegration()
        {
            try
            {
                Debug.Log("[Integration] Starting HTTP match integration example");

                // Step 1: Authenticate
                await AuthenticateAsync();

                // Step 2: Queue for match
                var reservation = await QueueMatchAsync();

                // Step 3: Initialize coordinator
                InitializeCoordinator(reservation);

                // Step 4: Load battle scene (or use existing)
                Debug.Log("[Integration] Ready for gameplay!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Integration] Failed: {ex.Message}");
            }
        }

        private async Task AuthenticateAsync()
        {
            Debug.Log("[Integration] Authenticating...");

            var baseUrl = ConfigManager.GetApiBaseUrl();
            _authService = new AuthService(baseUrl);

            // Check if already authenticated
            if (_authService.IsAuthenticated)
            {
                Debug.Log($"[Integration] Already authenticated as {_authService.CurrentPlayerId}");
                return;
            }

            // Login
            bool success = await _authService.Login(testEmail, testPassword);
            if (!success)
            {
                throw new InvalidOperationException("Authentication failed");
            }

            Debug.Log($"[Integration] Authenticated as {_authService.CurrentPlayerId}");
        }

        private async Task<MatchReservation> QueueMatchAsync()
        {
            Debug.Log("[Integration] Queueing for casual match...");

            var baseUrl = ConfigManager.GetApiBaseUrl();
            var matchmakingClient = new MatchmakingApiClient(baseUrl);
            _matchmakingService = new MatchmakingService(matchmakingClient, _authService);

            var reservation = await _matchmakingService.QueueCasual(testDeckId);

            Debug.Log($"[Integration] Got match reservation:");
            Debug.Log($"  - MatchId: {reservation.MatchId}");
            Debug.Log($"  - RoomCode: {reservation.RoomCode}");
            Debug.Log($"  - SeatIndex: {reservation.SeatIndex}");
            Debug.Log($"  - Status: {reservation.Status}");

            return reservation;
        }

        private void InitializeCoordinator(MatchReservation reservation)
        {
            Debug.Log("[Integration] Initializing HTTP coordinator...");

            // Create coordinator
            var coordinatorGo = new GameObject("MatchHttpCoordinator");
            _coordinator = coordinatorGo.AddComponent<MatchHttpCoordinator>();

            // Subscribe to events
            _coordinator.SnapshotChanged += OnSnapshotChanged;
            _coordinator.ErrorOccurred += OnError;

            // Initialize with match details
            _coordinator.Initialize(
                reservation.MatchId,
                _authService.CurrentPlayerId,
                reservation.SeatIndex
            );

            Debug.Log("[Integration] HTTP coordinator initialized");
        }

        private void OnSnapshotChanged(MatchSnapshot snapshot)
        {
            Debug.Log($"[Integration] Snapshot: phase={snapshot.phase}, turn={snapshot.turnNumber}, active={snapshot.activeSeatIndex}");

            if (snapshot.phase == 1) // WaitingForReady
            {
                Debug.Log("[Integration] Match waiting for ready. Auto-readying...");
                _coordinator.RequestSetReady(true);
            }
        }

        private void OnError(string message)
        {
            Debug.LogError($"[Integration] Error: {message}");
        }

        // Example gameplay commands that would be called from UI
        public void ExamplePlayCard(string handKey, int slotIndex)
        {
            if (_coordinator != null)
            {
                _coordinator.RequestPlayCard(handKey, slotIndex);
            }
        }

        public void ExampleEndTurn()
        {
            if (_coordinator != null)
            {
                _coordinator.RequestEndTurn();
            }
        }

        public void ExampleForfeit()
        {
            if (_coordinator != null)
            {
                _coordinator.RequestForfeit();
            }
        }
    }
}
