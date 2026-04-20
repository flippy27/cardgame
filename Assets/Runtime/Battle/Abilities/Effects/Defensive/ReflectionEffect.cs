using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Reflection: Reflects X% damage back to attacker.</summary>
    public class ReflectionEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Attacker == null || context.Defender == null || context.FinalDamage <= 0 || context.Battle == null)
                return;

            // Reflect 30% of damage back to attacker from defender
            int reflectedDamage = (context.FinalDamage * 30) / 100;
            if (reflectedDamage > 0)
            {
                // DealDamage parameters: (attacker, defender, damage, isSkillDamage)
                // Defender is dealing damage back to Attacker
                context.Battle.DealDamage(context.Defender.RuntimeId, context.Attacker.RuntimeId, reflectedDamage, false);
            }
        }
    }
}
