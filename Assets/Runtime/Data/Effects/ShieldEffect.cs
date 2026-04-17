using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Shield Effect: grants HasShield status to target.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Shield", fileName = "FX_Shield")]
    public sealed class ShieldEffect : EffectDefinition
    {
        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var target = context.FindCard(execution.TargetRuntimeId);
            if (target != null && !target.HasShield)
            {
                target.HasShield = true;
                Debug.Log($"[Effect] {target.DisplayName} gained Shield!");
            }
        }
    }
}
