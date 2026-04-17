using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Splash Damage Effect: damages all enemy cards on board.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Splash Damage", fileName = "FX_SplashDamage")]
    public sealed class SplashDamageEffect : EffectDefinition
    {
        [Min(1)] public int damageAmount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var source = context.FindCard(execution.SourceRuntimeId);
            if (source == null) return;

            var enemy = context.GetPlayerState(execution.TargetPlayer);
            var damaged = 0;

            foreach (var slot in enemy.Board)
            {
                if (slot.Occupant != null && !slot.Occupant.IsDead)
                {
                    context.DealDamage(execution.SourceRuntimeId, slot.Occupant.RuntimeId, damageAmount, ignoreArmor: false);
                    damaged++;
                }
            }

            Debug.Log($"[Effect] Splash damage: hit {damaged} enemies for {damageAmount} HP");
        }
    }
}
