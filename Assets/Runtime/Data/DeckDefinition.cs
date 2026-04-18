using Flippy.CardDuelMobile.Core;
using UnityEngine;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Decks/Deck Definition", fileName = "DeckDefinition")]
    public sealed class DeckDefinition : ScriptableObject
    {
        [System.Serializable]
        public class DeckCard
        {
            public CardDefinition card;
            public int quantity;
        }

        public string deckId = "deck";
        public string displayName = "New Deck";
        [TextArea] public string description;
        public string deckType = "Balanced";
        public CardFaction faction = CardFaction.Ember;
        public DeckCard[] cards;

        public int GetTotalCards()
        {
            int total = 0;
            if (cards != null)
            {
                foreach (var deckCard in cards)
                {
                    total += deckCard.quantity;
                }
            }
            return total;
        }
    }
}
