namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Calculates final damage after all modifiers.
    /// Skills can modify damage (armor, trample, execute, etc).
    /// </summary>
    public class DamageCalculationPhase : ISkillPhase
    {
        public bool CanExecute(SkillContext context)
        {
            return context.IsValidAttack && context.TargetList.Count > 0 && 
                   context.Trigger == SkillTrigger.OnDamageCalculation;
        }

        public SkillContext Execute(SkillContext context)
        {
            // Set final damage = base damage initially
            context.FinalDamage = context.BaseDamage;

            // Execute attacker damage modification skills
            var pipeline = new SkillPipeline();

            // Execute defender damage reduction skills

            return context;
        }
    }
}
