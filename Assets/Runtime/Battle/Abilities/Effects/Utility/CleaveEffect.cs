using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Cleave: Hits all enemies in same row.</summary>
    public class CleaveEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnSelectTarget;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            // Cleave requires custom targeting logic
            // Would need to add all cards in same row to target list
            // Placeholder for now
        }
    }
}
