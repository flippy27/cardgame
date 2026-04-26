using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// HTTP client for match gameplay operations.
    /// Handles PlayCard, EndTurn, SetReady, etc.
    /// </summary>
    public sealed class MatchplayApiClient
    {
        private readonly string _baseUrl;

        public MatchplayApiClient(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Get match snapshot for a player (auth token auto-added from SecureTokenStorage).
        /// GET /api/v1/matches/{matchId}/snapshot/{playerId}
        /// </summary>
        public async Task<MatchSnapshot> GetSnapshot(string matchId, string playerId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/snapshot/{playerId}";
                var response = await HttpClientHelper.GetAsync(url);
                return JsonUtility.FromJson<MatchSnapshot>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"GetSnapshot failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get persisted rules snapshot for a match from DB.
        /// GET /api/v1/matches/{matchId}/rules/{playerId}
        /// </summary>
        public async Task<GameRulesDto> GetMatchRules(string matchId, string playerId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/rules/{playerId}";
                var response = await HttpClientHelper.GetAsync(url);
                return JsonUtility.FromJson<GameRulesDto>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"GetMatchRules failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Set ready status (auth token auto-added from SecureTokenStorage).
        /// POST /api/v1/matches/{matchId}/ready
        /// </summary>
        public async Task<MatchSnapshot> SetReady(string matchId, string playerId, bool isReady)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/ready";
                var request = JsonUtility.ToJson(new SetReadyRequestDto
                {
                    matchId = matchId,
                    playerId = playerId,
                    isReady = isReady
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchSnapshot>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"SetReady failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Play a card (auth token auto-added from SecureTokenStorage).
        /// POST /api/v1/matches/{matchId}/play
        /// </summary>
        public async Task<MatchSnapshot> PlayCard(string matchId, string playerId, string runtimeHandKey, int slotIndex)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/play";
                var request = JsonUtility.ToJson(new PlayCardRequestDto
                {
                    matchId = matchId,
                    playerId = playerId,
                    runtimeHandKey = runtimeHandKey,
                    slotIndex = slotIndex
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchSnapshot>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"PlayCard failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// End turn (auth token auto-added from SecureTokenStorage).
        /// POST /api/v1/matches/{matchId}/end-turn
        /// </summary>
        public async Task<MatchSnapshot> EndTurn(string matchId, string playerId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/end-turn";
                var request = JsonUtility.ToJson(new EndTurnRequestDto
                {
                    matchId = matchId,
                    playerId = playerId
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchSnapshot>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"EndTurn failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Destroy one of the local player's live board cards.
        /// POST /api/v1/matches/{matchId}/destroy-card
        /// </summary>
        public async Task<MatchSnapshot> DestroyCard(string matchId, string playerId, string runtimeCardId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/destroy-card";
                var request = JsonUtility.ToJson(new DestroyCardRequestDto
                {
                    matchId = matchId,
                    playerId = playerId,
                    runtimeCardId = runtimeCardId
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchSnapshot>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"DestroyCard failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Forfeit match (auth token auto-added from SecureTokenStorage).
        /// POST /api/v1/matches/{matchId}/forfeit
        /// </summary>
        public async Task<MatchSnapshot> Forfeit(string matchId, string playerId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/matches/{matchId}/forfeit";
                var request = JsonUtility.ToJson(new ForfeitRequestDto
                {
                    matchId = matchId,
                    playerId = playerId
                });
                var response = await HttpClientHelper.PostAsync(url, request);
                return JsonUtility.FromJson<MatchSnapshot>(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchplayApiClient", $"Forfeit failed: {ex.Message}");
                throw;
            }
        }
    }
}
