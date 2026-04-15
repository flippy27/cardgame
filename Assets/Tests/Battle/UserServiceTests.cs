using NUnit.Framework;
using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Tests
{
    public class UserServiceTests
    {
        private UserService _userService;
        private CardGameApiClient _apiClient;
        private AuthService _authService;

        [SetUp]
        public void Setup()
        {
            _apiClient = new CardGameApiClient("http://localhost:5000");
            _authService = new AuthService("http://localhost:5000");
            _userService = new UserService(_apiClient, _authService);
        }

        [Test]
        public void GetProfile_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _userService.GetProfile("test");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void GetStats_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _userService.GetStats("test");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void UpdateProfile_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();
            var profile = new PlayerProfileDto { PlayerId = "test" };

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _userService.UpdateProfile(profile);
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void GetAchievements_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _userService.GetAchievements("test");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void UnlockAchievement_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _userService.UnlockAchievement("achievement1");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void PlayerProfileDto_Serializable()
        {
            // Arrange
            var profile = new PlayerProfileDto
            {
                PlayerId = "player1",
                DisplayName = "TestPlayer",
                Level = 10,
                Experience = 5000,
                FactionPreference = "Fire",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastSeenAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsPremium = false
            };

            // Act
            var json = JsonUtility.ToJson(profile);
            var deserialized = JsonUtility.FromJson<PlayerProfileDto>(json);

            // Assert
            Assert.AreEqual(profile.PlayerId, deserialized.PlayerId);
            Assert.AreEqual(profile.DisplayName, deserialized.DisplayName);
            Assert.AreEqual(profile.Level, deserialized.Level);
            Assert.AreEqual(profile.IsPremium, deserialized.IsPremium);
        }

        [Test]
        public void PlayerStatsDto_Serializable()
        {
            // Arrange
            var stats = new PlayerStatsDto
            {
                PlayerId = "player1",
                CurrentRating = 1500,
                HighestRating = 1800,
                TotalMatches = 50,
                Wins = 30,
                Losses = 20,
                WinRate = 0.6f,
                RankedRating = 1400,
                RankedDivision = 3,
                CasualRating = 1600
            };

            // Act
            var json = JsonUtility.ToJson(stats);
            var deserialized = JsonUtility.FromJson<PlayerStatsDto>(json);

            // Assert
            Assert.AreEqual(stats.PlayerId, deserialized.PlayerId);
            Assert.AreEqual(stats.CurrentRating, deserialized.CurrentRating);
            Assert.AreEqual(stats.Wins, deserialized.Wins);
            Assert.AreEqual(stats.WinRate, deserialized.WinRate);
        }

        [Test]
        public void AchievementDto_Serializable()
        {
            // Arrange
            var achievement = new AchievementDto
            {
                AchievementId = "ach1",
                Title = "First Win",
                Description = "Win your first match",
                RewardExp = 100,
                IsUnlocked = true,
                UnlockedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Act
            var json = JsonUtility.ToJson(achievement);
            var deserialized = JsonUtility.FromJson<AchievementDto>(json);

            // Assert
            Assert.AreEqual(achievement.AchievementId, deserialized.AchievementId);
            Assert.AreEqual(achievement.Title, deserialized.Title);
            Assert.AreEqual(achievement.IsUnlocked, deserialized.IsUnlocked);
        }
    }
}
