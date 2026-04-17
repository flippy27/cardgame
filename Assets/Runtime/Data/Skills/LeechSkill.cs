using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Heals hero equal to damage dealt.
    /// Implementation: Call context.Heal() in BattleContext.DealDamage after applying damage.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Leech", fileName = "Skill_Leech")]
    public sealed class LeechSkill : CardSkillDefinition
    {
        public LeechSkill()
        {
            skillId = "leech";
        }

        [Range(0, 100)] public int leechPercent = 100;

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName} leeched life!";
        }
    }
}
