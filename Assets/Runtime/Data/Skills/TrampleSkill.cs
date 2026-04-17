using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data.Skills
{
    /// <summary>
    /// Trample skill: Ignores target armor.
    /// Damage bypasses armor calculation completely.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Trample", fileName = "Skill_Trample")]
    public sealed class TrampleSkill : CardSkillDefinition
    {
        public override int GetArmorAbsorption(CardRuntime attacker, CardRuntime defender, int damage)
        {
            // Trample ignores armor - return 0 absorption
            return 0;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            if (defender?.Armor > 0)
            {
                return $"{attacker.DisplayName} tramples through {defender.DisplayName}'s armor!";
            }
            return null;
        }
    }
}
