using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>DiagonalAttackEffect: Placeholder for complex targeting skill.</summary>
    public class DiagonalAttackEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnSelectTarget;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            // Complex targeting skill - requires custom logic
        }
    }
}
