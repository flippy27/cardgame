using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Hits all enemies of same type/faction.
    /// For now, hits all enemy cards (can refine to faction later).
    /// Requires ChainTargetSelector to work properly.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Chain", fileName = "Skill_Chain")]
    public sealed class ChainSkill : CardSkillDefinition
    {
        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName} chain attacked!";
        }
    }
}
