using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Defines a skill and its effects.
    /// Maps a skill ID to a collection of effects that execute at different triggers.
    /// </summary>
    public class SkillDefinition
    {
        public string SkillId { get; set; }
        public List<ISkillEffect> Effects { get; set; } = new();

        public SkillDefinition(string skillId)
        {
            SkillId = skillId;
        }

        public void AddEffect(ISkillEffect effect)
        {
            if (effect != null)
                Effects.Add(effect);
        }
    }
}
