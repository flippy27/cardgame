using UnityEngine;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Mazo simple para prototipo.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Deck Definition", fileName = "DeckDefinition")]
    public sealed class DeckDefinition : ScriptableObject
    {
        public string deckId = "deck";
        public string displayName = "New Deck";
        public CardDefinition[] cards;
    }
}
