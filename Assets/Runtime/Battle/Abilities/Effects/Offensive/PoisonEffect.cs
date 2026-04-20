using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Poison: Applies poison stacks to target.</summary>
    public class PoisonEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null)
                return;

            context.Defender.PoisonStacks += 2; // 2 poison stacks per hit
        }
    }
}
