using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Descarga y cachea historial de matches desde API.
    /// GET /api/matches/history/{playerId}?page=&pageSize=
    /// </summary>
    public sealed class MatchHistoryService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private Dictionary<string, List<MatchHistoryEntry>> _cache = new();

        public MatchHistoryService(CardGameApiClient apiClient = null, AuthService authService = null)
        {
            _apiClient = apiClient ?? new CardGameApiClient();
            _authService = authService ?? new AuthService();
        }

        /// <summary>
        /// Obtiene página de historial para jugador.
        /// Cachea resultados por (playerId, page).
        /// </summary>
        public async Task<MatchHistoryPage> FetchHistory(
            string playerId,
            int page = 1,
            int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ValidationException("PlayerId required.");

            var cacheKey = $"{playerId}_{page}_{pageSize}";

            // Retry from cache if available
            if (_cache.ContainsKey(cacheKey))
            {
                return new MatchHistoryPage
                {
                    page = page,
                    pageSize = pageSize,
                    matches = _cache[cacheKey]
                };
            }

            // Fetch from API
            var url = $"{_apiClient.BaseUrl}/api/matches/history/{playerId}?page={page}&pageSize={pageSize}";
            using var request = UnityWebRequest.Get(url);

            // Add auth header if available
            var authHeader = _authService.GetAuthorizationHeader();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                request.SetRequestHeader("Authorization", authHeader);
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 401)
                {
                    throw new InvalidGameStateException("Unauthorized. Log in first.");
                }
                throw new InvalidOperationException(
                    $"Failed to fetch match history: {request.responseCode} - {request.error}");
            }

            var json = request.downloadHandler.text;
            var pageDto = JsonUtility.FromJson<MatchHistoryPageDto>(json);

            // Cache results
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

        /// <summary>
        /// Limpia cache (para reload forzado).
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Obtiene win rate del jugador de cache (requiere llamada previa a FetchHistory).
        /// </summary>
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
    public sealed class MatchHistoryPageDto
    {
        public int page;
        public int pageSize;
        public int totalCount;
        public MatchHistoryEntryDto[] matches;
    }

    [System.Serializable]
    public sealed class MatchHistoryEntryDto
    {
        public string matchId;
        public string opponentId;
        public string result; // "win", "loss", "draw"
        public string mode;
        public int durationSeconds;
        public int? ratingBefore;
        public int? ratingAfter;
        public string createdAt;
    }

    public sealed class MatchHistoryPage
    {
        public int page;
        public int pageSize;
        public int totalCount;
        public List<MatchHistoryEntry> matches;
    }

    public sealed class MatchHistoryEntry
    {
        public string matchId;
        public string opponentId;
        public string result; // "win", "loss", "draw"
        public string mode;
        public int durationSeconds;
        public int ratingBefore;
        public int ratingAfter;
        public int ratingDelta;
        public string createdAt;
    }
}
