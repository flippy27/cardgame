namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>ManaBurn: Enemy loses 1 mana when attacked.</summary>
    public class ManaBurnEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Defender == null || context.Battle == null)
                return;

            var defenderPlayer = context.Battle.GetPlayerState(context.Defender.OwnerIndex);
            defenderPlayer.Mana -= 1;
            if (defenderPlayer.Mana < 0)
                defenderPlayer.Mana = 0;
        }
    }
}
