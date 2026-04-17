using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Deals double damage if this card is the only one on its team's board.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Last Stand", fileName = "Skill_LastStand")]
    public sealed class LastStandSkill : CardSkillDefinition
    {
        public LastStandSkill()
        {
            skillId = "last_stand";
        }

        public override int ModifyDamage(CardRuntime attacker, CardRuntime defender, int baseDamage, bool ignoreArmor)
        {
            if (attacker == null)
                return baseDamage;

            return baseDamage * 2;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName} made a Last Stand!";
        }
    }
}
