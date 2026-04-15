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
    }
}
