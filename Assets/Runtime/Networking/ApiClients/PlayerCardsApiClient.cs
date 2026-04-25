using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// Player card collection and upgrade endpoints.
    ///
    /// Base path: /api/v1/players/{userId}/cards
    /// Auth: JWT Bearer required.
    /// </summary>
    public sealed class PlayerCardsApiClient
    {
        private readonly string _baseUrl;

        public PlayerCardsApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/players/{userId}/cards
        public async Task<PlayerCardCollectionDto> FetchCards(string userId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/cards");
            return JsonUtility.FromJson<PlayerCardCollectionDto>(json);
        }

        // GET /api/v1/players/{userId}/cards/summary
        // Grouped by card type — use this for the collection album screen.
        public async Task<PlayerCardSummaryDto> FetchSummary(string userId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/cards/summary");
            return JsonUtility.FromJson<PlayerCardSummaryDto>(json);
        }

        // GET /api/v1/players/{userId}/cards/{playerCardId}
        // Full detail: base stats, effective stats, upgrade history.
        public async Task<PlayerCardDetailDto> FetchCardDetail(string userId, string playerCardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/cards/{playerCardId}");
            return JsonUtility.FromJson<PlayerCardDetailDto>(json);
        }

        // GET /api/v1/players/{userId}/cards/by-card/{cardId}
        public async Task<PlayerCardDto[]> FetchByCardId(string userId, string cardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/cards/by-card/{cardId}");
            var w = JsonUtility.FromJson<PlayerCardListWrapper>($"{{\"cards\":{json}}}");
            return w?.cards ?? Array.Empty<PlayerCardDto>();
        }

        // POST /api/v1/players/{userId}/cards/{playerCardId}/upgrades
        public async Task<PlayerCardUpgradeDto> ApplyUpgrade(string userId, string playerCardId, ApplyUpgradeRequestDto request)
        {
            var body = JsonUtility.ToJson(request);
            var json = await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/players/{userId}/cards/{playerCardId}/upgrades", body);
            return JsonUtility.FromJson<PlayerCardUpgradeDto>(json);
        }

        // GET /api/v1/players/{userId}/cards/{playerCardId}/upgrades
        public async Task<PlayerCardUpgradeDto[]> FetchUpgrades(string userId, string playerCardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/cards/{playerCardId}/upgrades");
            var w = JsonUtility.FromJson<UpgradeListWrapper>($"{{\"upgrades\":{json}}}");
            return w?.upgrades ?? Array.Empty<PlayerCardUpgradeDto>();
        }

        // ---- DTOs ----

        [Serializable]
        public sealed class PlayerCardDto
        {
            public string id;               // player-card UUID
            public string userId;
            public string cardDefinitionId;
            public string cardId;           // "ember_vanguard"
            public string displayName;
            public int cardRarity;          // CardRarity int
            public int cardFaction;         // CardFaction int
            public int cardType;            // CardType int
            public string acquiredFrom;     // "crafted" | "starter_pack" | etc.
            public string acquiredAt;
        }

        [Serializable]
        public sealed class PlayerCardCollectionDto
        {
            public string userId;
            public int totalCards;
            public PlayerCardDto[] cards;
        }

        [Serializable]
        public sealed class PlayerCardSummaryDto
        {
            public string userId;
            public int uniqueCardTypes;
            public int totalCopies;
            public PlayerCardSummaryEntryDto[] cards;
        }

        [Serializable]
        public sealed class PlayerCardSummaryEntryDto
        {
            public string cardId;           // "ember_vanguard"
            public string displayName;
            public int ownedCopies;
            public PlayerCardDto[] ownedInstances;
        }

        [Serializable]
        public sealed class PlayerCardDetailDto
        {
            public string id;
            public string userId;
            public string cardDefinitionId;
            public string cardId;
            public string displayName;
            public string description;
            public int manaCost;
            public int baseAttack;
            public int baseHealth;
            public int baseArmor;
            public int cardRarity;
            public int cardFaction;
            public int cardType;
            public int unitType;
            public string acquiredFrom;
            public string acquiredAt;
            public int effectiveAttack;
            public int effectiveHealth;
            public int effectiveArmor;
            public int level;
            public PlayerCardUpgradeDto[] upgrades;
        }

        [Serializable]
        public sealed class PlayerCardUpgradeDto
        {
            public string id;
            public string playerCardId;
            public string upgradeKind;     // "attack_bonus" | "health_bonus" | etc.
            public int intValue;           // 0 when null from server
            public string stringValue;     // ability_id for "added_ability"
            public string appliedAt;
            public string appliedBy;
            public string note;
        }

        [Serializable]
        public sealed class ApplyUpgradeRequestDto
        {
            public string upgradeKind;
            public int intValue;
            public string stringValue;
            public string appliedBy;
            public string note;
        }

        // ---- Internal wrappers ----
        [Serializable] private sealed class PlayerCardListWrapper { public PlayerCardDto[] cards; }
        [Serializable] private sealed class UpgradeListWrapper { public PlayerCardUpgradeDto[] upgrades; }
    }
}
