using Flippy.CardDuelMobile.Battle.Abilities;
using AbilityTriggerEnum = Flippy.CardDuelMobile.Battle.Abilities.AbilityTrigger;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Abilities/Test/OnTurnEnd", fileName = "Ability_Test_OnTurnEnd")]
    public sealed class TestOnTurnEndAbility : ScriptableObject
    {
        public string abilityId = "test_on_turn_end";
        public string displayName = "Test: Mana Loss on Turn End";
        public string description = "Enemy loses 1 mana at the end of each turn";
        public AbilityTriggerEnum trigger = AbilityTriggerEnum.OnTurnEnd;
        public TargetSelectorDefinition targetSelector;
        public EffectDefinition[] effects;
    }
}
