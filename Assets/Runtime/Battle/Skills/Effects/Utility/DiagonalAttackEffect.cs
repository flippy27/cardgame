namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>DiagonalAttackEffect: Placeholder for complex targeting skill.</summary>
    public class DiagonalAttackEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnSelectTarget;
        public int Priority => 100;

        public void Apply(SkillContext context)
        {
            // Complex targeting skill - requires custom logic
        }
    }
}
