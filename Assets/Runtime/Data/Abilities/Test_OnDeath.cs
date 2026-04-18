using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Abilities/Test/OnDeath", fileName = "Ability_Test_OnDeath")]
    public sealed class TestOnDeathAbility : ScriptableObject
    {
        public string abilityId = "test_on_death";
        public string displayName = "Test: Revenge on Death";
        public string description = "Deals 2 damage to enemy hero when this card dies";
        public AbilityTrigger trigger = AbilityTrigger.OnDeath;
        public TargetSelectorDefinition targetSelector;
        public EffectDefinition[] effects;
    }
}
