using System.Collections.Generic;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Tests.Battle
{
    /// <summary>
    /// Helpers para crear mock/stub objects en tests.
    /// </summary>
    public static class BattleTestHelpers
    {
        /// <summary>
        /// Crea CardDefinition stub para testing (sin dependencias de Unity).
        /// </summary>
        public static CardDefinition CreateTestCard(
            string cardId = "test_card",
            string displayName = "Test Card",
            int manaCost = 1,
            int attack = 1,
            int health = 1,
            int armor = 0)
        {
            // Crear instancia vacía sin llamar a ScriptableObject constructor
            var card = new CardDefinition();
            // Usar reflection para asignar valores (evita issues de inicialización)
            var fields = typeof(CardDefinition).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.Name == nameof(CardDefinition.cardId)) field.SetValue(card, cardId);
                else if (field.Name == nameof(CardDefinition.displayName)) field.SetValue(card, displayName);
                else if (field.Name == nameof(CardDefinition.manaCost)) field.SetValue(card, manaCost);
                else if (field.Name == nameof(CardDefinition.attack)) field.SetValue(card, attack);
                else if (field.Name == nameof(CardDefinition.health)) field.SetValue(card, health);
                else if (field.Name == nameof(CardDefinition.armor)) field.SetValue(card, armor);
                else if (field.Name == nameof(CardDefinition.abilities)) field.SetValue(card, new AbilityDefinition[] { });
            }

            return card;
        }

        /// <summary>
        /// Crea DeckDefinition stub con N copias del mismo card.
        /// </summary>
        public static DeckDefinition CreateTestDeck(CardDefinition card, int copies = 5)
        {
            var cards = new CardDefinition[copies];
            for (var i = 0; i < copies; i++)
            {
                cards[i] = card;
            }

            var deck = new DeckDefinition();
            var fields = typeof(DeckDefinition).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.Name == nameof(DeckDefinition.deckId)) field.SetValue(deck, "test_deck");
                else if (field.Name == nameof(DeckDefinition.displayName)) field.SetValue(deck, "Test Deck");
                else if (field.Name == nameof(DeckDefinition.cards)) field.SetValue(deck, cards);
            }

            return deck;
        }

        /// <summary>
        /// Crea DuelRulesProfile stub con valores estándar para testing.
        /// </summary>
        public static DuelRulesProfile CreateTestRules(
            int startingHeroHealth = 20,
            int startingMana = 1,
            int maxMana = 10,
            int manaPerTurn = 1,
            int startingHandSize = 4,
            bool drawAtTurnStart = true)
        {
            var rules = new DuelRulesProfile();
            var fields = typeof(DuelRulesProfile).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.Name == nameof(DuelRulesProfile.startingHeroHealth)) field.SetValue(rules, startingHeroHealth);
                else if (field.Name == nameof(DuelRulesProfile.startingMana)) field.SetValue(rules, startingMana);
                else if (field.Name == nameof(DuelRulesProfile.maxMana)) field.SetValue(rules, maxMana);
                else if (field.Name == nameof(DuelRulesProfile.manaPerTurn)) field.SetValue(rules, manaPerTurn);
                else if (field.Name == nameof(DuelRulesProfile.startingHandSize)) field.SetValue(rules, startingHandSize);
                else if (field.Name == nameof(DuelRulesProfile.drawAtTurnStart)) field.SetValue(rules, drawAtTurnStart);
            }

            return rules;
        }
    }
}
