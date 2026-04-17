using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Hits all enemies in same row.
    /// Front hits all 3 enemy positions.
    /// BackLeft/BackRight hits both back positions.
    /// Requires CleaveTargetSelector to work properly.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Cleave", fileName = "Skill_Cleave")]
    public sealed class CleaveSkill : CardSkillDefinition
    {
        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName} unleashed Cleave!";
        }
    }
}
