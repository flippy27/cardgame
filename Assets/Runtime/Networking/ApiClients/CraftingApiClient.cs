using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using System.Linq;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// Crafting endpoints.
    ///
    ///   GET  /api/v1/crafting/cards              → CraftableCardDto[]
    ///   GET  /api/v1/crafting/cards/{cardId}     → CraftableCardDto
    ///   POST /api/v1/crafting/cards/{cardId}     → CraftCardResponseDto  (no body — userId from JWT)
    ///
    /// CraftCardResponseDto embeds PlayerCardDto (PlayerCardsApiClient) and PlayerItemDto[] (InventoryApiClient).
    /// Those types must be in scope since all ApiClients share the same namespace.
    /// </summary>
    public sealed class CraftingApiClient
    {
        private readonly string _baseUrl;

        public CraftingApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/crafting/cards
        public async Task<CraftableCardDto[]> FetchCraftableCards()
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/crafting/cards");
            Debug.Log($"crafitng cards {json}");
            CraftableCardListWrapper w = JsonUtility.FromJson<CraftableCardListWrapper>($"{{\"cards\":{json}}}");
            Debug.Log($"array  {w.cards.Count()}");
            return w?.cards ?? Array.Empty<CraftableCardDto>();
        }

        // GET /api/v1/crafting/cards/{cardId}
        public async Task<CraftableCardDto> FetchCraftableCard(string cardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/crafting/cards/{cardId}");
            return JsonUtility.FromJson<CraftableCardDto>(json);
        }

        // POST /api/v1/crafting/cards/{cardId}   — no body, userId resolved server-side from JWT
        public async Task<CraftCardResponseDto> CraftCard(string cardId)
        {
            var json = await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/crafting/cards/{cardId}", "{}");
            return JsonUtility.FromJson<CraftCardResponseDto>(json);
        }

        // ---- DTOs ----

        [Serializable]
        public sealed class CraftableCardDto
        {
            public string cardId;
            public string displayName;
            public int cardRarity;
            public bool isCraftable;
            public CraftingRequirementDto[] requirements;
        }

        [Serializable]
        public sealed class CraftingRequirementDto
        {
            public string id;
            public string cardDefinitionId;
            public int itemTypeId;
            public string itemTypeKey;           // "card_dust"
            public string itemTypeDisplayName;   // "Card Dust"
            public int quantityRequired;
        }

        [Serializable]
        public sealed class CraftCardResponseDto
        {
            public bool success;
            public string message;
            // Embedded from other clients — same namespace, no extra using needed.
            public PlayerCardsApiClient.PlayerCardDto playerCard;
            public InventoryApiClient.PlayerItemDto[] updatedInventory;
        }

        // ---- Internal wrapper ----
        [Serializable] private sealed class CraftableCardListWrapper { public CraftableCardDto[] cards; }
    }
}
