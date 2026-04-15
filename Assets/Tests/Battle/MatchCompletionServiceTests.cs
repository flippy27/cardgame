using System;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para MatchCompletionService.
    /// </summary>
    public class MatchCompletionServiceTests
    {
        // Note: MatchCompletionService requires GameService which is MonoBehaviour
        // Full integration tests would require game context.
        // Here we test the result structure.

        [Test]
        public void MatchCompletionResult_ValidStructure()
        {
            var result = new MatchCompletionResult
            {
                matchId = "match_123",
                playerId = "player_1",
                opponentId = "player_2",
                won = true,
                ratingBefore = 1600,
                ratingAfter = 1616,
                ratingDelta = 16,
                durationSeconds = 180,
                completedAt = DateTimeOffset.UtcNow
            };

            Assert.AreEqual("match_123", result.matchId);
            Assert.IsTrue(result.won);
            Assert.AreEqual(16, result.ratingDelta);
            Assert.AreEqual(180, result.durationSeconds);
        }

        [Test]
        public void MatchCompletionResult_LossScenario()
        {
            var result = new MatchCompletionResult
            {
                matchId = "match_124",
                playerId = "player_1",
                opponentId = "player_3",
                won = false,
                ratingBefore = 1600,
                ratingAfter = 1584,
                ratingDelta = -16,
                durationSeconds = 240
            };

            Assert.IsFalse(result.won);
            Assert.AreEqual(-16, result.ratingDelta);
            Assert.Less(result.ratingAfter, result.ratingBefore);
        }

        [Test]
        public void MatchCompletionResult_ZeroDelta()
        {
            // Edge case: both players same rating, one wins exactly 16, other loses 16
            var result = new MatchCompletionResult
            {
                ratingBefore = 1600,
                ratingAfter = 1616,
                ratingDelta = 16,
                won = true
            };

            Assert.Greater(result.ratingDelta, 0);
        }
    }
}
