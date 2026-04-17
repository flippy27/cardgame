using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Reflects a percentage of damage back to attacker.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Reflection", fileName = "Skill_Reflection")]
    public sealed class ReflectionSkill : CardSkillDefinition
    {
        [Range(0, 100)] public int reflectPercent = 50;

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} reflected damage!";
        }
    }
}
