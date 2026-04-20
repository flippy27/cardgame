using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class MatchApiClient
    {
        private readonly string _baseUrl;

        public MatchApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task RecordMatchAction(string matchId, object action)
        {
            var json = JsonUtility.ToJson(action);
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/matches/{matchId}/actions", json);
        }

        public async Task CompleteMatch(string matchId, object result)
        {
            var json = JsonUtility.ToJson(result);
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/matches/{matchId}/complete", json);
        }

        public async Task PostCheckpoint(string matchId, PostCheckpointRequestDto request)
        {
            var json = JsonUtility.ToJson(request);
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/matches/{matchId}/checkpoint", json);
        }

        [System.Serializable]
        public sealed class PostCheckpointRequestDto
        {
            public string matchId;
            public string playerId;
            public object snapshot;
            public int checkpointNumber;
            public int sequence;
            public string timestamp;
        }
    }
}
