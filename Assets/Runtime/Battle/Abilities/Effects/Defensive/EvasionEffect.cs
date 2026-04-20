using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Evasion: X% chance to dodge attack completely.</summary>
    public class EvasionEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageCalculation;
        public int Priority => 150;

        private static System.Random _random = new System.Random();

        public void Apply(AbilityContext context)
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
