using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Effect/Hit Hero", fileName = "FX_HitHero")]
    public sealed class HitHeroEffectDefinition : EffectDefinition
    {
        public int amount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            context.DamageHero(execution.TargetPlayer, amount);
        }
    }
}