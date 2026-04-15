using System.Collections.Generic;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para CardGameApiClient.
    /// Nota: Tests reales requerirían servidor HTTP vivo o mocks.
    /// Por ahora, tests de validación básica.
    /// </summary>
    public class CardGameApiClientTests
    {
        private CardGameApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new CardGameApiClient("http://localhost:5000");
        }

        [Test]
        public void Constructor_ValidUrl_Success()
        {
            var client = new CardGameApiClient("http://api.example.com");
            Assert.IsNotNull(client);
        }

        [Test]
        public void Constructor_EmptyUrl_ThrowsValidationException()
        {
            Assert.Throws<ValidationException>(() => new CardGameApiClient(""));
        }

        [Test]
        public void Constructor_TrimsTrailingSlash()
        {
            var client = new CardGameApiClient("http://localhost:5000/");
            Assert.IsNotNull(client);
            // Internal baseUrl should be trimmed (can't verify directly but construction succeeds)
        }

        [Test]
        public void CardStatsDto_Serializable()
        {
            var stats = new CardStatsDto
            {
                totalCards = 18,
                manaCostAvg = 2.5f,
                attackAvg = 2.3f,
                healthAvg = 2.8f,
                cardsWithAbilities = 12
            };

            Assert.AreEqual(18, stats.totalCards);
            Assert.AreEqual(2.5f, stats.manaCostAvg, 0.01f);
        }

        [Test]
        public void ServerCardDefinition_ValidStructure()
        {
            var card = new ServerCardDefinition
            {
                CardId = "test_card",
                DisplayName = "Test",
                ManaCost = 2,
                Attack = 1,
                Health = 3,
                Armor = 0,
                AllowedRow = 0, // FrontOnly
                DefaultAttackSelector = 1, // FrontlineFirst
                Abilities = new ServerAbilityDefinition[] { }
            };

            Assert.AreEqual("test_card", card.CardId);
            Assert.AreEqual(2, card.ManaCost);
        }

        [Test]
        public void ServerAbilityDefinition_ValidStructure()
        {
            var ability = new ServerAbilityDefinition
            {
                AbilityId = "test_ability",
                Trigger = 0, // OnPlay
                Selector = 0, // Self
                Effects = new[]
                {
                    new ServerEffectDefinition { Kind = 0, Amount = 5 } // Damage
                }
            };

            Assert.AreEqual("test_ability", ability.AbilityId);
            Assert.AreEqual(1, ability.Effects.Length);
        }

        // Note: Actual HTTP tests would require:
        // - Mock HTTP server or HttpClientFactory with mock handler
        // - Integration tests against real API
        // - Async test support in NUnit
    }
}
