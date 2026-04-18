namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Charge: Can attack the turn it's played.</summary>
    public class ChargeEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnCardInitialize;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            // Charge would need to mark card as able to attack immediately
            // Requires changes to attack flow
        }
    }
}
