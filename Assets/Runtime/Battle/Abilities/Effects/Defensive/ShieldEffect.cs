using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Shield: Blocks first attack completely (divine shield).</summary>
    public class ShieldEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageCalculation;
        public int Priority => 200; // High priority - block before other calculations

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null || !context.Defender.HasShield)
                return;

            // Block all damage
            context.FinalDamage = 0;
            
            // Consume shield
            context.Defender.HasShield = false;
        }
    }
}
