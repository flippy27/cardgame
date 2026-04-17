using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data.Skills
{
    /// <summary>
    /// Diagonal Attack skill: Ranged-only skill.
    /// Back cards with this skill attack diagonal back card instead of directly opposite.
    /// Front → BackLeft or BackRight (alternates or picks available)
    /// BackLeft → attacks BackRight
    /// BackRight → attacks BackLeft
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Diagonal Attack", fileName = "Skill_DiagonalAttack")]
    public sealed class DiagonalAttackSkill : CardSkillDefinition
    {
        public override bool CanAttack(CardRuntime attacker, CardRuntime defender, int damage, out int modifiedDamage)
        {
            modifiedDamage = damage;
            // Diagonal attack is valid - targeting is handled by TargetSelector
            return true;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker.DisplayName} attacks diagonally at {defender.DisplayName}!";
        }
    }
}
