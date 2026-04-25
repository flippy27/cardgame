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
        [Tooltip("Turns until this card can attack after being played (1 = can't attack same turn)")]
        public int turnsUntilCanAttack = 1;

        [Header("Visuals")]
        public CardVisualProfile visualProfile;

        [Header("Attack Feel")]
        [Tooltip("0 = auto-resolve from attack stat. 1-5 = explicit projectile motion preset.")]
        [Range(0, 5)] public int attackMotionLevel;
        [Tooltip("0 = auto-resolve from attack stat. 1-5 = explicit camera shake preset.")]
        [Range(0, 5)] public int attackShakeLevel;
        [Tooltip("Optional explicit delivery type: melee, projectile, beam, arc. Empty = auto.")]
        public string attackDeliveryType;
    }
}
