using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Skill/Evasion", fileName = "Skill_Evasion")]
    public sealed class EvasionSkill : CardSkillDefinition
    {
        [Range(0, 100)] public int evasionChance = 30;

        public override bool CanAttack(CardRuntime attacker, CardRuntime defender, int damage, out int modifiedDamage)
        {
            modifiedDamage = damage;
            if (defender != null && Random.Range(0, 100) < evasionChance)
            {
                return false;
            }
            return true;
        }

        public override string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return $"{defender?.DisplayName} dodged the attack!";
        }
    }
}
