using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Heal Effect: restores health to target.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Heal", fileName = "FX_Heal")]
    public sealed class HealEffect : EffectDefinition
    {
        [Min(1)] public int healAmount = 2;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var target = context.FindCard(execution.TargetRuntimeId);
            if (target != null && target.CurrentHealth < target.MaxHealth)
            {
                var oldHealth = target.CurrentHealth;
                target.CurrentHealth += healAmount;
                if (target.CurrentHealth > target.MaxHealth)
                {
                    target.CurrentHealth = target.MaxHealth;
                }
                Debug.Log($"[Effect] {target.DisplayName} healed {healAmount} HP ({oldHealth} → {target.CurrentHealth})");
            }
        }
    }
}
