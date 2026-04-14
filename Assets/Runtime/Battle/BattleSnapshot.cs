using System;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Battle
{
    [Serializable]
    public sealed class DuelSnapshotDto
    {
        public int snapshotVersion = CardConstants.SnapshotVersion;
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
        public int playerIndex;
        public int heroHealth;
        public int mana;
        public int maxMana;
        public CardInHandDto[] hand;
        public BoardSlotSnapshotDto[] board;
        public int remainingDeckCount;
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
        public bool canBePlayedInFront;
        public bool canBePlayedInBack;
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
    }
}
