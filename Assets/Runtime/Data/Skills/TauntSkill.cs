using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// All enemy attacks must target this card if alive.
    /// Implemented in target selectors via FindTauntTarget().
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Taunt", fileName = "Skill_Taunt")]
    public sealed class TauntSkill : CardSkillDefinition
    {
        public TauntSkill()
        {
            skillId = "taunt";
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} taunted!";
        }
    }
}
