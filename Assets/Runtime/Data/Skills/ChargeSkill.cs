using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Card can attack on the turn it's played (no waiting).
    /// Data-only marker. Requires changes to ExecuteSlotAttack to skip stunned check.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Charge", fileName = "Skill_Charge")]
    public sealed class ChargeSkill : CardSkillDefinition
    {
        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return null;
        }
    }
}
