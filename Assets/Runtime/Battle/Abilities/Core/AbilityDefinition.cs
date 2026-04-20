using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Defines a skill and its effects.
    /// Maps a skill ID to a collection of effects that execute at different triggers.
    /// </summary>
    public class AbilityDefinition
    {
        public string AbilityId { get; set; }
        public List<IAbilityEffect> Effects { get; set; } = new();

        public AbilityDefinition(string skillId)
        {
            AbilityId = skillId;
        }

        public void AddEffect(IAbilityEffect effect)
        {
            if (effect != null)
                Effects.Add(effect);
        }
    }
}
