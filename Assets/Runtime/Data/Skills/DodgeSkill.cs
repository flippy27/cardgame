using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Ranged units cannot directly target this card. Only melee can hit it.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Dodge", fileName = "Skill_Dodge")]
    public sealed class DodgeSkill : CardSkillDefinition
    {
        public override bool BlocksDamage(CardRuntime attacker, CardRuntime defender)
        {
            if (defender == null || attacker == null)
                return false;

            if (attacker.CurrentSlot == BoardSlot.Front)
                return false;

            return true;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} dodged the ranged attack!";
        }
    }
}
