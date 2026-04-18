namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Shield: Blocks first attack completely (divine shield).</summary>
    public class ShieldEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageCalculation;
        public int Priority => 200; // High priority - block before other calculations

        public void Apply(SkillContext context)
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
