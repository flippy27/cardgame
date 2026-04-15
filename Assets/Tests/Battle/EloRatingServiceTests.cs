using Flippy.CardDuelMobile.Networking;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para EloRatingService.
    /// Valida: cálculos Elo, expected win rates, deltas.
    /// </summary>
    public class EloRatingServiceTests
    {
        [Test]
        public void CalculateEloChange_EqualRatings_Winner_GainsApproximately16()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(1600, 1600, player1Won: true);

            Assert.AreEqual(1616, new1);
            Assert.AreEqual(1584, new2);
        }

        [Test]
        public void CalculateEloChange_EqualRatings_Loser_LosesApproximately16()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(1600, 1600, player1Won: false);

            Assert.AreEqual(1584, new1);
            Assert.AreEqual(1616, new2);
        }

        [Test]
        public void CalculateEloChange_HigherRated_Wins_GainsLess()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(1800, 1600, player1Won: true);

            // Higher rated wins = less points
            Assert.Less(new1 - 1800, 16); // Expected to gain less than 16
            Assert.Greater(1800 - new2, 16); // Expected opponent to lose more than 16
        }

        [Test]
        public void CalculateEloChange_LowerRated_Wins_GainsMore()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(1400, 1800, player1Won: true);

            // Lower rated wins = more points
            Assert.Greater(new1 - 1400, 16); // Expected to gain more than 16
            Assert.Less(1800 - new2, 16); // Expected opponent to lose less than 16
        }

        [Test]
        public void CalculateEloChange_Clamped_ToFloor()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(100, 2000, player1Won: false);

            // Should not go below 100
            Assert.GreaterOrEqual(new1, 100);
        }

        [Test]
        public void CalculateEloChange_Clamped_ToCeiling()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(4000, 100, player1Won: true);

            // Should not go above 4000
            Assert.LessOrEqual(new1, 4000);
        }

        [Test]
        public void CalculateDelta_Winner_Positive()
        {
            var delta = EloRatingService.CalculateDelta(1600, 1600, won: true);

            Assert.Greater(delta, 0);
            Assert.AreEqual(16, delta);
        }

        [Test]
        public void CalculateDelta_Loser_Negative()
        {
            var delta = EloRatingService.CalculateDelta(1600, 1600, won: false);

            Assert.Less(delta, 0);
            Assert.AreEqual(-16, delta);
        }

        [Test]
        public void GetExpectedWinRate_EqualRatings_Approximately50Percent()
        {
            var expected = EloRatingService.GetExpectedWinRate(1600, 1600);

            Assert.AreEqual(0.5, expected, 0.01);
        }

        [Test]
        public void GetExpectedWinRate_HigherRated_GreaterThan50Percent()
        {
            var expected = EloRatingService.GetExpectedWinRate(1800, 1600);

            Assert.Greater(expected, 0.5);
            Assert.Less(expected, 1.0);
        }

        [Test]
        public void GetExpectedWinRate_LowerRated_LessThan50Percent()
        {
            var expected = EloRatingService.GetExpectedWinRate(1400, 1600);

            Assert.Less(expected, 0.5);
            Assert.Greater(expected, 0.0);
        }

        [Test]
        public void GetExpectedWinRate_BigRatingGap_Extreme()
        {
            var expectedHigh = EloRatingService.GetExpectedWinRate(2400, 1200);
            var expectedLow = EloRatingService.GetExpectedWinRate(1200, 2400);

            Assert.Greater(expectedHigh, 0.95);
            Assert.Less(expectedLow, 0.05);
        }

        [Test]
        public void ZeroSumProperty_TotalRatingChangeIsZero()
        {
            var (new1, new2) = EloRatingService.CalculateEloChange(1600, 1600, player1Won: true);

            // Without clamping, sum should be zero (zero-sum game)
            var delta1 = new1 - 1600;
            var delta2 = new2 - 1600;

            Assert.AreEqual(delta1, -delta2);
        }
    }
}
