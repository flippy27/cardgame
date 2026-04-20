using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Stun: Target skips next attack.</summary>
    public class StunEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null)
                return;

            context.Defender.Stunned = true;
        }
    }
}
