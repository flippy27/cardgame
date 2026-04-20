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
        public int level;
        public int rating;
    }

    [System.Serializable]
    public sealed class UserStatsDto
    {
        public int totalMatches;
        public int wins;
        public int losses;
        public float winRate;
        public int rating;
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
}
