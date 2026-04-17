using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Applies poison stacks to target. Target takes poisonStacks damage at turn start.
    /// Applied in BattleContext.DealDamage after damage is applied.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Poison", fileName = "Skill_Poison")]
    public sealed class PoisonSkill : CardSkillDefinition
    {
        public PoisonSkill()
        {
            skillId = "poison";
        }

        [Min(1)] public int poisonStacks = 2;

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} was poisoned!";
        }
    }
}
