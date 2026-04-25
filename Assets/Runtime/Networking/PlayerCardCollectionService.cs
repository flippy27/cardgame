using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Service layer for the player's card collection.
    /// Wraps PlayerCardsApiClient with caching and upgrade helpers.
    ///
    /// Primary flow for the collection screen:
    ///   GetSummaryAsync() → shows album grouped by card type with copy counts
    ///   GetCardDetailAsync(playerCardId) → shows stats + upgrade history
    ///   ApplyUpgradeAsync(...) → consumes items then applies upgrade server-side
    ///
    /// NOTE: The upgrade flow is non-atomic per the current API contract:
    ///   1. client calls InventoryService to verify balance
    ///   2. client calls the backend to apply the upgrade (server does NOT deduct items here)
    ///   3. client calls InventoryService.Consume() separately if the server adds that endpoint
    ///   See contract section §4 for details.
    ///   When a server-atomic upgrade endpoint is available, replace ApplyUpgradeAsync.
    /// </summary>
    public sealed class PlayerCardCollectionService
    {
        private readonly PlayerCardsApiClient _apiClient;
        private readonly AuthService _authService;

        private PlayerCardsApiClient.PlayerCardSummaryDto _cachedSummary;
        private DateTimeOffset _summaryCacheTime = DateTimeOffset.MinValue;
        // Per-card detail cache (playerCardId → detail)
        private Dictionary<string, PlayerCardsApiClient.PlayerCardDetailDto> _detailCache = new();
        private const int CACHE_MINUTES = 10;
        private const int DETAIL_CACHE_MINUTES = 5;

        public PlayerCardCollectionService(PlayerCardsApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Returns the player's collection grouped by card type.
        /// Each entry contains owned copy count and instance UUIDs.
        /// Cached 10 minutes.
        /// </summary>
        public async Task<PlayerCardsApiClient.PlayerCardSummaryDto> GetSummaryAsync()
        {
            if (!_authService.IsAuthenticated) return null;

            var now = DateTimeOffset.UtcNow;
            if (_cachedSummary != null && (now - _summaryCacheTime).TotalMinutes < CACHE_MINUTES)
                return _cachedSummary;

            try
            {
                _cachedSummary = await _apiClient.FetchSummary(_authService.CurrentPlayerId);
                _summaryCacheTime = now;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] FetchSummary failed: {ex.Message}");
            }

            return _cachedSummary;
        }

        /// <summary>
        /// Returns flat list of all owned card instances.
        /// </summary>
        public async Task<List<PlayerCardsApiClient.PlayerCardDto>> GetCardsAsync()
        {
            if (!_authService.IsAuthenticated) return new();

            try
            {
                var result = await _apiClient.FetchCards(_authService.CurrentPlayerId);
                return new List<PlayerCardsApiClient.PlayerCardDto>(result?.cards ?? Array.Empty<PlayerCardsApiClient.PlayerCardDto>());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] FetchCards failed: {ex.Message}");
                return new();
            }
        }

        /// <summary>
        /// Returns full detail for a specific owned card instance including upgrades and effective stats.
        /// Cached 5 minutes per playerCardId.
        /// </summary>
        public async Task<PlayerCardsApiClient.PlayerCardDetailDto> GetCardDetailAsync(string playerCardId)
        {
            if (!_authService.IsAuthenticated) return null;

            var now = DateTimeOffset.UtcNow;
            if (_detailCache.TryGetValue(playerCardId, out var cached))
            {
                // Simple TTL: invalidate detail cache when summary cache refreshes
                if ((now - _summaryCacheTime).TotalMinutes < DETAIL_CACHE_MINUTES)
                    return cached;
            }

            try
            {
                var detail = await _apiClient.FetchCardDetail(_authService.CurrentPlayerId, playerCardId);
                _detailCache[playerCardId] = detail;
                return detail;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] FetchCardDetail failed for {playerCardId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies an upgrade to a player-owned card.
        /// IMPORTANT: This does NOT deduct items. The current API contract requires the client
        /// to deduct items separately before calling this. Pass the InventoryService and costs
        /// — this method will verify balance then call consume → apply in sequence.
        ///
        /// If the server adds an atomic endpoint later, update this method.
        /// </summary>
        public async Task<(bool success, string message, PlayerCardsApiClient.PlayerCardDetailDto updatedCard)>
            ApplyUpgradeAsync(
                string playerCardId,
                PlayerCardsApiClient.ApplyUpgradeRequestDto upgradeRequest,
                InventoryService inventoryService,
                (string itemTypeKey, int quantity)[] itemCosts)
        {
            if (!_authService.IsAuthenticated)
                return (false, "Not authenticated", null);

            // 1. Verify balance
            var inventory = await inventoryService.GetInventoryAsync();
            if (!inventoryService.CanAfford(itemCosts, inventory))
            {
                var deficits = BuildDeficitMessage(itemCosts, inventory);
                return (false, $"Insufficient items: {deficits}", null);
            }

            // 2. Apply the upgrade server-side
            // NOTE: per contract, server doesn't deduct items here
            PlayerCardsApiClient.PlayerCardUpgradeDto upgradeResult;
            try
            {
                upgradeResult = await _apiClient.ApplyUpgrade(_authService.CurrentPlayerId, playerCardId, upgradeRequest);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] ApplyUpgrade failed: {ex.Message}");
                return (false, $"Upgrade failed: {ex.Message}", null);
            }

            if (upgradeResult == null)
                return (false, "Upgrade failed: no response from server", null);

            // 3. Invalidate caches
            _detailCache.Remove(playerCardId);
            InvalidateSummaryCache();
            inventoryService.InvalidateCache();

            // 4. Fetch updated card detail
            var updatedCard = await GetCardDetailAsync(playerCardId);
            return (true, "Upgrade applied successfully.", updatedCard);
        }

        public bool HasAnyCards() => _cachedSummary != null && _cachedSummary.totalCopies > 0;

        public void InvalidateSummaryCache()
        {
            _cachedSummary = null;
            _detailCache.Clear();
            _summaryCacheTime = DateTimeOffset.MinValue;
        }

        private string BuildDeficitMessage(
            (string itemTypeKey, int quantity)[] costs,
            Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            var parts = new System.Text.StringBuilder();
            foreach (var (key, qty) in costs)
            {
                inventory.TryGetValue(key, out var bal);
                int have = bal?.quantity ?? 0;
                if (have < qty)
                    parts.Append($"{key}: need {qty}, have {have}. ");
            }
            return parts.ToString().TrimEnd();
        }
    }
}
