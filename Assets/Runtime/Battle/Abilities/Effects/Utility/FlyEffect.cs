using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>Fly: Only flying units can attack this card.</summary>
    public class FlyEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnValidateAttack;
        public int Priority => 100;

        public void Apply(AbilityContext context)
        {
            // Fly: Only flying units can attack this card.
            if (context.Defender == null || context.Attacker == null)
                return;

            // Check if defender has Fly ability
            var defenderHasFly = HasAbility(context.Defender, "fly");
            var attackerHasFly = HasAbility(context.Attacker, "fly");

            // Block attack if defender has fly but attacker doesn't
            if (defenderHasFly && !attackerHasFly)
            {
                context.IsValidAttack = false;
            }
        }

        private bool HasAbility(CardRuntime card, string abilityId)
        {
            if (card?.Definition?.abilities == null)
                return false;

            return System.Array.Exists(card.Definition.abilities, a => a?.abilityId == abilityId);
        }
    }
}
