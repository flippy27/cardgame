namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Selects targets for the attack.
    /// Skills can modify target selection (taunt, cleave, chain, etc).
    /// </summary>
    public class TargetSelectionPhase : IAbilityPhase
    {
        public bool CanExecute(AbilityContext context)
        {
            return context.IsValidAttack && context.Trigger == AbilityTrigger.OnSelectTarget;
        }

        public AbilityContext Execute(AbilityContext context)
        {
            // Clear target list
            context.TargetList.Clear();

            // Execute target selection skills
            // TODO: Implement target selection skill execution

            return context;
        }
    }
}
