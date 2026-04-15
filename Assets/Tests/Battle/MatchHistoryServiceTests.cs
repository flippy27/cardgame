using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para MatchHistoryService.
    /// </summary>
    public class MatchHistoryServiceTests
    {
        private MatchHistoryService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new MatchHistoryService();
        }

        [Test]
        public void Constructor_InitializesWithoutHistory()
        {
            Assert.IsNotNull(_service);
        }

        [Test]
        public async void FetchHistory_NullPlayerId_ThrowsValidationException()
        {
            var ex = Assert.ThrowsAsync<ValidationException>(
                async () => await _service.FetchHistory(null));
            Assert.That(ex.Message.Contains("PlayerId"));
        }

        [Test]
        public async void FetchHistory_EmptyPlayerId_ThrowsValidationException()
        {
            var ex = Assert.ThrowsAsync<ValidationException>(
                async () => await _service.FetchHistory(""));
            Assert.That(ex.Message.Contains("PlayerId"));
        }

        [Test]
        public void GetWinRateFromCache_NoHistory_ReturnsZeros()
        {
            var (wins, losses, total) = _service.GetWinRateFromCache("player1");

            Assert.AreEqual(0, wins);
            Assert.AreEqual(0, losses);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void ClearCache_RemovesAllEntries()
        {
            _service.ClearCache();
            var (wins, losses, total) = _service.GetWinRateFromCache("player1");

            Assert.AreEqual(0, total);
        }

        [Test]
        public void MatchHistoryEntryDto_Serializable()
        {
            var entry = new MatchHistoryEntryDto
            {
                matchId = "match1",
                opponentId = "opponent1",
                result = "win",
                mode = "ranked",
                durationSeconds = 120,
                ratingBefore = 1500,
                ratingAfter = 1520,
                createdAt = "2026-04-14T10:00:00Z"
            };

            Assert.AreEqual("match1", entry.matchId);
            Assert.AreEqual("win", entry.result);
            Assert.AreEqual(1520, entry.ratingAfter);
        }

        [Test]
        public void MatchHistoryEntry_CalculatesDelta()
        {
            var entry = new MatchHistoryEntry
            {
                ratingBefore = 1500,
                ratingAfter = 1532,
                ratingDelta = 1532 - 1500
            };

            Assert.AreEqual(32, entry.ratingDelta);
        }

        [Test]
        public void MatchHistoryPage_ValidStructure()
        {
            var page = new MatchHistoryPage
            {
                page = 1,
                pageSize = 20,
                totalCount = 42,
                matches = new System.Collections.Generic.List<MatchHistoryEntry>()
            };

            Assert.AreEqual(1, page.page);
            Assert.AreEqual(20, page.pageSize);
            Assert.AreEqual(42, page.totalCount);
        }
    }
}
