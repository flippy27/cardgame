using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    [System.Serializable]
    public sealed class DeckDto
    {
        public string deckId;
        public string playerId;
        public string deckName;
        public string displayName;
        public string description;
        public List<string> cardIds;
        public long createdAt;
        public long updatedAt;
        public bool isActive;
    }

    [System.Serializable]
    public sealed class DeckUpsertRequestDto
    {
        public string playerId;
        public string deckId;
        public string displayName;
        public List<string> cardIds;
    }

    [System.Serializable]
    public sealed class ServerCardDefinition
    {
        public string cardId;
        public string name;
        public string displayName;
        public string description;
        public int manaCost;
        public int attack;
        public int health;
        public string cardType;
        public string rarity;
        public string[] abilities;
    }

    [System.Serializable]
    public sealed class UserProfileDto
    {
        public string userId;
        public string username;
        public string email;
        public int rating;
        public int wins;
        public int losses;
        public string createdAt;
        public string lastLoginAt;
    }

    [System.Serializable]
    public sealed class UserStatsDto
    {
        public string userId;
        public string username;
        public int totalGames;
        public int wins;
        public int losses;
        public float winRate;
        public int rating;
        public string region;
    }

    [System.Serializable]
    public sealed class CardStatsDto
    {
        public string cardId;
        public int timesPlayed;
        public int winRate;
        public int totalCards;
        public int cardsWithAbilities;
        public float manaCostAvg;
        public float attackAvg;
        public float healthAvg;
    }

    [System.Serializable]
    public sealed class LeaderboardDto
    {
        public string userId;
        public string username;
        public int rank;
        public int wins;
    }

    [System.Serializable]
    public sealed class LeaderboardPageDto
    {
        public int page;
        public int pageSize;
        public int totalCount;
        public List<LeaderboardDto> entries;
    }

    [System.Serializable]
    public sealed class QueueForMatchRequestDto
    {
        public string playerId;
        public string deckId;
        public int mode;
        public int rating;
    }

    [System.Serializable]
    public sealed class CreatePrivateMatchRequestDto
    {
        public string playerId;
        public string deckId;
        public string matchName;
    }

    [System.Serializable]
    public sealed class JoinPrivateMatchRequestDto
    {
        public string playerId;
        public string deckId;
        public string roomCode;
    }

    [System.Serializable]
    public sealed class ConnectMatchRequestDto
    {
        public string playerId;
        public string matchId;
        public string reconnectToken;
    }

    [System.Serializable]
    public sealed class SetReadyRequestDto
    {
        public string matchId;
        public string playerId;
        public bool isReady;
    }

    [System.Serializable]
    public sealed class PlayCardRequestDto
    {
        public string matchId;
        public string playerId;
        public string runtimeHandKey;
        public int slotIndex;
    }

    [System.Serializable]
    public sealed class EndTurnRequestDto
    {
        public string matchId;
        public string playerId;
    }

    [System.Serializable]
    public sealed class ForfeitRequestDto
    {
        public string matchId;
        public string playerId;
    }
}
