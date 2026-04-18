namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Represents an effect that a skill can apply at various points in the battle pipeline.
    /// A single skill can have multiple effects.
    /// </summary>
    public interface ISkillEffect
    {
        /// <summary>Event this effect responds to.</summary>
        SkillTrigger Trigger { get; }

        /// <summary>Priority for execution order (higher executes first).</summary>
        int Priority { get; }

        /// <summary>Execute the effect on the given context.</summary>
        void Apply(SkillContext context);
    }
}
