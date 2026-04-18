namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Validates if an attack is valid.
    /// Skills can block or allow attacks based on position, status, etc.
    /// </summary>
    public class AttackValidationPhase : ISkillPhase
    {
        public bool CanExecute(SkillContext context)
        {
            return context.Trigger == SkillTrigger.OnValidateAttack;
        }

        public SkillContext Execute(SkillContext context)
        {
            // Check stunned first
            if (context.Attacker?.Stunned == true)
            {
                context.IsValidAttack = false;
                return context;
            }

            // Execute attacker validation skills
            var pipeline = new SkillPipeline();

            // Execute defender validation skills

            return context;
        }
    }
}
