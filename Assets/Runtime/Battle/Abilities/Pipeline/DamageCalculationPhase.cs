namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Calculates final damage after all modifiers.
    /// Skills can modify damage (armor, trample, execute, etc).
    /// </summary>
    public class DamageCalculationPhase : IAbilityPhase
    {
        public bool CanExecute(AbilityContext context)
        {
            return context.IsValidAttack && context.TargetList.Count > 0 && 
                   context.Trigger == AbilityTrigger.OnDamageCalculation;
        }

        public AbilityContext Execute(AbilityContext context)
        {
            // Set final damage = base damage initially
            context.FinalDamage = context.BaseDamage;

            // Run all damage calculation effects (attacker and defender effects both run)
            var damageEffects = AbilityRegistry.GetAllEffects(AbilityTrigger.OnDamageCalculation);
            foreach (var effect in damageEffects)
            {
                effect.Apply(context);
            }

            return context;
        }
    }
}
