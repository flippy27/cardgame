using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Gestiona leaderboard (ranking global de jugadores).
    /// </summary>
    public sealed class LeaderboardService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private Dictionary<(int page, int pageSize), LeaderboardPageDto> _cachedPages = new();
        private DateTimeOffset _lastLeaderboardFetch = DateTimeOffset.MinValue;
        private const int CACHE_MINUTES = 10;

        public LeaderboardService(CardGameApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Obtiene leaderboard (página específica).
        /// Cachea por 10 minutos.
        /// </summary>
        public async Task<LeaderboardPageDto> GetLeaderboardAsync(int page = 1, int pageSize = 100)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Leaderboard] Not authenticated");
                return null;
            }

            if (page < 1)
                page = 1;

            if (pageSize < 1 || pageSize > 1000)
                pageSize = 100;

            var cacheKey = (page, pageSize);
            var now = DateTimeOffset.UtcNow;

            // Return cached if fresh
            if (_cachedPages.TryGetValue(cacheKey, out var cached) &&
                (now - _lastLeaderboardFetch).TotalMinutes < CACHE_MINUTES)
            {
                Debug.Log($"[Leaderboard] Returned cached page {page}");
                return cached;
            }

            try
            {
                var leaderboard = await _apiClient.GetLeaderboardAsync(page, pageSize);
                _cachedPages[cacheKey] = leaderboard;
                _lastLeaderboardFetch = now;

                Debug.Log($"[Leaderboard] Fetched page {page}: {leaderboard.entries.Count} entries (Total: {leaderboard.totalCount})");
                return leaderboard;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Leaderboard] Failed to fetch: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene el top 10.
        /// </summary>
        public async Task<LeaderboardPageDto> GetTop10Async()
        {
            return await GetLeaderboardAsync(page: 1, pageSize: 10);
        }

        /// <summary>
        /// Obtiene el top 100.
        /// </summary>
        public async Task<LeaderboardPageDto> GetTop100Async()
        {
            return await GetLeaderboardAsync(page: 1, pageSize: 100);
        }

        /// <summary>
        /// Limpia la caché.
        /// </summary>
        public void ClearCache()
        {
            _cachedPages.Clear();
            _lastLeaderboardFetch = DateTimeOffset.MinValue;
            Debug.Log("[Leaderboard] Cache cleared");
        }

        /// <summary>
        /// Fuerza refetch (ignora caché).
        /// </summary>
        public async Task<LeaderboardPageDto> RefreshAsync(int page = 1, int pageSize = 100)
        {
            _cachedPages.Clear();
            _lastLeaderboardFetch = DateTimeOffset.MinValue;
            return await GetLeaderboardAsync(page, pageSize);
        }
    }
}
