using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Stuns target for next turn (cannot attack).
    /// Applied in BattleContext.DealDamage.
    /// Cleared in ProcessStatusEffects.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Stun", fileName = "Skill_Stun")]
    public sealed class StunSkill : CardSkillDefinition
    {
        public StunSkill()
        {
            skillId = "stun";
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} was stunned!";
        }
    }
}
