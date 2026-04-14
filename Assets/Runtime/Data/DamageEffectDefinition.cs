using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Effect/Damage", fileName = "FX_Damage")]
    public sealed class DamageEffectDefinition : EffectDefinition
    {
        public int amount = 1;
        public bool ignoreArmor;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            context.DealDamage(execution.SourceRuntimeId, execution.TargetRuntimeId, amount, ignoreArmor);
        }
    }
}