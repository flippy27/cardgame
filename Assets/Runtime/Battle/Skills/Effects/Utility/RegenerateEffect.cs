namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Regenerate: Heals X HP at end of turn.</summary>
    public class RegenerateEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnTurnStart;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            // Regenerate handled via abilities system
            // This is a placeholder
        }
    }
}
