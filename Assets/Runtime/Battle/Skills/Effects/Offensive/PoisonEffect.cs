namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Poison: Applies poison stacks to target.</summary>
    public class PoisonEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Defender == null)
                return;

            context.Defender.PoisonStacks += 2; // 2 poison stacks per hit
        }
    }
}
