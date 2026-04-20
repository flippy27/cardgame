using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class ReplayPlayerService
    {
        private ReplayApiClient _apiClient;
        private AuthService _authService;

        public ReplayPlayerService(ReplayApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient;
            _authService = authService;
        }

        public async Task<MatchReplay> FetchReplayAsync(string matchId)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Replay] Not authenticated");
                return null;
            }

            try
            {
                var dto = await _apiClient.FetchReplay(matchId);
                var replay = new MatchReplay
                {
                    matchId = dto.matchId,
                    actions = dto.actions ?? new List<object>()
                };

                Debug.Log($"[Replay] Loaded {replay.matchId}: {replay.actions.Count} actions");
                return replay;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Replay] Failed to fetch {matchId}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ValidateReplayAsync(string matchId)
        {
            if (!_authService.IsAuthenticated)
                return false;

            try
            {
                var response = await _apiClient.ValidateReplay(matchId);
                return response?.valid ?? false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Replay] Validation failed: {ex.Message}");
                return false;
            }
        }

        public sealed class MatchReplay
        {
            public string matchId;
            public List<object> actions;
        }
    }
}
