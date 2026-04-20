using Flippy.CardDuelMobile.Battle.Abilities;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Dodge: Ranged attacks cannot target this card.</summary>
    public class DodgeEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnValidateAttack;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            if (context.Attacker == null || context.Defender == null)
                return;

            // Check if attacker is ranged
            var unitType = context.Attacker.Definition?.unitType ?? UnitType.Melee;
            if (unitType == UnitType.Ranged)
            {
                context.IsValidAttack = false;
            }
        }
    }
}
