using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Effect/Gain Armor", fileName = "FX_GainArmor")]
    public sealed class GainArmorEffectDefinition : EffectDefinition
    {
        public int amount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            context.GainArmor(execution.TargetRuntimeId, amount);
        }
    }
}