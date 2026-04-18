namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Taunt: All enemy attacks must target this card.</summary>
    public class TauntEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnSelectTarget;
        public int Priority => 200; // High priority - overrides normal targeting

        public void Apply(SkillContext context)
        {
            // Taunt is handled by target selectors checking for taunt targets
            // This effect acts as a flag/marker
        }
    }
}
