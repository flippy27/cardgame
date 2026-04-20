using System;
using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Matches API DTO for game state snapshot.
    /// Mirrors CardDuel.ServerApi.Game.MatchSnapshot.
    /// </summary>
    [System.Serializable]
    public sealed class MatchSnapshot
    {
        public string matchId;
        public string roomCode;
        public int mode; // QueueMode: Casual=0, Ranked=1, Private=2
        public int phase; // MatchPhase: WaitingForPlayers=0, WaitingForReady=1, InProgress=2, Completed=3, Abandoned=4
        public int localSeatIndex;
        public int activeSeatIndex;
        public int turnNumber;
        public int connectedPlayers;
        public int? winnerSeatIndex;
        public int matchSeed;
        public double reconnectGraceRemainingSeconds;
        public string statusMessage;
        public SeatSnapshot[] seats;
        public string[] logs;
        public bool duelEnded;
    }

    [System.Serializable]
    public sealed class SeatSnapshot
    {
        public int seatIndex;
        public bool connected;
        public bool ready;
        public int heroHealth;
        public int mana;
        public int maxMana;
        public int remainingDeckCount;
        public HandCardSnapshot[] hand;
        public BoardSlotSnapshot[] board;
    }

    [System.Serializable]
    public sealed class HandCardSnapshot
    {
        public string runtimeHandKey;
        public string cardId;
        public string displayName;
        public int manaCost;
        public int attack;
        public int health;
        public bool canBePlayedInFront;
        public bool canBePlayedInBack;
    }

    [System.Serializable]
    public sealed class BoardSlotSnapshot
    {
        public int slot; // BoardSlot: Front=0, BackLeft=1, BackRight=2
        public bool occupied;
        public BoardCardSnapshot occupant;
    }

    [System.Serializable]
    public sealed class BoardCardSnapshot
    {
        public string runtimeId;
        public string cardId;
        public string displayName;
        public int ownerSeatIndex;
        public int attack;
        public int currentHealth;
        public int maxHealth;
        public int armor;
        public int slot; // BoardSlot
    }
}
