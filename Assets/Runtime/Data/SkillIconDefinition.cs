using UnityEngine;
using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Maps skill IDs to their icon textures for UI display.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Skill Icon Definition", fileName = "SkillIconDefinition")]
    public sealed class SkillIconDefinition : ScriptableObject
    {
        [System.Serializable]
        public class SkillIconEntry
        {
            public string skillId;
            public Texture2D icon;
        }

        public List<SkillIconEntry> skillIcons = new();

        /// <summary>Get icon for a skill, or null if not found.</summary>
        public Texture2D GetIcon(string skillId)
        {
            foreach (var entry in skillIcons)
            {
                if (entry.skillId == skillId && entry.icon != null)
                {
                    return entry.icon;
                }
            }
            return null;
        }

        /// <summary>Get icons for multiple skills.</summary>
        public Texture2D[] GetIcons(string[] skillIds)
        {
            var icons = new List<Texture2D>();
            foreach (var skillId in skillIds)
            {
                var icon = GetIcon(skillId);
                if (icon != null)
                {
                    icons.Add(icon);
                }
            }
            return icons.ToArray();
        }
    }
}
