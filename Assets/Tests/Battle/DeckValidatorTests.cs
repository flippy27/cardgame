using System;
using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using NUnit.Framework;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Tests para DeckValidator.
    /// Valida: tamaño, límite copias, rareza.
    /// </summary>
    public class DeckValidatorTests
    {
        private DeckValidationRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = new DeckValidationRules
            {
                MinDeckSize = 20,
                MaxDeckSize = 30,
                MaxCopiesPerCard = 2,
                MaxLegendariesPerDeck = 1
            };
        }

        [Test]
        public void Validate_EmptyDeck_Fails()
        {
            var deck = new DeckDefinition { cards = new CardDefinition[] { } };

            var result = DeckValidator.Validate(deck, _rules);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("empty", result.Errors[0].ToLower());
        }

        [Test]
        public void Validate_DeckTooSmall_Fails()
        {
            var card = BattleTestHelpers.CreateTestCard();
            var deck = BattleTestHelpers.CreateTestDeck(card, 10);

            var result = DeckValidator.Validate(deck, _rules);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("too small", result.Errors[0].ToLower());
        }

        [Test]
        public void Validate_DeckTooLarge_Fails()
        {
            var card = BattleTestHelpers.CreateTestCard();
            var deck = BattleTestHelpers.CreateTestDeck(card, 35);

            var result = DeckValidator.Validate(deck, _rules);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("too large", result.Errors[0].ToLower());
        }

        [Test]
        public void Validate_TooManyCopies_Fails()
        {
            var card = BattleTestHelpers.CreateTestCard("card1");
            var cards = new CardDefinition[25];
            // 20 copies of card1, 5 of other
            for (int i = 0; i < 20; i++) cards[i] = card;
            for (int i = 20; i < 25; i++) cards[i] = BattleTestHelpers.CreateTestCard("other");

            var deck = new DeckDefinition();
            var fields = typeof(DeckDefinition).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name == nameof(DeckDefinition.cards)) field.SetValue(deck, cards);
            }

            var result = DeckValidator.Validate(deck, _rules);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Errors.Any(e => e.Contains("copy limit")));
        }

        [Test]
        public void Validate_ValidDeck_Succeeds()
        {
            var card = BattleTestHelpers.CreateTestCard();
            var deck = BattleTestHelpers.CreateTestDeck(card, 25);

            var result = DeckValidator.Validate(deck, _rules);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [Test]
        public void ValidateCardIds_ValidList_Succeeds()
        {
            var ids = new[] { "card1", "card2", "card1" };

            var result = DeckValidator.ValidateCardIds(ids, _rules);

            // Should fail because too small (3 < 20), not because of ids themselves
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("too small", result.Errors[0].ToLower());
        }

        [Test]
        public void ValidateCardIds_TooManyCopies_Fails()
        {
            var ids = new string[25];
            for (int i = 0; i < 20; i++) ids[i] = "card1";
            for (int i = 20; i < 25; i++) ids[i] = "other";

            var result = DeckValidator.ValidateCardIds(ids, _rules);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Errors.Any(e => e.Contains("copy limit")));
        }

        [Test]
        public void ValidateCardIds_NullCardId_Fails()
        {
            var ids = new string[25];
            for (int i = 0; i < 25; i++) ids[i] = i == 0 ? null : "card1";

            var result = DeckValidator.ValidateCardIds(ids, _rules);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Errors.Any(e => e.Contains("null", System.StringComparison.OrdinalIgnoreCase)));
        }
    }
}
