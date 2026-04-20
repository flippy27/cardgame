using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Regenerate: Heals X HP at end of turn.</summary>
    public class RegenerateEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnTurnStart;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            // Regenerate handled via abilities system
            // This is a placeholder
        }
    }
}
