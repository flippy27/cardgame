using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data.Skills
{
    /// <summary>
    /// Melee Range skill: Front card with this skill can attack Back cards.
    /// Allows breaking through ranged defense.
    /// Front attacks Back (any available) instead of Front.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Melee Range", fileName = "Skill_MeleeRange")]
    public sealed class MeleeRangeSkill : CardSkillDefinition
    {
        public override bool CanAttack(CardRuntime attacker, CardRuntime defender, int damage, out int modifiedDamage)
        {
            modifiedDamage = damage;
            // Melee Range allows attacking back cards
            return true;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker.DisplayName} uses melee range to reach {defender.DisplayName}!";
        }
    }
}
