using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Base class for card combat skills (Fly, Trample, etc).
    /// Skills modify attack behavior, targeting, or damage.
    /// </summary>
    public abstract class CardSkillDefinition : ScriptableObject
    {
        public string skillId = "skill";
        public string displayName = "Skill";
        [TextArea] public string description;

        /// <summary>
        /// Called before damage is applied. Modify damage amount or determine if attack succeeds.
        /// Return true if attack should proceed, false to block.
        /// </summary>
        public virtual bool CanAttack(CardRuntime attacker, CardRuntime defender, int damage, out int modifiedDamage)
        {
            modifiedDamage = damage;
            return true;
        }

        /// <summary>
        /// Modify damage calculation (for Trample ignoring armor, etc).
        /// Only called if CanAttack returned true.
        /// </summary>
        public virtual int ModifyDamage(CardRuntime attacker, CardRuntime defender, int baseDamage, bool ignoreArmor)
        {
            return baseDamage;
        }

        /// <summary>
        /// Modify armor absorption (for Trample which ignores it).
        /// </summary>
        public virtual int GetArmorAbsorption(CardRuntime attacker, CardRuntime defender, int damage)
        {
            if (defender.Armor <= 0) return 0;
            return System.Math.Min(defender.Armor, damage);
        }

        /// <summary>
        /// Check if this skill blocks damage (e.g., Fly blocks non-flying attackers).
        /// Return true to block, false to allow damage.
        /// </summary>
        public virtual bool BlocksDamage(CardRuntime attacker, CardRuntime defender)
        {
            return false;
        }

        /// <summary>
        /// Get log message for this skill interaction.
        /// </summary>
        public virtual string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
        {
            return null; // No message by default
        }
    }
}
