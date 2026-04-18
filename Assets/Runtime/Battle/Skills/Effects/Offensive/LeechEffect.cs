namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Leech: Heals attacker equal to damage dealt.</summary>
    public class LeechEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Attacker == null || context.FinalDamage <= 0 || context.Battle == null)
                return;

            // Heal attacker's hero
            var attackerPlayer = context.Battle.GetPlayerState(context.Attacker.OwnerIndex);
            attackerPlayer.HeroHealth += context.FinalDamage;
        }
    }
}
