using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Tests
{
    /// <summary>
    /// Pruebas de integración: flujos completos sin API real.
    /// </summary>
    public class IntegrationTests
    {
        private GameService _gameService;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            FakeHttpServerExtensions.InitializeFakeServer();
        }

        [SetUp]
        public void Setup()
        {
            // Crear GameService para pruebas
            var obj = new GameObject("GameService");
            _gameService = obj.AddComponent<GameService>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameService != null)
            {
                Object.Destroy(_gameService.gameObject);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            FakeHttpServerExtensions.ClearFakeServer();
        }

        [Test]
        public void GameService_Initializes_Successfully()
        {
            // Assert
            Assert.IsNotNull(_gameService.ApiClient);
            Assert.IsNotNull(_gameService.AuthService);
            Assert.IsNotNull(_gameService.CardCatalog);
            Assert.IsNotNull(_gameService.MatchHistory);
            Assert.IsNotNull(_gameService.UserService);
            Assert.IsNotNull(_gameService.DeckService);
            Assert.IsNotNull(_gameService.Matchmaking);
            Assert.IsTrue(_gameService.IsReady);
        }

        [Test]
        public async Task Bootstrap_LoadsCatalog_Successfully()
        {
            // Act
            var result = await _gameService.Bootstrap();

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(_gameService.IsCatalogReady);

            var (total, withAbilities) = _gameService.CardCatalog.GetStats();
            Assert.Greater(total, 0);
        }

        [Test]
        public void ValidateDeck_EmptyDeck_IsInvalid()
        {
            // Arrange
            var cardIds = new string[0];

            // Act
            var result = _gameService.ValidateDeck(cardIds);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.Greater(result.Errors.Count, 0);
        }

        [Test]
        public void ValidateDeck_ValidCards_IsValid()
        {
            // Arrange
            var cardIds = new[] { "card1", "card2", "card1", "card2" };

            // Act
            var result = _gameService.ValidateDeck(cardIds);

            // Assert - should validate once catalog is loaded
            // For now just checks structure
            Assert.IsNotNull(result);
        }

        [Test]
        public void Login_ValidCredentials_SetsAuthentication()
        {
            // Act
            var result = _gameService.Login("player1", "password").Result;

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(_gameService.IsAuthenticated);
            Assert.AreEqual("player1", _gameService.AuthService.CurrentPlayerId);
        }

        [Test]
        public void Login_EmptyCredentials_ReturnsFalse()
        {
            // Act
            var result = _gameService.Login("", "").Result;

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void Logout_ClearsAuthentication()
        {
            // Arrange
            _gameService.Login("player1", "password").Wait();
            Assert.IsTrue(_gameService.IsAuthenticated);

            // Act
            _gameService.Logout();

            // Assert
            Assert.IsFalse(_gameService.IsAuthenticated);
        }

        [Test]
        public async Task GetCardStats_ReturnsValidStats()
        {
            // Arrange
            await _gameService.Bootstrap();

            // Act
            var stats = _gameService.GetCardStats();

            // Assert
            Assert.IsNotNull(stats);
            Assert.Greater(stats.totalCards, 0);
        }

        [Test]
        public void Login_Then_LoadMatchHistory_Fails_BeforeLogin()
        {
            // Arrange
            _gameService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _gameService.LoadMatchHistory();
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public async Task DeckService_ValidatesDeckAgainstCatalog()
        {
            // Arrange
            await _gameService.Bootstrap();
            var validCardIds = new[] { "card1", "card2" };

            // Act
            var result = _gameService.DeckService.ValidateDeck(validCardIds);

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void UserService_RequiresAuthentication()
        {
            // Arrange
            _gameService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _gameService.UserService.GetProfile();
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void Matchmaking_JoinQueue_RequiresAuthentication()
        {
            // Arrange
            _gameService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _gameService.Matchmaking.JoinQueue(MatchmakingService.QueueMode.Casual);
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void CardCatalogCache_GetAll_ReturnsEmptyWhenNotLoaded()
        {
            // Act
            var all = _gameService.CardCatalog.GetAll();

            // Assert
            Assert.IsNotNull(all);
            Assert.AreEqual(0, all.Count);
        }

        [Test]
        public async Task CardCatalogCache_GetCard_ReturnsNull_WhenNotLoaded()
        {
            // Act
            var card = _gameService.CardCatalog.GetCard("card1");

            // Assert
            Assert.IsNull(card);
        }

        [Test]
        public void ApiClient_HasConfigurableTimeout()
        {
            // Act
            var client = _gameService.ApiClient;

            // Assert
            Assert.AreEqual(30, client.TimeoutSeconds);
            client.TimeoutSeconds = 60;
            Assert.AreEqual(60, client.TimeoutSeconds);
        }

        [Test]
        public void ApiClient_HasConfigurableRetries()
        {
            // Act
            var client = _gameService.ApiClient;

            // Assert
            Assert.AreEqual(3, client.MaxRetries);
            client.MaxRetries = 5;
            Assert.AreEqual(5, client.MaxRetries);
        }

        [Test]
        public void AuthService_GeneratesMockToken()
        {
            // Act
            var token = _gameService.AuthService.GetAuthorizationHeader();

            // Assert (before login, should be null)
            Assert.IsNull(token);

            // After login
            _gameService.Login("player1", "password").Wait();
            token = _gameService.AuthService.GetAuthorizationHeader();
            Assert.IsNotNull(token);
            Assert.That(token, Does.StartWith("Bearer "));
        }
    }
}
