namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Evasion: X% chance to dodge attack completely.</summary>
    public class EvasionEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageCalculation;
        public int Priority => 150;

        private static System.Random _random = new System.Random();

        public void Apply(SkillContext context)
        {
            if (context.Defender == null)
                return;

            // 25% evasion chance
            if (_random.Next(0, 100) < 25)
            {
                context.FinalDamage = 0;
            }
        }
    }
}
