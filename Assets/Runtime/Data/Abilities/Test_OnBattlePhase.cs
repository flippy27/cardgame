using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Abilities/Test/OnBattlePhase", fileName = "Ability_Test_OnBattlePhase")]
    public sealed class TestOnBattlePhaseAbility : ScriptableObject
    {
        public string abilityId = "test_on_battle_phase";
        public string displayName = "Test: ATK Buff on Battle Phase";
        public string description = "Gains +1 ATK during battle phase";
        public AbilityTrigger trigger = AbilityTrigger.OnBattlePhase;
        public TargetSelectorDefinition targetSelector;
        public EffectDefinition[] effects;
    }
}
