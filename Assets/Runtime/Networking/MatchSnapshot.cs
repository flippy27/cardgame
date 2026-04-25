using System;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Networking.ApiClients;

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
        public string rulesetId;
        public GameRulesDto rules;
        public int phase; // MatchPhase: WaitingForPlayers=0, WaitingForReady=1, InProgress=2, Completed=3, Abandoned=4
        public int localSeatIndex = -1;
        public int activeSeatIndex = -1;
        public string activePlayerId;
        public bool isLocalPlayersTurn;
        public int turnNumber;
        public int connectedPlayers;
        public int? winnerSeatIndex;
        public int matchSeed;
        public double reconnectGraceRemainingSeconds;
        public string statusMessage;
        public SeatSnapshot[] seats;
        public string[] logs;
        public BattleEventSnapshot[] battleEvents;
        public bool duelEnded;
    }

    [System.Serializable]
    public sealed class SeatSnapshot
    {
        public int seatIndex;
        public string playerId;
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
        public int armor;
        public int unitType = -1;
        public string attackDeliveryType;
        public bool canBePlayedInFront;
        public bool canBePlayedInBack;
        public CardAbilitySnapshot[] abilities;
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
        public int attackMotionLevel;
        public int attackShakeLevel;
        public string attackDeliveryType;
        public int attack;
        public int currentHealth;
        public int maxHealth;
        public int armor;
        public int unitType = -1;
        public int slot; // BoardSlot
        public StatusEffectSnapshot[] statusEffects;
        public CardAbilitySnapshot[] abilities;
    }

    [System.Serializable]
    public sealed class BattleEventSnapshot
    {
        public string eventId;
        public int sequence = -1;
        public string kind;
        public int sourceSeatIndex = -1;
        public string sourceRuntimeId;
        public int targetSeatIndex = -1;
        public string targetRuntimeId;
        public string abilityId;
        public int effectKind = -1;
        public int amount;
        public int secondaryAmount;
        public int hpBefore;
        public int hpAfter;
        public int armorBefore;
        public int armorAfter;
        public int statusKind = -1;
        public int durationTurns;
        public string message;
    }

    [System.Serializable]
    public sealed class StatusEffectSnapshot
    {
        public int kind = -1;
        public int amount;
        public int remainingTurns;
        public string sourceRuntimeId;
        public string abilityId;
    }

    [System.Serializable]
    public sealed class CardAbilitySnapshot
    {
        public string abilityId;
        public string displayName;
        public int skillType = -1;
        public int triggerKind = -1;
        public int targetSelectorKind = -1;
        public string animationCueId;
        public string conditionsJson;
        public string metadataJson;
        public CardEffectSnapshot[] effects;
    }

    [System.Serializable]
    public sealed class CardEffectSnapshot
    {
        public int effectKind = -1;
        public int amount;
        public int secondaryAmount;
        public int durationTurns;
        public int targetSelectorKindOverride = -1;
        public int sequence;
        public string metadataJson;
    }
}
