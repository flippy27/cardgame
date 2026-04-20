namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Represents an effect that an ability can apply at various points in the battle pipeline.
    /// A single ability can have multiple effects.
    /// </summary>
    public interface IAbilityEffect
    {
        /// <summary>Event this effect responds to.</summary>
        AbilityTrigger Trigger { get; }

        /// <summary>Priority for execution order (higher executes first).</summary>
        int Priority { get; }

        /// <summary>Execute the effect on the given context.</summary>
        void Apply(AbilityContext context);
    }
}
