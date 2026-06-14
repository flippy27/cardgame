using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// Salvage (disenchant) endpoints — mirrors the server's api/v1/salvage contract.
    ///
    ///   GET  /api/v1/salvage/cards/{cardId}  → CardSalvageInfoDto   (preview, anonymous)
    ///   GET  /api/v1/salvage/salvageable     → SalvageableSummaryDto (auth)
    ///   POST /api/v1/salvage/cards/{cardId}  → SalvageCardResponse   (auth; no body, userId from JWT)
    ///
    /// On a blocked salvage the response has success=false, errorCode="card_in_deck" and decks[]
    /// listing the saved decks still using the card (for the deep-link-to-deck flow).
    /// All DTOs are camelCase JsonUtility mirrors; enum-ish fields are plain ints.
    /// </summary>
    public sealed class SalvageApiClient
    {
        private readonly string _baseUrl;

        public SalvageApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/salvage/cards/{cardId}
        public async Task<CardSalvageInfoDto> FetchSalvagePreview(string cardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/salvage/cards/{cardId}");
            return JsonUtility.FromJson<CardSalvageInfoDto>(json);
        }

        // GET /api/v1/salvage/salvageable
        public async Task<SalvageableSummaryDto> FetchSalvageable()
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/salvage/salvageable");
            return JsonUtility.FromJson<SalvageableSummaryDto>(json);
        }

        // POST /api/v1/salvage/cards/{cardId}  — no body, userId resolved server-side from JWT
        public async Task<SalvageCardResponse> SalvageCard(string cardId)
        {
            var json = await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/salvage/cards/{cardId}", "{}");
            return JsonUtility.FromJson<SalvageCardResponse>(json);
        }

        // ---- DTOs (mirror Contracts/SalvageDtos.cs) ----

        [Serializable]
        public sealed class SalvageYieldDto
        {
            public string id;
            public string cardDefinitionId;
            public int itemTypeId;
            public string itemTypeKey;           // "card_dust"
            public string itemTypeDisplayName;   // "Card Dust"
            public int itemTypeRarity;           // 0 Common .. 3 Legendary
            public string itemTypeCategory;      // crafting | faction | upgrade | legendary
            public string itemTypeIconAssetRef;  // "ui/items/card_dust"
            public int quantity;
        }

        [Serializable]
        public sealed class CardSalvageInfoDto
        {
            public string cardId;
            public string displayName;
            public int cardRarity;
            public int cardFaction;
            public int cardType;
            public bool isSalvageable;
            public SalvageYieldDto[] yields;
        }

        [Serializable]
        public sealed class DeckReferenceDto
        {
            public string deckId;
            public string displayName;
            public int copiesInDeck;
        }

        [Serializable]
        public sealed class SalvageCardResponse
        {
            public bool success;
            public string message;
            public string errorCode;             // e.g. "card_in_deck" when blocked
            public string salvagedPlayerCardId;
            public SalvageYieldDto[] grantedMaterials;
            public InventoryApiClient.PlayerItemDto[] updatedInventory;
            public DeckReferenceDto[] decks;      // populated when errorCode == "card_in_deck"
        }

        [Serializable]
        public sealed class SalvageableCardDto
        {
            public string cardId;
            public string displayName;
            public int cardRarity;
            public int cardFaction;
            public int cardType;
            public int ownedCopies;
            public bool lockedInDeck;
            public SalvageYieldDto[] yields;
        }

        [Serializable]
        public sealed class SalvageableSummaryDto
        {
            public string userId;
            public int totalSalvageableCopies;
            public SalvageableCardDto[] cards;
        }
    }
}
