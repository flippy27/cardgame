using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Buff Attack Effect: increases target's attack.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Buff Attack", fileName = "FX_BuffAttack")]
    public sealed class BuffAttackEffect : EffectDefinition
    {
        [Min(1)] public int attackBonus = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var target = context.FindCard(execution.TargetRuntimeId);
            if (target != null)
            {
                target.Attack += attackBonus;
                Debug.Log($"[Effect] {target.DisplayName} gained +{attackBonus} ATK (total: {target.Attack})");
            }
        }
    }
}
