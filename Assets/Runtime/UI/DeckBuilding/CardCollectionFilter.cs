using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Pure filter state — no MonoBehaviour.
    /// Apply() filters a PlayerCardSummaryEntryDto list and returns a filtered copy.
    ///
    /// Rarity and Faction are now available from PlayerCardDto.cardRarity / .cardFaction.
    /// Faction uses the first ownedInstance's faction value (all copies share the same definition).
    /// </summary>
    public sealed class CardCollectionFilter
    {
        public CardRarity? Rarity { get; set; }
        public CardFaction? Faction { get; set; }
        public string SearchText { get; set; } = string.Empty;

        public bool IsActive =>
            Rarity.HasValue || Faction.HasValue || !string.IsNullOrWhiteSpace(SearchText);

        public void Clear()
        {
            Rarity = null;
            Faction = null;
            SearchText = string.Empty;
        }

        /// <summary>
        /// Returns a filtered copy of the summary entries.
        /// All active filters are AND-combined.
        /// </summary>
        public List<PlayerCardsApiClient.PlayerCardSummaryEntryDto> Apply(
            List<PlayerCardsApiClient.PlayerCardSummaryEntryDto> entries)
        {
            if (entries == null) return new();

            IEnumerable<PlayerCardsApiClient.PlayerCardSummaryEntryDto> result = entries;

            if (Rarity.HasValue)
            {
                int rarityInt = (int)Rarity.Value;
                result = result.Where(e =>
                    e.ownedInstances != null &&
                    e.ownedInstances.Length > 0 &&
                    e.ownedInstances[0].cardRarity == rarityInt);
            }

            if (Faction.HasValue)
            {
                int factionInt = (int)Faction.Value;
                result = result.Where(e =>
                    e.ownedInstances != null &&
                    e.ownedInstances.Length > 0 &&
                    e.ownedInstances[0].cardFaction == factionInt);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLowerInvariant();
                result = result.Where(e =>
                    (e.displayName?.ToLowerInvariant().Contains(lower) == true) ||
                    (e.cardId?.ToLowerInvariant().Contains(lower) == true));
            }

            return result.ToList();
        }
    }
}
