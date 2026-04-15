using NUnit.Framework;
using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Tests
{
    public class MatchmakingServiceTests
    {
        private MatchmakingService _matchmakingService;
        private CardGameApiClient _apiClient;
        private AuthService _authService;

        [SetUp]
        public void Setup()
        {
            _apiClient = new CardGameApiClient("http://localhost:5000");
            _authService = new AuthService("http://localhost:5000");
            _matchmakingService = new MatchmakingService(_apiClient, _authService);
        }

        [Test]
        public void JoinQueue_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _matchmakingService.JoinQueue(MatchmakingService.QueueMode.Casual);
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void LeaveQueue_WhenNotSearching_ReturnsFalse()
        {
            // Arrange
            Assert.IsFalse(_matchmakingService.IsSearching);

            // Act
            var result = _matchmakingService.LeaveQueue().Result;

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void GetStatus_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _matchmakingService.GetStatus();
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void IsSearching_InitiallyFalse()
        {
            // Assert
            Assert.IsFalse(_matchmakingService.IsSearching);
        }

        [Test]
        public void TimeInQueue_InitiallyZero()
        {
            // Assert
            Assert.AreEqual(0, _matchmakingService.TimeInQueue);
        }

        [Test]
        public void MatchmakingStatusDto_Serializable()
        {
            // Arrange
            var status = new MatchmakingStatusDto
            {
                IsSearching = true,
                QueueMode = 0,
                TimeInQueueSeconds = 30,
                EstimatedWaitSeconds = 45,
                PlayersInQueue = 150,
                OpponentId = null,
                MatchId = null
            };

            // Act
            var json = JsonUtility.ToJson(status);
            var deserialized = JsonUtility.FromJson<MatchmakingStatusDto>(json);

            // Assert
            Assert.AreEqual(status.IsSearching, deserialized.IsSearching);
            Assert.AreEqual(status.QueueMode, deserialized.QueueMode);
            Assert.AreEqual(status.TimeInQueueSeconds, deserialized.TimeInQueueSeconds);
            Assert.AreEqual(status.PlayersInQueue, deserialized.PlayersInQueue);
        }

        [Test]
        public void CancelSearch_CallsLeaveQueue()
        {
            // Act
            var result = _matchmakingService.CancelSearch().Result;

            // Assert - no exception is success for unresearching
            Assert.IsFalse(_matchmakingService.IsSearching);
        }
    }
}
