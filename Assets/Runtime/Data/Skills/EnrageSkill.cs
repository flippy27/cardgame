using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Gains +1 attack when damaged. Tracked in CardRuntime.EnrageBonus.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Enrage", fileName = "Skill_Enrage")]
    public sealed class EnrageSkill : CardSkillDefinition
    {
        public EnrageSkill()
        {
            skillId = "enrage";
        }

        [Min(1)] public int bonusPerHit = 1;

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return null;
        }
    }
}
