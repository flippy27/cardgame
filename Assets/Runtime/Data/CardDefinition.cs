using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Asset principal de una carta.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Card Definition", fileName = "CardDefinition")]
    public sealed class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string cardId = "card";
        public string displayName = "New Card";
        [TextArea] public string description;
        public CardFaction faction = CardFaction.Ember;
        public CardRarity rarity = CardRarity.Common;
        public CardType cardType = CardType.Unit;

        [Header("Stats")]
        public int manaCost = 1;
        public int attack = 1;
        public int health = 1;
        public int armor;

        [Header("Unit (for Units only)")]
        public UnitType unitType = UnitType.Melee;

        [Header("Gameplay")]
        public TargetSelectorDefinition defaultAttackTargetSelector;
        public AbilityDefinition[] abilities;

        [Header("Visuals")]
        public CardVisualProfile visualProfile;
    }
}
