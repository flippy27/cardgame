using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Service layer for player item inventory.
    /// Wraps InventoryApiClient with caching and convenience helpers.
    /// Register in GameBootstrap: ServiceLocator.Register&lt;InventoryService&gt;(...)
    /// </summary>
    public sealed class InventoryService
    {
        public const string CardDustKey = "card_dust";

        private readonly InventoryApiClient _apiClient;
        private readonly AuthService _authService;

        // Keyed by itemTypeKey
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _cache = new();
        private DateTimeOffset _cacheTime = DateTimeOffset.MinValue;
        private const int CACHE_MINUTES = 5;

        public InventoryService(InventoryApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Returns all player items keyed by itemTypeKey.
        /// Cached 5 minutes. Returns empty dict if not authenticated.
        /// </summary>
        public async Task<Dictionary<string, InventoryApiClient.PlayerItemDto>> GetInventoryAsync()
        {
            if (!_authService.IsAuthenticated) return new();

            var now = DateTimeOffset.UtcNow;
            if (_cache.Count > 0 && (now - _cacheTime).TotalMinutes < CACHE_MINUTES)
                return _cache;

            try
            {
                var inv = await _apiClient.FetchInventory(_authService.CurrentPlayerId);
                _cache.Clear();
                if (inv?.items != null)
                    foreach (var item in inv.items)
                        _cache[item.itemTypeKey] = item;
                _cacheTime = now;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Inventory] Fetch failed: {ex.Message}");
            }

            return _cache;
        }

        /// <summary>Returns current quantity for a specific item. 0 if not owned.</summary>
        public async Task<int> GetItemBalanceAsync(string itemTypeKey)
        {
            var inv = await GetInventoryAsync();
            return inv.TryGetValue(itemTypeKey, out var item) ? item.quantity : 0;
        }

        /// <summary>Returns card_dust balance.</summary>
        public Task<int> GetCardDustAsync() => GetItemBalanceAsync(CardDustKey);

        /// <summary>
        /// Returns true if the player has enough of every item in the cost list.
        /// </summary>
        public bool CanAfford(
            (string itemTypeKey, int quantity)[] costs,
            Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            foreach (var (key, qty) in costs)
            {
                inventory.TryGetValue(key, out var bal);
                if ((bal?.quantity ?? 0) < qty) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if the player can afford all crafting requirements.
        /// </summary>
        public bool CanAffordCraft(
            CraftingApiClient.CraftingRequirementDto[] requirements,
            Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            if (requirements == null) return true;
            foreach (var req in requirements)
            {
                inventory.TryGetValue(req.itemTypeKey, out var bal);
                if ((bal?.quantity ?? 0) < req.quantityRequired) return false;
            }
            return true;
        }

        public void InvalidateCache()
        {
            _cache.Clear();
            _cacheTime = DateTimeOffset.MinValue;
        }

        /// <summary>Updates cache from a partial update list (e.g. returned after crafting).</summary>
        public void ApplyPartialUpdate(InventoryApiClient.PlayerItemDto[] updatedItems)
        {
            if (updatedItems == null) return;
            foreach (var item in updatedItems)
                _cache[item.itemTypeKey] = item;
        }
    }
}
