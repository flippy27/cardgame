using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Costs enemy 1 mana when attacking.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Mana Burn", fileName = "Skill_ManaBurn")]
    public sealed class ManaBurnSkill : CardSkillDefinition
    {
        public ManaBurnSkill()
        {
            skillId = "mana_burn";
        }

        [Min(1)] public int manaCost = 1;

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{attacker?.DisplayName} burned {defender?.OwnerIndex} mana!";
        }
    }
}
