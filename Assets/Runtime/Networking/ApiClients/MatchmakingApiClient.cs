using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class MatchmakingApiClient
    {
        public enum QueueMode
        {
            Casual = 0,
            Ranked = 1
        }

        private readonly string _baseUrl;

        public MatchmakingApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task JoinQueue(QueueMode mode)
        {
            var modeStr = mode == QueueMode.Casual ? "casual" : "ranked";
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/matchmaking/queue?mode={modeStr}", "{}");
        }

        public async Task LeaveQueue()
        {
            await HttpClientHelper.DeleteAsync($"{_baseUrl}/api/v1/matchmaking/queue");
        }

        public async Task<MatchmakingStatusDto> GetStatus()
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/matchmaking/status");
            return JsonUtility.FromJson<MatchmakingStatusDto>(json);
        }

        [System.Serializable]
        public sealed class MatchmakingStatusDto
        {
            public bool IsSearching;
            public int QueueMode;
            public int TimeInQueueSeconds;
            public int EstimatedWaitSeconds;
            public int PlayersInQueue;
            public string OpponentId;
            public string MatchId;
        }
    }
}
