using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Maneja la conclusión de un match:
    /// - Cálculo de Elo
    /// - Envío de resultado a servidor
    /// - Actualización de datos locales
    /// </summary>
    public sealed class MatchCompletionService
    {
        private readonly GameService _gameService;

        public MatchCompletionService(GameService gameService)
        {
            _gameService = gameService ?? throw new ValidationException("GameService required.");
        }

        /// <summary>
        /// Completa un match y actualiza ratings.
        /// </summary>
        public async Task<MatchCompletionResult> CompleteMatch(
            string matchId,
            string playerId,
            string opponentId,
            int playerRatingBefore,
            int opponentRatingBefore,
            bool playerWon,
            int durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ValidationException("MatchId required.");

            try
            {
                // Calcular nuevos ratings
                var (playerRatingAfter, opponentRatingAfter) = EloRatingService.CalculateEloChange(
                    playerRatingBefore,
                    opponentRatingBefore,
                    playerWon);

                var result = new MatchCompletionResult
                {
                    matchId = matchId,
                    playerId = playerId,
                    opponentId = opponentId,
                    won = playerWon,
                    ratingBefore = playerRatingBefore,
                    ratingAfter = playerRatingAfter,
                    ratingDelta = playerRatingAfter - playerRatingBefore,
                    durationSeconds = durationSeconds,
                    completedAt = DateTimeOffset.UtcNow
                };

                // Enviar resultado a servidor
                var apiClient = new CardGameApiClient();
                var response = await apiClient.CompleteMatchAsync(
                    matchId,
                    playerId,
                    opponentId,
                    playerWon,
                    durationSeconds);

                Debug.Log($"[MatchCompletion] Match {matchId}: {(playerWon ? "WIN" : "LOSS")} | " +
                         $"Rating {playerRatingBefore} → {playerRatingAfter} ({(result.ratingDelta > 0 ? "+" : "")}{result.ratingDelta}) | " +
                         $"API Response: {response}");

                // Invalidar cache de match history (datos desactualizados)
                _gameService.MatchHistory.ClearCache();

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchCompletion] Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene cambio estimado de rating vs oponente.
        /// </summary>
        public (int winDelta, int lossDelta) GetExpectedRatingChange(
            int playerRating,
            int opponentRating)
        {
            var winDelta = EloRatingService.CalculateDelta(playerRating, opponentRating, won: true);
            var lossDelta = EloRatingService.CalculateDelta(playerRating, opponentRating, won: false);

            return (winDelta, lossDelta);
        }

        /// <summary>
        /// Obtiene probabilidad de victoria estimada.
        /// </summary>
        public double GetExpectedWinProbability(int playerRating, int opponentRating)
        {
            return EloRatingService.GetExpectedWinRate(playerRating, opponentRating);
        }
    }

    public sealed class MatchCompletionResult
    {
        public string matchId;
        public string playerId;
        public string opponentId;
        public bool won;
        public int ratingBefore;
        public int ratingAfter;
        public int ratingDelta;
        public int durationSeconds;
        public DateTimeOffset completedAt;
    }
}
