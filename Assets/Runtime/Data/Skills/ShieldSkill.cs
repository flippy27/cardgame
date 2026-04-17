using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Skill/Shield", fileName = "Skill_Shield")]
    public sealed class ShieldSkill : CardSkillDefinition
    {
        public ShieldSkill()
        {
            skillId = "shield";
        }

        public override bool BlocksDamage(CardRuntime attacker, CardRuntime defender)
        {
            if (defender != null && defender.HasShield)
            {
                return true;
            }
            return false;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} was protected by Shield!";
        }
    }
}
