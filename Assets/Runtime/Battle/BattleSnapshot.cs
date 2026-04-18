using System;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Battle
{
    [Serializable]
    public sealed class DuelSnapshotDto
    {
        public int snapshotVersion = CardConstants.SnapshotVersion;
        public string matchId = string.Empty;
        public int localPlayerIndex;
        public int activePlayerIndex;
        public int turnNumber;
        public bool duelEnded;
        public DuelEndReason endReason;
        public MatchPhase matchPhase;
        public bool localPlayerReady;
        public bool remotePlayerReady;
        public bool localPlayerConnected;
        public bool remotePlayerConnected;
        public int connectedPlayers;
        public int winnerPlayerIndex = -1;
        public int matchSeed;
        public float reconnectGraceRemainingSeconds;
        public string statusMessage;
        public PlayerSnapshotDto[] players;
        public List<BattleLogEntry> logs;
    }

    [Serializable]
    public sealed class PlayerSnapshotDto
    {
        public string playerId = string.Empty;
        public int playerIndex;
        public int heroHealth;
        public int mana;
        public int maxMana;
        public int rating = 1000;
        public CardInHandDto[] hand;
        public BoardSlotSnapshotDto[] board;
        public int remainingDeckCount;
        public int deadCardPileCount;
    }

    [Serializable]
    public sealed class CardInHandDto
    {
        public string runtimeCardKey;
        public string cardId;
        public string displayName;
        public int manaCost;
        public int attack;
        public int health;
        public bool isUnit;
        public int unitType; // 0=Melee, 1=Ranged, 2=Magic
    }

    [Serializable]
    public sealed class BoardSlotSnapshotDto
    {
        public BoardSlot slot;
        public bool occupied;
        public BoardCardDto occupant;
    }

    [Serializable]
    public sealed class BoardCardDto
    {
        public string runtimeId;
        public string cardId;
        public string displayName;
        public int ownerIndex;
        public int attack;
        public int currentHealth;
        public int maxHealth;
        public int armor;
        public BoardSlot slot;
        public bool canAttack;
        public int unitType; // 0=Melee, 1=Ranged
        public int turnsUntilCanAttack;
    }
}
