using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// After killing a target, excess damage ricochets to adjacent card.
    /// TODO: Implement in ExecuteSlotAttack post-damage handling.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Ricochet", fileName = "Skill_Ricochet")]
    public sealed class RicochetSkill : CardSkillDefinition
    {
        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName}'s attack ricocheted!";
        }
    }
}
