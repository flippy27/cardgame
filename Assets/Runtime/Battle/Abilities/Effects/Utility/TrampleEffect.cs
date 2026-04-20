using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Trample: Ignores target armor.</summary>
    public class TrampleEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageCalculation;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            context.IgnoreArmor = true;
        }
    }
}
