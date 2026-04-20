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
}
