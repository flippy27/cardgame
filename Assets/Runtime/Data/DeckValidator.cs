using System;
using System.Collections.Generic;
using System.Linq;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Valida mazos contra reglas configurables.
    /// SYNC: Keep aligned with API DeckValidationService constants.
    /// </summary>
    public sealed class DeckValidationRules
    {
        /// <summary>Minimum cards in deck (synced with API: 20)</summary>
        public int MinDeckSize = 20;
        /// <summary>Maximum cards in deck (synced with API: 30)</summary>
        public int MaxDeckSize = 30;
        /// <summary>Max copies of same card (synced with API: 3)</summary>
        public int MaxCopiesPerCard = 3;
        /// <summary>Max legendaries per deck (API has no limit, but client can enforce)</summary>
        public int MaxLegendariesPerDeck = int.MaxValue; // No limit by default
    }

    /// <summary>
    /// Resultado de validación de mazo.
    /// </summary>
    public sealed class DeckValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new();

        public void AddError(string error) => Errors.Add(error);

        public override string ToString() => IsValid
            ? "Valid"
            : $"Invalid: {string.Join(", ", Errors)}";
    }

    /// <summary>
    /// Validador de mazos.
    /// </summary>
    public static class DeckValidator
    {
        /// <summary>
        /// Valida DeckDefinition contra reglas.
        /// </summary>
        public static DeckValidationResult Validate(
            DeckDefinition deck,
            DeckValidationRules rules = null,
            Func<string, CardDefinition> cardLookup = null)
        {
            rules ??= new DeckValidationRules();
            var result = new DeckValidationResult { IsValid = true };

            // Null check
            if (deck == null || deck.cards == null || deck.cards.Length == 0)
            {
                result.IsValid = false;
                result.AddError("Deck is empty or null.");
                return result;
            }

            // Size validation
            if (deck.cards.Length < rules.MinDeckSize)
            {
                result.IsValid = false;
                result.AddError($"Deck too small: {deck.cards.Length} < {rules.MinDeckSize}.");
            }

            if (deck.cards.Length > rules.MaxDeckSize)
            {
                result.IsValid = false;
                result.AddError($"Deck too large: {deck.cards.Length} > {rules.MaxDeckSize}.");
            }

            // Count copies per card
            var cardCounts = new Dictionary<string, int>();
            foreach (var deckCard in deck.cards.Where(c => c != null && c.card != null))
            {
                var key = deckCard.card.cardId;
                if (!cardCounts.ContainsKey(key))
                {
                    cardCounts[key] = 0;
                }
                cardCounts[key] += deckCard.quantity;
            }

            // Copy limits
            foreach (var kvp in cardCounts.Where(x => x.Value > rules.MaxCopiesPerCard))
            {
                result.IsValid = false;
                result.AddError($"Card '{kvp.Key}' exceeds copy limit: {kvp.Value} > {rules.MaxCopiesPerCard}.");
            }

            // Rarity limits (if CardDefinition accessible)
            if (cardLookup != null)
            {
                var legendaryCount = 0;
                foreach (var deckCard in deck.cards.Where(c => c != null && c.card != null))
                {
                    var def = cardLookup(deckCard.card.cardId);
                    if (def != null && def.rarity == Core.CardRarity.Legendary)
                    {
                        legendaryCount += deckCard.quantity;
                    }
                }

                if (legendaryCount > rules.MaxLegendariesPerDeck)
                {
                    result.IsValid = false;
                    result.AddError($"Too many legendaries: {legendaryCount} > {rules.MaxLegendariesPerDeck}.");
                }
            }

            return result;
        }

        /// <summary>
        /// Valida lista de card IDs (sin CardDefinition objects).
        /// </summary>
        public static DeckValidationResult ValidateCardIds(
            IEnumerable<string> cardIds,
            DeckValidationRules rules = null)
        {
            rules ??= new DeckValidationRules();
            var result = new DeckValidationResult { IsValid = true };
            var ids = cardIds.ToList();

            // Size validation
            if (ids.Count < rules.MinDeckSize)
            {
                result.IsValid = false;
                result.AddError($"Deck too small: {ids.Count} < {rules.MinDeckSize}.");
            }

            if (ids.Count > rules.MaxDeckSize)
            {
                result.IsValid = false;
                result.AddError($"Deck too large: {ids.Count} > {rules.MaxDeckSize}.");
            }

            // Copy limits
            var counts = new Dictionary<string, int>();
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    result.IsValid = false;
                    result.AddError("Null or empty card ID.");
                    continue;
                }

                if (!counts.ContainsKey(id))
                {
                    counts[id] = 0;
                }
                counts[id]++;
            }

            foreach (var kvp in counts.Where(x => x.Value > rules.MaxCopiesPerCard))
            {
                result.IsValid = false;
                result.AddError($"Card '{kvp.Key}' exceeds copy limit: {kvp.Value} > {rules.MaxCopiesPerCard}.");
            }

            return result;
        }
    }
}
