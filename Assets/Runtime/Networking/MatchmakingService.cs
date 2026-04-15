using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Maneja búsqueda de oponentes, colas de espera (casual y ranked).
    /// </summary>
    public sealed class MatchmakingService
    {
        public enum QueueMode
        {
            Casual = 0,
            Ranked = 1
        }

        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;

        public bool IsSearching { get; private set; }
        public QueueMode CurrentMode { get; private set; }
        public int TimeInQueue { get; private set; }

        public MatchmakingService(CardGameApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Se une a la cola de búsqueda (casual o ranked).
        /// </summary>
        public async Task<bool> JoinQueue(QueueMode mode)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            if (IsSearching)
            {
                Debug.LogWarning("Already in queue");
                return false;
            }

            try
            {
                var modeStr = mode == QueueMode.Casual ? "casual" : "ranked";
                var endpoint = $"/api/matchmaking/queue?mode={modeStr}";
                await PostAsync(endpoint, "");

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

        /// <summary>
        /// Se sale de la cola de búsqueda.
        /// </summary>
        public async Task<bool> LeaveQueue()
        {
            if (!IsSearching)
            {
                Debug.LogWarning("Not in queue");
                return false;
            }

            try
            {
                await DeleteAsync("/api/matchmaking/queue");
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

        /// <summary>
        /// Obtiene estado de búsqueda actual.
        /// </summary>
        public async Task<MatchmakingStatusDto> GetStatus()
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            try
            {
                var json = await GetAsync("/api/matchmaking/status");
                var status = JsonUtility.FromJson<MatchmakingStatusDto>(json);
                TimeInQueue = status.TimeInQueueSeconds;
                return status;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get queue status: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cancela búsqueda actual.
        /// </summary>
        public async Task<bool> CancelSearch()
        {
            return await LeaveQueue();
        }

        // Helper methods

        private async Task<string> GetAsync(string endpoint)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url);
            request.method = "GET";
            request.timeout = _apiClient.TimeoutSeconds;
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }

            return request.downloadHandler.text;
        }

        private async Task PostAsync(string endpoint, string body)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
            request.timeout = _apiClient.TimeoutSeconds;
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }
        }

        private async Task DeleteAsync(string endpoint)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url, "DELETE");
            request.timeout = _apiClient.TimeoutSeconds;
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }
        }
    }

    /// <summary>
    /// DTO para estado de búsqueda en la cola.
    /// </summary>
    [System.Serializable]
    public sealed class MatchmakingStatusDto
    {
        public bool IsSearching;
        public int QueueMode; // 0=Casual, 1=Ranked
        public int TimeInQueueSeconds;
        public int EstimatedWaitSeconds;
        public int PlayersInQueue;
        public string OpponentId; // null si no encontrado aún
        public string MatchId; // null si no hay match creado
    }
}
