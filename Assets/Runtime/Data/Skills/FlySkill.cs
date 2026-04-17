using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data.Skills
{
    /// <summary>
    /// Fly skill: Only flying units can defend against flying attacks.
    /// If defender doesn't have Fly, damage goes to hero instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Fly", fileName = "Skill_Fly")]
    public sealed class FlySkill : CardSkillDefinition
    {
        public override bool BlocksDamage(CardRuntime attacker, CardRuntime defender)
        {
            // Only blocks if defender also has Fly
            if (defender == null) return false;
            return !HasSkill(defender, "fly");
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            if (defender != null && !HasSkill(defender, "fly"))
            {
                return $"{attacker.DisplayName} flies over {defender.DisplayName} - damage redirected to hero!";
            }
            return null;
        }

        private bool HasSkill(CardRuntime card, string skillId)
        {
            if (card?.Definition?.skills == null) return false;
            foreach (var skill in card.Definition.skills)
            {
                if (skill != null && skill.skillId == skillId)
                    return true;
            }
            return false;
        }
    }
}
