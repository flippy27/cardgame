using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>MeleeRangeEffect: Placeholder for complex targeting skill.</summary>
    public class MeleeRangeEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnSelectTarget;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            // Complex targeting skill - requires custom logic
        }
    }
}
