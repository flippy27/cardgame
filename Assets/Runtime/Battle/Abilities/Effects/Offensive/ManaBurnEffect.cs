using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>ManaBurn: Enemy loses 1 mana when attacked.</summary>
    public class ManaBurnEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageDealt;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null || context.Battle == null)
                return;

            var defenderPlayer = context.Battle.TryGetPlayerState(context.Defender.OwnerIndex);
            if (defenderPlayer != null)
            {
                defenderPlayer.Mana -= 1;
                if (defenderPlayer.Mana < 0)
                    defenderPlayer.Mana = 0;
            }
        }
    }
}
