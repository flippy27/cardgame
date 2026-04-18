using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Abilities/Test/OnDamaged", fileName = "Ability_Test_OnDamaged")]
    public sealed class TestOnDamagedAbility : ScriptableObject
    {
        public string abilityId = "test_on_damaged";
        public string displayName = "Test: Shield on Damaged";
        public string description = "Gains +1 Armor when damaged";
        public AbilityTrigger trigger = AbilityTrigger.OnDamaged;
        public TargetSelectorDefinition targetSelector;
        public EffectDefinition[] effects;
    }
}
