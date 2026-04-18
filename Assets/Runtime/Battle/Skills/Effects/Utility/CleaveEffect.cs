namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Cleave: Hits all enemies in same row.</summary>
    public class CleaveEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnSelectTarget;
        public int Priority => 100;

        public void Apply(SkillContext context)
        {
            // Cleave requires custom targeting logic
            // Would need to add all cards in same row to target list
            // Placeholder for now
        }
    }
}
