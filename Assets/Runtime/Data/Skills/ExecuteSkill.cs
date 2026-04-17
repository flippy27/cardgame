using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Deals bonus damage proportional to target's missing health.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Execute", fileName = "Skill_Execute")]
    public sealed class ExecuteSkill : CardSkillDefinition
    {
        [Range(0, 200)] public int bonusPercentMissing = 50;

        public override int ModifyDamage(CardRuntime attacker, CardRuntime defender, int baseDamage, bool ignoreArmor)
        {
            if (defender == null || defender.MaxHealth <= 0)
                return baseDamage;

            var missing = defender.MaxHealth - defender.CurrentHealth;
            if (missing <= 0)
                return baseDamage;

            var bonus = (missing * bonusPercentMissing) / 100;
            return baseDamage + bonus;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName} executed {defender?.DisplayName} with Execute damage!";
        }
    }
}
