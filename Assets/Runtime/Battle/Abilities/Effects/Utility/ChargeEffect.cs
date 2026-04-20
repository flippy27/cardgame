using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Charge: Can attack the turn it's played.</summary>
    public class ChargeEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnCardInitialize;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            // Charge would need to mark card as able to attack immediately
            // Requires changes to attack flow
        }
    }
}
