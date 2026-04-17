using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Hero Ping Effect: deals direct damage to enemy hero.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Hero Ping", fileName = "FX_HeroPing")]
    public sealed class HeroPingEffect : EffectDefinition
    {
        [Min(1)] public int damageAmount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var source = context.FindCard(execution.SourceRuntimeId);
            var sourceName = source?.DisplayName ?? "Effect";
            context.DamageHero(execution.TargetPlayer, damageAmount);
            Debug.Log($"[Effect] {sourceName} dealt {damageAmount} direct damage to Hero");
        }
    }
}
