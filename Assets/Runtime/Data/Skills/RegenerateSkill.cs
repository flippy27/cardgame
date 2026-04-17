using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Heals X HP at start of owner's turn.
    /// Typically paired with AbilityDefinition (OnTurnStart trigger) that calls context.Heal().
    /// This is a marker skill; healing is done via ability wrapper.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill/Regenerate", fileName = "Skill_Regenerate")]
    public sealed class RegenerateSkill : CardSkillDefinition
    {
        [Min(1)] public int healPerTurn = 2;

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return null;
        }
    }
}
