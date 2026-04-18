namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Categorizes skills by their function and behavior.
    /// Helps organize and filter skills for different card types.
    /// </summary>
    public enum SkillType
    {
        /// <summary>Defensive skills: armor, shield, evasion, reflection, dodge</summary>
        Defensive,

        /// <summary>Offensive skills: poison, stun, leech, mana_burn, enrage</summary>
        Offensive,

        /// <summary>Equipable skills: weapon/armor cards that grant abilities when equipped</summary>
        Equipable,

        /// <summary>Utility skills: regenerate, charge, taunt</summary>
        Utility,

        /// <summary>Modifier skills: change how the unit attacks (melee_range, cleave, diagonal_attack, etc)</summary>
        Modifier
    }
}
