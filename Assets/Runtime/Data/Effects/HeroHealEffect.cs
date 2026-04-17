using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Hero Heal Effect: heals the owning hero.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Hero Heal", fileName = "FX_HeroHeal")]
    public sealed class HeroHealEffect : EffectDefinition
    {
        [Min(1)] public int healAmount = 2;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var player = context.GetPlayerState(execution.SourcePlayer);
            var oldHp = player.HeroHealth;
            player.HeroHealth += healAmount;
            Debug.Log($"[Effect] Player {execution.SourcePlayer} Hero healed {healAmount} HP ({oldHp} → {player.HeroHealth})");
        }
    }
}
