using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Execute: Bonus damage proportional to target's missing health.</summary>
    public class ExecuteEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageCalculation;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null || context.Defender.MaxHealth <= 0)
                return;

            // Bonus damage = 50% of missing health
            int missingHealth = context.Defender.MaxHealth - context.Defender.CurrentHealth;
            int bonusDamage = (missingHealth * 50) / 100;
            context.FinalDamage += bonusDamage;
        }
    }
}
