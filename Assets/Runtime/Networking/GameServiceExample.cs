using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Ejemplo de cómo usar GameService en un flujo real.
    /// Este script no debe estar en producción; es referencia pedagógica.
    ///
    /// FLUJO TÍPICO:
    /// 1. Bootstrap (descargar catálogo)
    /// 2. Login (obtener token JWT)
    /// 3. Validar deck
    /// 4. Crear/unirse a match
    /// 5. Completar match (actualizar ratings)
    /// 6. Ver historial
    /// </summary>
    public sealed class GameServiceExample : MonoBehaviour
    {
        private GameService _gameService;

        private async void Start()
        {
            _gameService = GameService.Instance;
            if (_gameService == null)
            {
                Debug.LogError("GameService not found");
                return;
            }

            // Ejemplo: Bootstrap
            await ExampleBootstrap();

            // Ejemplo: Login
            await ExampleLogin();

            // Ejemplo: Validar deck
            ExampleValidateDeck();

            // Ejemplo: Match completion
            await ExampleMatchCompletion();

            // Ejemplo: Match history
            await ExampleMatchHistory();

            // Ejemplo: User profile
            await ExampleUserProfile();

            // Ejemplo: Deck management
            await ExampleDeckManagement();

            // Ejemplo: Matchmaking
            await ExampleMatchmaking();

            // Ejemplo: Local cache & offline
            ExampleLocalCache();
        }

        private async Task ExampleBootstrap()
        {
            Debug.Log("=== EXAMPLE: Bootstrap ===");

            var success = await _gameService.Bootstrap();
            if (!success)
            {
                Debug.LogError("Bootstrap failed");
                return;
            }

            var stats = _gameService.GetCardStats();
            Debug.Log($"Catalog loaded: {stats.totalCards} cards");
        }

        private async Task ExampleLogin()
        {
            Debug.Log("=== EXAMPLE: Login ===");

            var success = await _gameService.Login("test_player", "test_password");
            if (!success)
            {
                Debug.LogError("Login failed");
                return;
            }

            Debug.Log($"Logged in as {_gameService.AuthService.CurrentPlayerId}");
        }

        private void ExampleValidateDeck()
        {
            Debug.Log("=== EXAMPLE: Validate Deck ===");

            var cardIds = new[] { "card1", "card2", "card3", "card4", "card5" };
            var result = _gameService.ValidateDeck(cardIds);

            if (result.IsValid)
            {
                Debug.Log("Deck is valid!");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    Debug.LogError(error);
                }
            }
        }

        private async Task ExampleMatchCompletion()
        {
            Debug.Log("=== EXAMPLE: Match Completion ===");

            if (!_gameService.IsAuthenticated)
            {
                Debug.LogWarning("Not authenticated, skipping match example");
                return;
            }

            var completionService = new MatchCompletionService(_gameService);

            // Ejemplo: jugador gana con ratings iguales
            var result = await completionService.CompleteMatch(
                matchId: "match_example_001",
                playerId: _gameService.AuthService.CurrentPlayerId,
                opponentId: "opponent_123",
                playerRatingBefore: 1600,
                opponentRatingBefore: 1600,
                playerWon: true,
                durationSeconds: 180);

            Debug.Log($"Match completed!");
            Debug.Log($"  Before: {result.ratingBefore}");
            Debug.Log($"  After:  {result.ratingAfter}");
            Debug.Log($"  Delta:  {(result.ratingDelta > 0 ? "+" : "")}{result.ratingDelta}");
            Debug.Log($"  Duration: {result.durationSeconds}s");

            // Ejemplo: estimar cambio vs oponente más fuerte
            var (winDelta, lossDelta) = completionService.GetExpectedRatingChange(1600, 1800);
            Debug.Log($"vs 1800 rated opponent: Win +{winDelta}, Loss {lossDelta}");

            // Ejemplo: probabilidad de victoria
            var winProbability = completionService.GetExpectedWinProbability(1600, 1800);
            Debug.Log($"Win probability: {winProbability:P1}");
        }

        private async Task ExampleMatchHistory()
        {
            Debug.Log("=== EXAMPLE: Match History ===");

            if (!_gameService.IsAuthenticated)
            {
                Debug.LogWarning("Not authenticated, skipping history example");
                return;
            }

            try
            {
                var page = await _gameService.LoadMatchHistory(page: 1, pageSize: 5);

                Debug.Log($"Match history (page {page.page}/{(page.totalCount + 19) / 20}):");
                foreach (var match in page.matches)
                {
                    var ratingChange = match.ratingAfter - match.ratingBefore;
                    var ratingStr = ratingChange > 0 ? $"+{ratingChange}" : ratingChange.ToString();
                    Debug.Log($"  {match.opponentId}: {match.result} | Rating {match.ratingBefore}→{match.ratingAfter} ({ratingStr})");
                }

                var (wins, losses, total) = _gameService.MatchHistory.GetWinRateFromCache(
                    _gameService.AuthService.CurrentPlayerId);
                var winRate = total > 0 ? (wins * 100.0 / total) : 0.0;
                Debug.Log($"Win rate: {wins}W-{losses}L ({winRate:F1}%)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Match history error: {ex.Message}");
            }
        }

        private async Task ExampleUserProfile()
        {
            Debug.Log("=== EXAMPLE: User Profile & Stats ===");

            if (!_gameService.IsAuthenticated)
            {
                Debug.LogWarning("Not authenticated, skipping profile example");
                return;
            }

            try
            {
                var profile = await _gameService.UserService.GetProfile();
                Debug.Log($"Profile: {profile.DisplayName} (Level {profile.Level})");
                Debug.Log($"  Faction: {profile.FactionPreference}");
                Debug.Log($"  XP: {profile.Experience}");

                var stats = await _gameService.UserService.GetStats();
                Debug.Log($"Stats: {stats.Wins}W-{stats.Losses}L ({stats.WinRate:P})");
                Debug.Log($"  Current Rating: {stats.CurrentRating}");
                Debug.Log($"  Highest Rating: {stats.HighestRating}");
                Debug.Log($"  Ranked Division: {stats.RankedDivision}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Profile error: {ex.Message}");
            }
        }

        private async Task ExampleDeckManagement()
        {
            Debug.Log("=== EXAMPLE: Deck Management ===");

            if (!_gameService.IsAuthenticated)
            {
                Debug.LogWarning("Not authenticated, skipping deck example");
                return;
            }

            try
            {
                // Cargar mazos existentes
                var decks = await _gameService.DeckService.LoadDecks();
                Debug.Log($"Your decks: {decks.Count}");
                foreach (var deck in decks)
                {
                    Debug.Log($"  - {deck.Name}: {deck.CardIds.Length} cards ({deck.WinRate}% WR)");
                }

                // Crear nuevo mazo (si las cartas son válidas)
                try
                {
                    var newDeck = await _gameService.DeckService.CreateDeck(
                        name: "Aggro Fire",
                        description: "Fast burn deck",
                        cardIds: new[] { "card1", "card2", "card1", "card2" });
                    Debug.Log($"Created deck: {newDeck.DeckId}");
                }
                catch (Core.ValidationException ex)
                {
                    Debug.LogWarning($"Cannot create deck: {ex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Deck error: {ex.Message}");
            }
        }

        private async Task ExampleMatchmaking()
        {
            Debug.Log("=== EXAMPLE: Matchmaking ===");

            if (!_gameService.IsAuthenticated)
            {
                Debug.LogWarning("Not authenticated, skipping matchmaking example");
                return;
            }

            try
            {
                // Unirse a cola casual (sin esperar realmente)
                var joined = await _gameService.Matchmaking.JoinQueue(MatchmakingService.QueueMode.Casual);
                if (joined)
                {
                    Debug.Log("Joined casual queue");

                    // Obtener estado
                    var status = await _gameService.Matchmaking.GetStatus();
                    Debug.Log($"Queue status: {status.PlayersInQueue} players, ~{status.EstimatedWaitSeconds}s wait");

                    // Dejar la cola
                    await _gameService.Matchmaking.LeaveQueue();
                    Debug.Log("Left queue");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Matchmaking error: {ex.Message}");
            }
        }

        private void ExampleLocalCache()
        {
            Debug.Log("=== EXAMPLE: Local Cache & Offline Support ===");

            // Guardar datos en caché local
            var playerData = new PlayerProfileDto
            {
                PlayerId = "test_player",
                DisplayName = "CachedPlayer",
                Level = 10,
                IsPremium = false
            };

            _gameService.LocalCache.Set("player_profile", playerData, expiryHours: 24);
            Debug.Log("Cached player profile");

            // Recuperar del caché
            var cached = _gameService.LocalCache.Get<PlayerProfileDto>("player_profile");
            if (cached != null)
            {
                Debug.Log($"Retrieved from cache: {cached.DisplayName} (Level {cached.Level})");
            }

            // Marcar cambios pendientes (offline mode)
            _gameService.OfflineSync.MarkPending("deck_update_1", "update_data");
            _gameService.OfflineSync.MarkPending("deck_update_2", "update_data");
            Debug.Log($"Pending changes: {_gameService.OfflineSync.PendingChanges}");

            // Simular offline -> online
            _gameService.OfflineSync.SetOnlineStatus(false);
            Debug.Log($"Going offline (synced: {_gameService.OfflineSync.PendingChanges} pending)");

            _gameService.OfflineSync.SetOnlineStatus(true);
            Debug.Log("Back online - would sync pending changes here");

            // Estadísticas de caché
            var (total, expired) = _gameService.LocalCache.GetStats();
            Debug.Log($"Cache stats: {total} keys, {expired} expired");
        }
    }
}
