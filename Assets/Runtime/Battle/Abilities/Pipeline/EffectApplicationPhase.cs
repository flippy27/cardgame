namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Applies status effects and secondary effects from damage.
    /// Skills can apply poison, stun, leech, etc.
    /// </summary>
    public class EffectApplicationPhase : IAbilityPhase
    {
        public bool CanExecute(AbilityContext context)
        {
            return context.IsValidAttack && context.FinalDamage > 0 && 
                   context.Trigger == AbilityTrigger.OnDamageDealt;
        }

        public AbilityContext Execute(AbilityContext context)
        {
            // Run all damage dealt effects (post-damage application effects)
            var damageDealtEffects = AbilityRegistry.GetAllEffects(AbilityTrigger.OnDamageDealt);
            foreach (var effect in damageDealtEffects)
            {
                effect.Apply(context);
            }

            return context;
        }
    }
}
