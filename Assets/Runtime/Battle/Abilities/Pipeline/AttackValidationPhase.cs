namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Validates if an attack is valid.
    /// Skills can block or allow attacks based on position, status, etc.
    /// </summary>
    public class AttackValidationPhase : IAbilityPhase
    {
        public bool CanExecute(AbilityContext context)
        {
            return context.Trigger == AbilityTrigger.OnValidateAttack;
        }

        public AbilityContext Execute(AbilityContext context)
        {
            // Check stunned first
            if (context.Attacker?.Stunned == true)
            {
                context.IsValidAttack = false;
                return context;
            }

            // Run all registered validation effects for both attacker and defender
            var validationEffects = AbilityRegistry.GetAllEffects(AbilityTrigger.OnValidateAttack);
            foreach (var effect in validationEffects)
            {
                effect.Apply(context);
                if (!context.IsValidAttack)
                    return context;
            }

            return context;
        }
    }
}
