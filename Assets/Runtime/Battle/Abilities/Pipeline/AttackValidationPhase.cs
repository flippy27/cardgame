using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Validates if an attack is valid.
    /// Skills can block or allow attacks based on position, status, etc.
    /// </summary>
    public class AttackValidationPhase : IAbilityPhase
    {
        public bool CanExecute(AbilityContext context)
        {
            return context.Trigger == AbilityTrigger.OnValidateAttack;
        }

        public AbilityContext Execute(AbilityContext context)
        {
            var attacker = context.Attacker;

            // Only units fight.
            if (attacker?.Definition == null || attacker.Definition.cardType != CardType.Unit)
            {
                context.IsValidAttack = false;
                return context;
            }

            // Stunned units skip their attack.
            if (attacker.Stunned)
            {
                context.IsValidAttack = false;
                return context;
            }

            // Summoning sickness: a card cannot attack the turn it was played. Haste sets
            // TurnsUntilCanAttack to 0 on play; the counter ticks down at the owner's turn start.
            if (attacker.TurnsUntilCanAttack > 0)
            {
                context.IsValidAttack = false;
                return context;
            }

            // Position rule (mydocs): melee attacks only from Front; ranged/magic only from a back slot.
            if (!IsValidAttackPosition(attacker))
            {
                context.IsValidAttack = false;
                return context;
            }

            // Run all registered validation effects for both attacker and defender
            var validationEffects = AbilityRegistry.GetAllEffects(AbilityTrigger.OnValidateAttack);
            foreach (var effect in validationEffects)
            {
                effect.Apply(context);
                if (!context.IsValidAttack)
                    return context;
            }

            return context;
        }

        // Melee may only attack from Front; Ranged/Magic only from a back slot. Mirrors the server
        // engine (MatchEngine.CanAttackFromCurrentSlot) so single-player matches multiplayer.
        private static bool IsValidAttackPosition(CardRuntime attacker)
        {
            switch (attacker.Definition.unitType)
            {
                case UnitType.Melee:
                    return attacker.CurrentSlot == BoardSlot.Front;
                case UnitType.Ranged:
                case UnitType.Magic:
                    return attacker.CurrentSlot == BoardSlot.BackLeft || attacker.CurrentSlot == BoardSlot.BackRight;
                default:
                    return false;
            }
        }
    }
}
