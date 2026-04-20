using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Armor: Reduces incoming damage before health.</summary>
    public class ArmorEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageCalculation;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            if (context.Defender == null || context.Defender.Armor <= 0)
                return;

            // Skip if attacker has trample
            if (context.IgnoreArmor)
                return;

            // Reduce damage by armor amount
            int armorAbsorbed = System.Math.Min(context.Defender.Armor, context.FinalDamage);
            context.FinalDamage -= armorAbsorbed;
            context.Defender.Armor -= armorAbsorbed;
        }
    }
}
