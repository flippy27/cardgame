using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Effect/Heal", fileName = "FX_Heal")]
    public sealed class HealEffectDefinition : EffectDefinition
    {
        public int amount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            context.Heal(execution.TargetRuntimeId, amount);
        }
    }
}