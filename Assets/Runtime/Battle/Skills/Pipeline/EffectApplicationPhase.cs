namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Applies status effects and secondary effects from damage.
    /// Skills can apply poison, stun, leech, etc.
    /// </summary>
    public class EffectApplicationPhase : ISkillPhase
    {
        public bool CanExecute(SkillContext context)
        {
            return context.IsValidAttack && context.FinalDamage > 0 && 
                   context.Trigger == SkillTrigger.OnDamageDealt;
        }

        public SkillContext Execute(SkillContext context)
        {
            // Execute attacker effect skills (leech, ricochet, etc)
            var pipeline = new SkillPipeline();

            // Execute defender effect skills (applied after taking damage)

            return context;
        }
    }
}
