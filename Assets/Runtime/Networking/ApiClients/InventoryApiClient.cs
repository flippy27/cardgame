using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// Inventory endpoints: GET/POST player item balances.
    ///
    /// Base path: /api/v1/players/{userId}/inventory
    /// Auth: JWT Bearer required for all except GET /api/v1/items
    /// </summary>
    public sealed class InventoryApiClient
    {
        private readonly string _baseUrl;

        public InventoryApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/players/{userId}/inventory
        public async Task<PlayerInventoryDto> FetchInventory(string userId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/inventory");
            return JsonUtility.FromJson<PlayerInventoryDto>(json);
        }

        // GET /api/v1/players/{userId}/inventory/{itemTypeKey}
        // Returns quantity: 0 if the player has never received this item.
        public async Task<PlayerItemDto> FetchItem(string userId, string itemTypeKey)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/players/{userId}/inventory/{itemTypeKey}");
            return JsonUtility.FromJson<PlayerItemDto>(json);
        }

        // GET /api/v1/items — public, no auth
        public async Task<ItemTypeDto[]> FetchItemTypes()
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/items");
            var w = JsonUtility.FromJson<ItemTypeListWrapper>($"{{\"items\":{json}}}");
            return w?.items ?? Array.Empty<ItemTypeDto>();
        }

        // ---- DTOs ----

        [Serializable]
        public sealed class PlayerInventoryDto
        {
            public string userId;
            public PlayerItemDto[] items;
        }

        [Serializable]
        public sealed class PlayerItemDto
        {
            public string id;
            public string userId;
            public int itemTypeId;
            public string itemTypeKey;       // "card_dust"
            public string itemTypeDisplayName; // "Card Dust"
            public string itemTypeCategory;  // "crafting" | "faction" | "upgrade"
            public int quantity;
            public string createdAt;
            public string updatedAt;
        }

        [Serializable]
        public sealed class ItemTypeDto
        {
            public int id;
            public string key;
            public string displayName;
            public string description;
            public string category;
            public int maxStack;
            public bool isActive;
            public string iconAssetRef;
        }

        [Serializable]
        private sealed class ItemTypeListWrapper { public ItemTypeDto[] items; }
    }
}
