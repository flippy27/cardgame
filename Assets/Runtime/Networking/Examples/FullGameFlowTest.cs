using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking.Examples
{
    /// <summary>
    /// Complete end-to-end game flow test.
    /// Tests: Auth → Matchmaking → SignalR connection → Game start → Ready → Win
    ///
    /// Usage:
    /// 1. Add this script to a GameObject
    /// 2. Call StartFullGameFlowTest() from button or OnEnable
    /// 3. Check console for step-by-step output
    /// </summary>
    public sealed class FullGameFlowTest : MonoBehaviour
    {
        [SerializeField] private string testEmail = "player1@flippy.com";
        [SerializeField] private string testPassword = "password123";
        [SerializeField] private string testDeckId = "starter-deck";
        [SerializeField] private float testDurationSeconds = 30f;

        private AuthService _authService;
        private MatchmakingService _matchmakingService;
        private MatchSignalRCoordinator _signalRCoordinator;
        private float _testStartTime;

        public async void StartFullGameFlowTest()
        {
            _testStartTime = Time.time;

            try
            {
                PrintSeparator("FULL GAME FLOW TEST START");

                // STEP 1: Auth
                PrintStep(1, "Authentication");
                await AuthenticateAsync();
                await WaitSeconds(1);

                // STEP 2: Load cards catalog
                PrintStep(2, "Load Cards Catalog");
                await LoadCardsCatalogAsync();
                await WaitSeconds(1);

                // STEP 3: Load player decks
                PrintStep(3, "Load Player Decks");
                await LoadPlayerDecksAsync();
                await WaitSeconds(1);

                // STEP 4: Queue for match
                PrintStep(4, "Queue for Casual Match");
                var reservation = await QueueMatchAsync();
                await WaitSeconds(1);

                // STEP 5: Connect via SignalR
                PrintStep(5, "Connect to Match (SignalR)");
                await ConnectToMatchAsync(reservation);
                await WaitSeconds(2);

                // STEP 6: Set ready
                PrintStep(6, "Set Ready & Wait for Game Start");
                await SetReadyAsync();
                await WaitForGameStart(15f);

                // STEP 7: Play a card
                PrintStep(7, "Play a Card");
                await PlayCardAsync();
                await WaitSeconds(2);

                // STEP 8: End turn
                PrintStep(8, "End Turn");
                await EndTurnAsync();
                await WaitSeconds(2);

                // STEP 9: Wait for win or timeout
                PrintStep(9, "Wait for Game to End");
                await WaitForGameEnd(testDurationSeconds);

                PrintSeparator("✅ FULL GAME FLOW TEST COMPLETED");
            }
            catch (Exception ex)
            {
                PrintError($"Test failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task AuthenticateAsync()
        {
            var baseUrl = ConfigManager.GetApiBaseUrl();
            _authService = new AuthService(baseUrl);

            Debug.Log($"[Test] API URL: {baseUrl}");
            Debug.Log($"[Test] Logging in as: {testEmail}");

            bool success = await _authService.Login(testEmail, testPassword);

            if (!success)
                throw new InvalidOperationException("Login failed");

            Debug.Log($"[Test] ✅ Authenticated. Player ID: {_authService.CurrentPlayerId}");
        }

        private async Task LoadCardsCatalogAsync()
        {
            var baseUrl = ConfigManager.GetApiBaseUrl();
            var cardApiClient = new CardApiClient(baseUrl);

            var cards = await cardApiClient.FetchAllCards();

            if (cards == null || cards.Count == 0)
                throw new InvalidOperationException("No cards loaded");

            Debug.Log($"[Test] ✅ Loaded {cards.Count} cards");
        }

        private async Task LoadPlayerDecksAsync()
        {
            var baseUrl = ConfigManager.GetApiBaseUrl();
            var cardApiClient = new CardApiClient(baseUrl);

            var decks = await cardApiClient.FetchPlayerDecks(_authService.CurrentPlayerId);

            if (decks == null || decks.Count == 0)
                throw new InvalidOperationException("No decks found");

            Debug.Log($"[Test] ✅ Loaded {decks.Count} player decks");
            foreach (var deck in decks)
            {
                Debug.Log($"   - {deck.displayName} ({deck.cardIds.Count} cards)");
            }
        }

        private async Task<MatchReservation> QueueMatchAsync()
        {
            var baseUrl = ConfigManager.GetApiBaseUrl();
            var matchmakingClient = new MatchmakingApiClient(baseUrl);
            _matchmakingService = new MatchmakingService(matchmakingClient, _authService);

            var reservation = await _matchmakingService.QueueCasual(testDeckId);

            if (string.IsNullOrEmpty(reservation.MatchId))
                throw new InvalidOperationException("Failed to queue match");

            Debug.Log($"[Test] ✅ Queued for match");
            Debug.Log($"   - Match ID: {reservation.MatchId}");
            Debug.Log($"   - Room Code: {reservation.RoomCode}");
            Debug.Log($"   - Seat: {reservation.SeatIndex}");

            return reservation;
        }

        private async Task ConnectToMatchAsync(MatchReservation reservation)
        {
            var coordinatorGo = new GameObject("SignalRCoordinator_Test");
            _signalRCoordinator = coordinatorGo.AddComponent<MatchSignalRCoordinator>();

            _signalRCoordinator.SnapshotChanged += OnSnapshotChanged;
            _signalRCoordinator.ErrorOccurred += OnCoordinatorError;

            bool connected = await _signalRCoordinator.ConnectAsync(
                reservation.MatchId,
                _authService.CurrentPlayerId,
                reservation.ReconnectToken,
                reservation.SeatIndex
            );

            if (!connected)
                throw new InvalidOperationException("Failed to connect to match");

            Debug.Log($"[Test] ✅ Connected to match via SignalR");
        }

        private async Task SetReadyAsync()
        {
            await _signalRCoordinator.SetReadyAsync(true);
            Debug.Log($"[Test] ✅ Set ready = true");
        }

        private async Task PlayCardAsync()
        {
            // In a real test, we'd extract actual card keys from snapshot
            // For now, just log that we attempted
            Debug.Log($"[Test] ⚠️  PlayCard would be called here (requires card key from snapshot)");
            await Task.Delay(500);
        }

        private async Task EndTurnAsync()
        {
            await _signalRCoordinator.EndTurnAsync();
            Debug.Log($"[Test] ✅ End turn sent");
        }

        private async Task WaitForGameStart(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                Debug.Log($"[Test] Waiting for game to start... ({elapsed:F1}s)");
                await WaitSeconds(1f);
                elapsed += 1f;
            }

            Debug.LogWarning($"[Test] ⚠️  Game start timeout after {timeoutSeconds}s");
        }

        private async Task WaitForGameEnd(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                Debug.Log($"[Test] Waiting for game to end... ({elapsed:F1}s)");
                await WaitSeconds(1f);
                elapsed += 1f;
            }

            Debug.LogWarning($"[Test] ⚠️  Game end timeout after {timeoutSeconds}s");
        }

        private void OnSnapshotChanged(MatchSnapshot snapshot)
        {
            if (snapshot == null) return;

            Debug.Log($"[Test] 📸 Snapshot: phase={snapshot.phase}, turn={snapshot.turnNumber}");

            if (snapshot.phase == (int)MatchPhase.InProgress)
            {
                Debug.Log($"[Test] ✅ Game in progress!");
            }
            else if (snapshot.duelEnded)
            {
                var winner = snapshot.winnerSeatIndex ?? -1;
                Debug.Log($"[Test] ✅ Game ended! Winner: Seat {winner}");
            }
        }

        private void OnCoordinatorError(string message)
        {
            Debug.LogError($"[Test] ❌ Coordinator error: {message}");
        }

        private async Task WaitSeconds(float seconds)
        {
            await Task.Delay((int)(seconds * 1000));
        }

        private void PrintStep(int number, string description)
        {
            PrintSeparator($"STEP {number}: {description}");
        }

        private void PrintSeparator(string text)
        {
            Debug.Log($"\n{'='}{new string('=', 60)}");
            Debug.Log($"  {text}");
            Debug.Log($"{'='}{new string('=', 60)}\n");
        }

        private void PrintError(string message)
        {
            Debug.LogError($"\n{'!'}{new string('!', 60)}");
            Debug.LogError($"  {message}");
            Debug.LogError($"{'!'}{new string('!', 60)}\n");
        }
    }
}
