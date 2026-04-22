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
            Ranked = 1,
            Private = 2
        }

        private readonly string _baseUrl;

        public MatchmakingApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Queue for a match (Casual or Ranked). Auth token auto-added from SecureTokenStorage.
        /// POST /api/v1/matchmaking/queue
        /// </summary>
        public async Task<MatchReservationDto> QueueForMatch(string playerId, string deckId, QueueMode mode, int rating)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matchmaking/queue";
                var request = JsonUtility.ToJson(new QueueForMatchRequestDto
                {
                    playerId = playerId,
                    deckId = deckId,
                    mode = (int)mode,
                    rating = rating
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchReservationDto>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchmakingApiClient", $"QueueForMatch failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create private match. Auth token auto-added from SecureTokenStorage.
        /// POST /api/v1/matchmaking/private
        /// </summary>
        public async Task<MatchReservationDto> CreatePrivateMatch(string playerId, string deckId, string matchName)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matchmaking/private";
                var request = JsonUtility.ToJson(new CreatePrivateMatchRequestDto
                {
                    playerId = playerId,
                    deckId = deckId,
                    matchName = matchName
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchReservationDto>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchmakingApiClient", $"CreatePrivateMatch failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Join private match by code. Auth token auto-added from SecureTokenStorage.
        /// POST /api/v1/matchmaking/private/join
        /// </summary>
        public async Task<MatchReservationDto> JoinPrivateMatch(string playerId, string deckId, string roomCode)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matchmaking/private/join";
                var request = JsonUtility.ToJson(new JoinPrivateMatchRequestDto
                {
                    playerId = playerId,
                    deckId = deckId,
                    roomCode = roomCode
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchReservationDto>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchmakingApiClient", $"JoinPrivateMatch failed: {ex.Message}");
                throw;
            }
        }

        [System.Serializable]
        public sealed class MatchReservationDto
        {
            public string matchId;
            public string roomCode;
            public string reconnectToken;
            public int seatIndex;
            public int mode; // QueueMode
            public bool waitingForOpponent;
            public string status;
            public string rulesetId;
            public GameRulesDto rules;
        }

    }
}
