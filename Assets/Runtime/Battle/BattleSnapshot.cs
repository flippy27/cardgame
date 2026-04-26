using System;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Battle
{
    [Serializable]
    public sealed class DuelSnapshotDto
    {
        public int snapshotVersion = CardConstants.SnapshotVersion;
        public string matchId = string.Empty;
        public string rulesetId = string.Empty;
        public string rulesetName = string.Empty;
        public GameRulesDto rules;
        public int localPlayerIndex;
        public int activePlayerIndex;
        public string activePlayerId = string.Empty;
        public bool isLocalPlayersTurn;
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
        public int localHeroMaxHealth = 20;
        public int remoteHeroMaxHealth = 20;
        public float reconnectGraceRemainingSeconds;
        public string statusMessage;
        public PlayerSnapshotDto[] players;
        public List<BattleLogEntry> logs;
        public BattleEventDto[] battleEvents;
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
        public int armor;
        public bool isUnit;
        public int unitType; // 0=Melee, 1=Ranged, 2=Magic
        public string attackDeliveryType;
        public CardAbilityDto[] abilities;
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
        public int manaCost;
        public int attackMotionLevel;
        public int attackShakeLevel;
        public string attackDeliveryType;
        public int ownerIndex;
        public int attack;
        public int currentHealth;
        public int maxHealth;
        public int armor;
        public BoardSlot slot;
        public bool canAttack;
        public int unitType; // 0=Melee, 1=Ranged
        public int turnsUntilCanAttack;
        public StatusEffectDto[] statusEffects;
        public CardAbilityDto[] abilities;
    }

    [Serializable]
    public sealed class BattleEventDto
    {
        public string eventId = string.Empty;
        public int sequence = -1;
        public string kind = string.Empty;

        // Seat indexes are converted to visual player indexes by SnapshotConverter.
        public int sourceSeatIndex = -1;
        public int targetSeatIndex = -1;
        public int serverSourceSeatIndex = -1;
        public int serverTargetSeatIndex = -1;

        public string sourceRuntimeId = string.Empty;
        public string targetRuntimeId = string.Empty;
        public string abilityId = string.Empty;
        public int effectKind = -1;
        public int amount;
        public int secondaryAmount;
        public int hpBefore;
        public int hpAfter;
        public int armorBefore;
        public int armorAfter;
        public int statusKind = -1;
        public int durationTurns;
        public string message = string.Empty;
    }

    [Serializable]
    public sealed class StatusEffectDto
    {
        public int kind = -1;
        public int amount;
        public int remainingTurns;
        public string sourceRuntimeId = string.Empty;
        public string abilityId = string.Empty;
        public string iconAssetRef = string.Empty;
    }

    [Serializable]
    public sealed class CardAbilityDto
    {
        public string abilityId = string.Empty;
        public string displayName = string.Empty;
        public string iconAssetRef = string.Empty;
        public int skillType = -1;
        public int triggerKind = -1;
        public int targetSelectorKind = -1;
        public string animationCueId = string.Empty;
        public string conditionsJson = string.Empty;
        public string metadataJson = string.Empty;
        public CardEffectDto[] effects;
    }

    [Serializable]
    public sealed class CardEffectDto
    {
        public int effectKind = -1;
        public int amount;
        public int secondaryAmount;
        public int durationTurns;
        public int targetSelectorKindOverride = -1;
        public int sequence;
        public string metadataJson = string.Empty;
    }
}
