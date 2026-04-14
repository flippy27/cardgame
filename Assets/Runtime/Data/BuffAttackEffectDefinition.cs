using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Effect/Buff Attack", fileName = "FX_BuffAttack")]
    public sealed class BuffAttackEffectDefinition : EffectDefinition
    {
        public int amount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            context.ModifyAttack(execution.TargetRuntimeId, amount);
        }
    }
}