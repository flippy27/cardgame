using System;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Ubicación disponible en el tablero de cada jugador.
    /// </summary>
    public enum BoardSlot
    {
        Front = 0,
        BackLeft = 1,
        BackRight = 2
    }

    /// <summary>
    /// Rareza visual / de colección.
    /// </summary>
    public enum CardRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    /// <summary>
    /// Facción temática para contenido.
    /// </summary>
    public enum CardFaction
    {
        Ember = 0,
        Tidal = 1,
        Grove = 2,
        Alloy = 3,
        Void = 4
    }

    /// <summary>
    /// Tipo de objetivo de un efecto.
    /// </summary>
    public enum TargetSide
    {
        Self = 0,
        Ally = 1,
        Enemy = 2,
        Both = 3
    }

    /// <summary>
    /// Estado final del duelo.
    /// </summary>
    public enum DuelEndReason
    {
        None = 0,
        EnemyHeroDefeated = 1,
        LocalHeroDefeated = 2,
        OpponentDisconnected = 3
    }

    /// <summary>
    /// Mensajes de log del duelo.
    /// </summary>
    public enum BattleLogType
    {
        Info = 0,
        Attack = 1,
        Heal = 2,
        Summon = 3,
        Death = 4,
        Turn = 5
    }

    /// <summary>
    /// Identifica jugador A o B en una partida 1v1.
    /// </summary>
    public enum DuelPlayerIndex
    {
        PlayerA = 0,
        PlayerB = 1
    }

    /// <summary>
    /// Fase del flujo de match online.
    /// </summary>
    public enum MatchPhase
    {
        WaitingForPlayers = 0,
        WaitingForReady = 1,
        Starting = 2,
        InProgress = 3,
        Completed = 4,
        Abandoned = 5
    }

    /// <summary>
    /// Tipo de efecto de una habilidad. ESPEJO EXACTO del server
    /// (CardDuel.ServerApi <c>Game/MatchEngine.cs</c> enum EffectKind). Llega por wire como
    /// ENTERO en <c>CardEffectDto.effectKind</c> / <c>BattleEventDto.effectKind</c>; castea
    /// <c>(EffectKind)dto.effectKind</c> para leerlo. Mantener los valores idénticos al server.
    /// </summary>
    public enum EffectKind
    {
        Damage = 0,
        Heal = 1,
        GainArmor = 2,
        BuffAttack = 3,
        HitHero = 4,
        Stun = 5,
        Poison = 6,
        Leech = 7,
        Evasion = 8,
        Shield = 9,
        Reflection = 10,
        Dodge = 11,
        Enrage = 12,
        ManaBurn = 13,
        Regenerate = 14,
        Execute = 15,
        DiagonalAttack = 16,
        Fly = 17,
        Armor = 18,
        Chain = 19,
        Charge = 20,
        Cleave = 21,
        LastStand = 22,
        MeleeRange = 23,
        Ricochet = 24,
        Taunt = 25,
        Trample = 26,
        Haste = 27,
        AddShield = 28,
        ApplyPoison = 29,
        ApplyStun = 30,
        // ── Status-applying / cleansing effects (mirror StatusEffectKind 4..11). ──
        ApplyBurn = 31,          // damage-over-time, ignores armor (fire)
        ApplyRegeneration = 32,  // heal-over-time (ally)
        ApplyParalyze = 33,      // multi-turn skip-attack (hard control)
        ApplyConfuse = 34,       // attacks a random target (incl. allies)
        ApplySilence = 35,       // disables the unit's abilities/modifiers
        ApplyVulnerable = 36,    // target takes extra incoming damage
        ApplyWeaken = 37,        // target deals less attack/counter damage
        ApplyWard = 38,          // blocks the next debuff applied to target (immunity charge)
        Cleanse = 39,            // removes all debuffs from target (ally cure)
        Dispel = 40              // removes all buffs from target (enemy purge)
    }

    /// <summary>
    /// Estado persistente sobre una carta. ESPEJO EXACTO del server
    /// (CardDuel.ServerApi <c>Game/MatchEngine.cs</c> enum StatusEffectKind). Llega por wire como
    /// ENTERO en <c>StatusEffectDto.kind</c> / <c>BattleEventDto.statusKind</c>. Mantener idéntico.
    /// </summary>
    public enum StatusEffectKind
    {
        Poison = 0,
        Stun = 1,
        Shield = 2,
        EnrageCooldown = 3,
        Burn = 4,
        Regeneration = 5,
        Paralyze = 6,
        Confuse = 7,
        Silence = 8,
        Vulnerable = 9,
        Weaken = 10,
        Ward = 11
    }
}
