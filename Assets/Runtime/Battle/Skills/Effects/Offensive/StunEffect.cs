namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Stun: Target skips next attack.</summary>
    public class StunEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Defender == null)
                return;

            context.Defender.Stunned = true;
        }
    }
}
