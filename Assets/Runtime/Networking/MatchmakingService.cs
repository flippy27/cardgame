using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class MatchmakingService
    {
        private readonly MatchmakingApiClient _apiClient;
        private readonly AuthService _authService;

        public bool IsSearching { get; private set; }
        public MatchmakingApiClient.QueueMode CurrentMode { get; private set; }
        public int TimeInQueue { get; private set; }

        public MatchmakingService(MatchmakingApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task<bool> JoinQueue(MatchmakingApiClient.QueueMode mode)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            if (IsSearching)
            {
                Debug.LogWarning("Already in queue");
                return false;
            }

            try
            {
                await _apiClient.JoinQueue(mode);
                IsSearching = true;
                CurrentMode = mode;
                TimeInQueue = 0;
                Debug.Log($"Joined {mode} queue");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to join queue: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LeaveQueue()
        {
            if (!IsSearching)
            {
                Debug.LogWarning("Not in queue");
                return false;
            }

            try
            {
                await _apiClient.LeaveQueue();
                IsSearching = false;
                TimeInQueue = 0;
                Debug.Log("Left queue");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to leave queue: {ex.Message}");
                return false;
            }
        }

        public async Task<MatchmakingApiClient.MatchmakingStatusDto> GetStatus()
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            try
            {
                var status = await _apiClient.GetStatus();
                TimeInQueue = status.TimeInQueueSeconds;
                return status;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get queue status: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CancelSearch()
        {
            return await LeaveQueue();
        }
    }
}
