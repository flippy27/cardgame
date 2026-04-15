using System;
using UnityEngine;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Calcula cambios de Elo rating.
    /// SYNC: Matches API.CardDuel.ServerApi.Services.EloRatingService
    /// K=32, floor=100, ceiling=4000.
    /// </summary>
    public sealed class EloRatingService
    {
        private const int K = 32; // K-factor
        private const int RatingFloor = 100;
        private const int RatingCeiling = 4000;

        /// <summary>
        /// Calcula nuevos ratings después de un match.
        /// </summary>
        public static (int newRating1, int newRating2) CalculateEloChange(
            int rating1,
            int rating2,
            bool player1Won)
        {
            // Expected score for player 1
            var expectedScore1 = 1.0 / (1.0 + Math.Pow(10.0, (rating2 - rating1) / 400.0));
            var expectedScore2 = 1.0 - expectedScore1;

            // Actual score
            var actualScore1 = player1Won ? 1.0 : 0.0;
            var actualScore2 = player1Won ? 0.0 : 1.0;

            // Rating change
            var delta1 = (int)Math.Round(K * (actualScore1 - expectedScore1));
            var delta2 = (int)Math.Round(K * (actualScore2 - expectedScore2));

            // Apply and clamp
            var newRating1 = Mathf.Clamp(rating1 + delta1, RatingFloor, RatingCeiling);
            var newRating2 = Mathf.Clamp(rating2 + delta2, RatingFloor, RatingCeiling);

            return (newRating1, newRating2);
        }

        /// <summary>
        /// Calcula solo el delta para un jugador.
        /// </summary>
        public static int CalculateDelta(int playerRating, int opponentRating, bool won)
        {
            var (newRating, _) = CalculateEloChange(playerRating, opponentRating, won);
            return newRating - playerRating;
        }

        /// <summary>
        /// Obtiene la expectativa de victoria (0-1) para el jugador 1.
        /// </summary>
        public static double GetExpectedWinRate(int player1Rating, int player2Rating)
        {
            return 1.0 / (1.0 + Math.Pow(10.0, (player2Rating - player1Rating) / 400.0));
        }
    }
}
