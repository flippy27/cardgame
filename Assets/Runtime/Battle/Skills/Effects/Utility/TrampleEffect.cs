namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Trample: Ignores target armor.</summary>
    public class TrampleEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageCalculation;
        public int Priority => 100;

        public void Apply(SkillContext context)
        {
            context.IgnoreArmor = true;
        }
    }
}
