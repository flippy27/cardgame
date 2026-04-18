namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Selects targets for the attack.
    /// Skills can modify target selection (taunt, cleave, chain, etc).
    /// </summary>
    public class TargetSelectionPhase : ISkillPhase
    {
        public bool CanExecute(SkillContext context)
        {
            return context.IsValidAttack && context.Trigger == SkillTrigger.OnSelectTarget;
        }

        public SkillContext Execute(SkillContext context)
        {
            // Clear target list
            context.TargetList.Clear();

            // Execute target selection skills
            var pipeline = new SkillPipeline();

            return context;
        }
    }
}
