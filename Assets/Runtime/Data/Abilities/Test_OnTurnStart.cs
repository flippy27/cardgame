using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Abilities/Test/OnTurnStart", fileName = "Ability_Test_OnTurnStart")]
    public sealed class TestOnTurnStartAbility : ScriptableObject
    {
        public string abilityId = "test_on_turn_start";
        public string displayName = "Test: Heal on Turn Start";
        public string description = "Heals the hero for 1 HP at the start of each turn";
        public AbilityTrigger trigger = AbilityTrigger.OnTurnStart;
        public TargetSelectorDefinition targetSelector;
        public EffectDefinition[] effects;
    }
}
