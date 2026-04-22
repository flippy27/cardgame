using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    [System.Serializable]
    public sealed class GameRulesSeatOverrideDto
    {
        public int seatIndex;
        public int additionalHeroHealth;
        public int additionalMaxHeroHealth;
        public int additionalStartingMana;
        public int additionalMaxMana;
        public int additionalManaPerTurn;
        public int additionalCardsDrawnOnTurnStart;
    }

    [System.Serializable]
    public sealed class GameRulesDto
    {
        public string rulesetId;
        public string rulesetKey;
        public string displayName;
        public string description;
        public bool isActive;
        public bool isDefault;
        public int startingHeroHealth;
        public int maxHeroHealth;
        public int startingMana;
        public int maxMana;
        public int manaGrantedPerTurn;
        public int manaGrantTiming;
        public int initialDrawCount;
        public int cardsDrawnOnTurnStart;
        public int startingSeatIndex;
        public GameRulesSeatOverrideDto[] seatOverrides;

        public ResolvedSeatRules ResolveSeatRules(int seatIndex)
        {
            var resolved = new ResolvedSeatRules
            {
                seatIndex = seatIndex,
                heroHealth = startingHeroHealth,
                maxHeroHealth = maxHeroHealth,
                startingMana = startingMana,
                maxMana = maxMana,
                manaGrantedPerTurn = manaGrantedPerTurn,
                cardsDrawnOnTurnStart = cardsDrawnOnTurnStart
            };

            if (seatOverrides == null)
            {
                return resolved;
            }

            for (var index = 0; index < seatOverrides.Length; index++)
            {
                var seatOverride = seatOverrides[index];
                if (seatOverride == null || seatOverride.seatIndex != seatIndex)
                {
                    continue;
                }

                resolved.heroHealth += seatOverride.additionalHeroHealth;
                resolved.maxHeroHealth += seatOverride.additionalMaxHeroHealth;
                resolved.startingMana += seatOverride.additionalStartingMana;
                resolved.maxMana += seatOverride.additionalMaxMana;
                resolved.manaGrantedPerTurn += seatOverride.additionalManaPerTurn;
                resolved.cardsDrawnOnTurnStart += seatOverride.additionalCardsDrawnOnTurnStart;
            }

            resolved.heroHealth = UnityEngine.Mathf.Max(1, resolved.heroHealth);
            resolved.maxHeroHealth = UnityEngine.Mathf.Max(resolved.heroHealth, resolved.maxHeroHealth);
            resolved.startingMana = UnityEngine.Mathf.Max(0, resolved.startingMana);
            resolved.maxMana = UnityEngine.Mathf.Max(resolved.startingMana, resolved.maxMana);
            resolved.manaGrantedPerTurn = UnityEngine.Mathf.Max(0, resolved.manaGrantedPerTurn);
            resolved.cardsDrawnOnTurnStart = UnityEngine.Mathf.Max(0, resolved.cardsDrawnOnTurnStart);
            return resolved;
        }
    }

    public sealed class ResolvedSeatRules
    {
        public int seatIndex;
        public int heroHealth;
        public int maxHeroHealth;
        public int startingMana;
        public int maxMana;
        public int manaGrantedPerTurn;
        public int cardsDrawnOnTurnStart;
    }

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
