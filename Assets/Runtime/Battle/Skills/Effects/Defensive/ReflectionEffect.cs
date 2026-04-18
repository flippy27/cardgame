namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Reflection: Reflects X% damage back to attacker.</summary>
    public class ReflectionEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Attacker == null || context.FinalDamage <= 0)
                return;

            // Reflect 30% of damage back
            int reflectedDamage = (context.FinalDamage * 30) / 100;
            if (reflectedDamage > 0 && context.Battle != null)
            {
                context.Battle.DealDamage(context.Defender?.RuntimeId, context.Attacker.RuntimeId, reflectedDamage, false);
            }
        }
    }
}
