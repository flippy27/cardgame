using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class MatchHistoryService
    {
        private readonly MatchHistoryApiClient _apiClient;
        private readonly AuthService _authService;
        private Dictionary<string, List<MatchHistoryEntry>> _cache = new();

        public MatchHistoryService(MatchHistoryApiClient apiClient = null, AuthService authService = null)
        {
            _apiClient = apiClient ?? new MatchHistoryApiClient();
            _authService = authService ?? new AuthService();
        }

        public async Task<MatchHistoryPage> FetchHistory(
            string playerId,
            int page = 1,
            int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ValidationException("PlayerId required.");

            var cacheKey = $"{playerId}_{page}_{pageSize}";

            if (_cache.ContainsKey(cacheKey))
            {
                return new MatchHistoryPage
                {
                    page = page,
                    pageSize = pageSize,
                    matches = _cache[cacheKey]
                };
            }

            try
            {
                var pageDto = await _apiClient.FetchHistory(playerId, page, pageSize);

                var entries = new List<MatchHistoryEntry>();
                if (pageDto.matches != null)
                {
                    foreach (var match in pageDto.matches)
                    {
                        entries.Add(new MatchHistoryEntry
                        {
                            matchId = match.matchId,
                            opponentId = match.opponentId,
                            result = match.result,
                            mode = match.mode,
                            durationSeconds = match.durationSeconds,
                            ratingBefore = match.ratingBefore ?? 0,
                            ratingAfter = match.ratingAfter ?? 0,
                            ratingDelta = (match.ratingAfter ?? 0) - (match.ratingBefore ?? 0),
                            createdAt = match.createdAt
                        });
                    }
                }

                _cache[cacheKey] = entries;

                return new MatchHistoryPage
                {
                    page = page,
                    pageSize = pageSize,
                    totalCount = pageDto.totalCount,
                    matches = entries
                };
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidGameStateException("Unauthorized. Log in first.");
            }
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        public (int wins, int losses, int total) GetWinRateFromCache(string playerId)
        {
            var wins = 0;
            var losses = 0;

            foreach (var kvp in _cache)
            {
                if (!kvp.Key.StartsWith(playerId))
                    continue;

                foreach (var match in kvp.Value)
                {
                    if (match.result == "win") wins++;
                    else if (match.result == "loss") losses++;
                }
            }

            return (wins, losses, wins + losses);
        }
    }

    [System.Serializable]
    public sealed class MatchHistoryPage
    {
        public int page;
        public int pageSize;
        public int totalCount;
        public List<MatchHistoryEntry> matches;
    }

    [System.Serializable]
    public sealed class MatchHistoryEntry
    {
        public string matchId;
        public string opponentId;
        public string result;
        public string mode;
        public int durationSeconds;
        public int ratingBefore;
        public int ratingAfter;
        public int ratingDelta;
        public long createdAt;
    }
}
