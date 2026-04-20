using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Taunt: All enemy attacks must target this card.</summary>
    public class TauntEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnSelectTarget;
        public int Priority => 200; // High priority - overrides normal targeting

        public void Apply(AbilityContext context)
        {
            // Taunt is handled by target selectors checking for taunt targets
            // This effect acts as a flag/marker
        }
    }
}
