using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Enrage: Gains +1 ATK when damaged.</summary>
    public class EnrageEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null || context.FinalDamage <= 0)
                return;

            context.Defender.EnrageBonus += 1;
        }
    }
}
