using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class ReplayApiClient
    {
        private readonly string _baseUrl;

        public ReplayApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task<MatchReplayDto> FetchReplay(string matchId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/replays/{matchId}");
            return JsonUtility.FromJson<MatchReplayDto>(json);
        }

        public async Task<ReplayValidationResponseDto> ValidateReplay(string matchId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/replays/{matchId}/validate");
            return JsonUtility.FromJson<ReplayValidationResponseDto>(json);
        }

        [System.Serializable]
        public sealed class MatchReplayDto
        {
            public string matchId;
            public System.Collections.Generic.List<object> actions;
        }

        [System.Serializable]
        public sealed class ReplayValidationResponseDto
        {
            public bool valid;
        }
    }
}
