namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>ChainEffect: Placeholder for complex targeting skill.</summary>
    public class ChainEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnSelectTarget;
        public int Priority => 100;

        public void Apply(SkillContext context)
        {
            // Complex targeting skill - requires custom logic
        }
    }
}
