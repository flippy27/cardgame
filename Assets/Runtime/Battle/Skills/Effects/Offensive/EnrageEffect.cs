namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Enrage: Gains +1 ATK when damaged.</summary>
    public class EnrageEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageReceived;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Defender == null || context.FinalDamage <= 0)
                return;

            context.Defender.EnrageBonus += 1;
        }
    }
}
