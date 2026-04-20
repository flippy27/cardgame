using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Leech: Heals attacker equal to damage dealt.</summary>
    public class LeechEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Attacker == null || context.FinalDamage <= 0 || context.Battle == null)
                return;

            // Heal attacker's hero
            var attackerPlayer = context.Battle.TryGetPlayerState(context.Attacker.OwnerIndex);
            if (attackerPlayer != null)
            {
                attackerPlayer.HeroHealth += context.FinalDamage;
            }
        }
    }
}
