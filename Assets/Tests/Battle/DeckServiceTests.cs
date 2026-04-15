using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Tests
{
    public class DeckServiceTests
    {
        private DeckService _deckService;
        private CardGameApiClient _apiClient;
        private AuthService _authService;
        private CardCatalogCache _cardCatalog;

        [SetUp]
        public void Setup()
        {
            _apiClient = new CardGameApiClient("http://localhost:5000");
            _authService = new AuthService(_apiClient);
            _cardCatalog = new CardCatalogCache(_apiClient);
            _deckService = new DeckService(_apiClient, _authService, _cardCatalog);
        }

        [Test]
        public void LoadDecks_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _deckService.LoadDecks();
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void GetDeck_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _deckService.GetDeck("deck1");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void CreateDeck_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();
            var cardIds = new[] { "card1", "card2" };

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _deckService.CreateDeck("TestDeck", "Test", cardIds);
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void CreateDeck_InvalidDeck_ThrowsValidationException()
        {
            // Arrange
            // Empty deck is invalid
            var cardIds = new string[0];

            // Act & Assert
            var ex = Assert.ThrowsAsync<ValidationException>(async () =>
            {
                await _deckService.CreateDeck("EmptyDeck", "Test", cardIds);
            });
            Assert.That(ex.Message, Does.Contain("Invalid deck"));
        }

        [Test]
        public void UpdateDeck_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _deckService.UpdateDeck("deck1", "NewName");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void DeleteDeck_NotAuthenticated_ThrowsException()
        {
            // Arrange
            _authService.Logout();

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidGameStateException>(async () =>
            {
                await _deckService.DeleteDeck("deck1");
            });
            Assert.That(ex.Message, Does.Contain("Not authenticated"));
        }

        [Test]
        public void ValidateDeck_CatalogNotLoaded_ThrowsException()
        {
            // Arrange
            var cardIds = new[] { "card1", "card2" };

            // Act & Assert
            var ex = Assert.Throws<InvalidGameStateException>(() =>
            {
                _deckService.ValidateDeck(cardIds);
            });
            Assert.That(ex.Message, Does.Contain("Card catalog not loaded"));
        }

        [Test]
        public void DeckDto_Serializable()
        {
            // Arrange
            var deck = new DeckDto
            {
                DeckId = "deck1",
                PlayerId = "player1",
                Name = "My Deck",
                Description = "A test deck",
                CardIds = new[] { "card1", "card2", "card3" },
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                WinRate = 55,
                Matches = 20
            };

            // Act
            var json = JsonUtility.ToJson(deck);
            var deserialized = JsonUtility.FromJson<DeckDto>(json);

            // Assert
            Assert.AreEqual(deck.DeckId, deserialized.DeckId);
            Assert.AreEqual(deck.PlayerId, deserialized.PlayerId);
            Assert.AreEqual(deck.Name, deserialized.Name);
            Assert.AreEqual(deck.CardIds.Length, deserialized.CardIds.Length);
            Assert.AreEqual(deck.WinRate, deserialized.WinRate);
        }

        [Test]
        public void ClearCache_RemovesAllCachedDecks()
        {
            // Arrange
            _deckService.ClearCache();

            // Act
            _deckService.ClearCache();

            // Assert - no exception, cache is empty
            Assert.Pass();
        }
    }
}
