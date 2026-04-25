using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Service layer for card crafting.
    /// Depends on InventoryService for affordability checks and cache updates.
    ///
    /// Craft flow:
    ///   1. GetCraftableCardsAsync() — fetch what can be crafted
    ///   2. InventoryService.CanAffordCraft(recipe.requirements, inventory) — check balance
    ///   3. CraftCardAsync(cardId) — server atomically deducts items + creates player card
    ///   4. inventoryService.ApplyPartialUpdate(result.updatedInventory) — update cached balances
    ///   5. playerCardCollectionService.InvalidateSummaryCache() — new card in collection
    /// </summary>
    public sealed class CraftingService
    {
        private readonly CraftingApiClient _apiClient;
        private readonly AuthService _authService;

        private List<CraftingApiClient.CraftableCardDto> _craftableCards = new();
        private DateTimeOffset _recipeCacheTime = DateTimeOffset.MinValue;
        private const int CACHE_MINUTES = 10;

        public CraftingService(CraftingApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Returns all craftable cards with their item requirements.
        /// Cached 10 minutes.
        /// </summary>
        public async Task<List<CraftingApiClient.CraftableCardDto>> GetCraftableCardsAsync()
        {
            var now = DateTimeOffset.UtcNow;
            if (_craftableCards.Count > 0 && (now - _recipeCacheTime).TotalMinutes < CACHE_MINUTES)
                return _craftableCards;

            try
            {
                var arr = await _apiClient.FetchCraftableCards();
                _craftableCards = new List<CraftingApiClient.CraftableCardDto>(arr);
                _recipeCacheTime = now;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Crafting] FetchCraftableCards failed: {ex.Message}");
            }

            return _craftableCards;
        }

        /// <summary>
        /// Crafts a card. Server atomically checks requirements, deducts items, creates PlayerCard.
        /// On success: call inventoryService.ApplyPartialUpdate + collectionService.InvalidateSummaryCache.
        /// Throws on network error. Returns unsuccessful result on 409 (insufficient items).
        /// </summary>
        public async Task<CraftingApiClient.CraftCardResponseDto> CraftCardAsync(string cardId)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidOperationException("Not authenticated");

            return await _apiClient.CraftCard(cardId);
        }

        public void InvalidateCache()
        {
            _craftableCards.Clear();
            _recipeCacheTime = DateTimeOffset.MinValue;
        }
    }
}
