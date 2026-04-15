using System.Collections.Generic;
using Flippy.CardDuelMobile.Networking;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para CardCatalogCache.
    /// </summary>
    public class CardCatalogCacheTests
    {
        private CardCatalogCache _cache;

        [SetUp]
        public void SetUp()
        {
            _cache = new CardCatalogCache();
        }

        [Test]
        public void Constructor_InitializesUnloaded()
        {
            Assert.IsFalse(_cache.IsLoaded);
            Assert.IsFalse(_cache.IsLoading);
            Assert.IsNull(_cache.LoadError);
        }

        [Test]
        public void GetCard_BeforeLoad_ReturnsNull()
        {
            var card = _cache.GetCard("test_card");
            Assert.IsNull(card);
        }

        [Test]
        public void GetAll_BeforeLoad_ReturnsEmpty()
        {
            var all = _cache.GetAll();
            Assert.AreEqual(0, all.Count);
        }

        [Test]
        public void GetStats_BeforeLoad_ReturnsZeros()
        {
            var (total, withAbilities) = _cache.GetStats();
            Assert.AreEqual(0, total);
            Assert.AreEqual(0, withAbilities);
        }

        [Test]
        public void Clear_RemovesAllData()
        {
            _cache.Clear();

            Assert.IsFalse(_cache.IsLoaded);
            Assert.IsFalse(_cache.IsLoading);
            Assert.IsNull(_cache.LoadError);
        }

        [Test]
        public void ValidateDeck_BeforeLoad_FailsValidation()
        {
            var ids = new[] { "card1", "card2", "card3" };
            var result = _cache.ValidateDeck(ids);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void ServerCardDefinition_ValidStructure()
        {
            var card = new ServerCardDefinition
            {
                CardId = "test",
                DisplayName = "Test Card",
                ManaCost = 2,
                Attack = 3,
                Health = 2,
                Armor = 0,
                AllowedRow = 0,
                DefaultAttackSelector = 1,
                Abilities = new ServerAbilityDefinition[] { }
            };

            Assert.AreEqual("test", card.CardId);
            Assert.AreEqual(2, card.ManaCost);
            Assert.AreEqual(0, card.Abilities.Length);
        }

        [Test]
        public void ServerAbilityDefinition_WithEffects()
        {
            var ability = new ServerAbilityDefinition
            {
                AbilityId = "damage",
                Trigger = 0,
                Selector = 0,
                Effects = new[]
                {
                    new ServerEffectDefinition { Kind = 0, Amount = 3 },
                    new ServerEffectDefinition { Kind = 2, Amount = 1 }
                }
            };

            Assert.AreEqual(2, ability.Effects.Length);
            Assert.AreEqual(3, ability.Effects[0].Amount);
        }
    }
}
