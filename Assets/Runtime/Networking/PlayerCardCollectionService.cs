using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Service layer for server-owned player card instances.
    /// The client caches and displays data; it does not invent upgrade costs.
    /// </summary>
    public sealed class PlayerCardCollectionService
    {
        private readonly PlayerCardsApiClient _apiClient;
        private readonly AuthService _authService;

        private PlayerCardsApiClient.PlayerCardSummaryDto _cachedSummary;
        private DateTimeOffset _summaryCacheTime = DateTimeOffset.MinValue;
        private readonly Dictionary<string, PlayerCardsApiClient.PlayerCardDetailDto> _detailCache = new();
        private readonly Dictionary<string, DateTimeOffset> _detailCacheTimes = new();

        private const int SummaryCacheMinutes = 10;
        private const int DetailCacheMinutes = 5;

        public PlayerCardCollectionService(PlayerCardsApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task<PlayerCardsApiClient.PlayerCardSummaryDto> GetSummaryAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            if (_cachedSummary != null && (now - _summaryCacheTime).TotalMinutes < SummaryCacheMinutes)
            {
                return _cachedSummary;
            }

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

        public async Task<List<PlayerCardsApiClient.PlayerCardDto>> GetCardsAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                return new List<PlayerCardsApiClient.PlayerCardDto>();
            }

            try
            {
                var result = await _apiClient.FetchCards(_authService.CurrentPlayerId);
                return new List<PlayerCardsApiClient.PlayerCardDto>(result?.cards ?? Array.Empty<PlayerCardsApiClient.PlayerCardDto>());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] FetchCards failed: {ex.Message}");
                return new List<PlayerCardsApiClient.PlayerCardDto>();
            }
        }

        public async Task<PlayerCardsApiClient.PlayerCardDetailDto> GetCardDetailAsync(string playerCardId)
        {
            if (!_authService.IsAuthenticated || string.IsNullOrWhiteSpace(playerCardId))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            if (_detailCache.TryGetValue(playerCardId, out var cached) &&
                _detailCacheTimes.TryGetValue(playerCardId, out var cachedAt) &&
                (now - cachedAt).TotalMinutes < DetailCacheMinutes)
            {
                return cached;
            }

            try
            {
                var detail = await _apiClient.FetchCardDetail(_authService.CurrentPlayerId, playerCardId);
                if (detail != null)
                {
                    _detailCache[playerCardId] = detail;
                    _detailCacheTimes[playerCardId] = now;
                }

                return detail;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] FetchCardDetail failed for {playerCardId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies a backend-approved upgrade request. The client does not calculate
        /// costs or touch inventory; the backend must own those decisions.
        /// </summary>
        public async Task<(bool success, string message, PlayerCardsApiClient.PlayerCardDetailDto updatedCard)>
            ApplyUpgradeAsync(string playerCardId, PlayerCardsApiClient.ApplyUpgradeRequestDto upgradeRequest)
        {
            if (!_authService.IsAuthenticated)
            {
                return (false, "Not authenticated", null);
            }

            try
            {
                var upgradeResult = await _apiClient.ApplyUpgrade(_authService.CurrentPlayerId, playerCardId, upgradeRequest);
                if (upgradeResult == null)
                {
                    return (false, "Upgrade failed: no response from server.", null);
                }

                _detailCache.Remove(playerCardId);
                _detailCacheTimes.Remove(playerCardId);
                InvalidateSummaryCache();
                var updatedCard = await GetCardDetailAsync(playerCardId);
                return (true, "Upgrade applied successfully.", updatedCard);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Collection] ApplyUpgrade failed: {ex.Message}");
                return (false, $"Upgrade failed: {ex.Message}", null);
            }
        }

        public bool HasAnyCards()
        {
            return _cachedSummary != null && _cachedSummary.totalCopies > 0;
        }

        public void InvalidateSummaryCache()
        {
            _cachedSummary = null;
            _detailCache.Clear();
            _detailCacheTimes.Clear();
            _summaryCacheTime = DateTimeOffset.MinValue;
        }
    }
}
