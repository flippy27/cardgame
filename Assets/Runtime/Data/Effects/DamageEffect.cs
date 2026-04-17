using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Damage Effect: deals damage to target.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Damage", fileName = "FX_Damage")]
    public sealed class DamageEffect : EffectDefinition
    {
        [Min(1)] public int damageAmount = 1;
        public bool ignoreArmor = false;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            context.DealDamage(execution.SourceRuntimeId, execution.TargetRuntimeId, damageAmount, ignoreArmor);
        }
    }
}
