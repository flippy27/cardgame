using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class MatchHistoryApiClient
    {
        private readonly string _baseUrl;

        public MatchHistoryApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task<MatchHistoryPageDto> FetchHistory(string playerId, int page = 1, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ValidationException("PlayerId required.");

            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/matches/history/{playerId}?page={page}&pageSize={pageSize}");
            return JsonUtility.FromJson<MatchHistoryPageDto>(json);
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
            public string result;
            public string mode;
            public int durationSeconds;
            public int? ratingBefore;
            public int? ratingAfter;
            public long createdAt;
        }
    }
}
