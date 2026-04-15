using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Maneja perfil de jugador, estadísticas y logros.
    /// </summary>
    public sealed class UserService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private PlayerProfileDto _cachedProfile;
        private DateTime _cacheExpiry;

        public UserService(CardGameApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Obtiene perfil del jugador (con cache de 5 minutos).
        /// </summary>
        public async Task<PlayerProfileDto> GetProfile(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            var id = playerId ?? _authService.CurrentPlayerId;

            // Cache válido?
            if (_cachedProfile != null && DateTime.UtcNow < _cacheExpiry && _cachedProfile.PlayerId == id)
            {
                return _cachedProfile;
            }

            // Fetch desde API
            var json = await GetAsync($"/api/users/{id}/profile");
            _cachedProfile = JsonUtility.FromJson<PlayerProfileDto>(json);
            _cacheExpiry = DateTime.UtcNow.AddMinutes(5);

            return _cachedProfile;
        }

        /// <summary>
        /// Obtiene estadísticas del jugador.
        /// </summary>
        public async Task<PlayerStatsDto> GetStats(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            var id = playerId ?? _authService.CurrentPlayerId;
            var json = await GetAsync($"/api/users/{id}/stats");
            return JsonUtility.FromJson<PlayerStatsDto>(json);
        }

        /// <summary>
        /// Actualiza el perfil del jugador.
        /// </summary>
        public async Task<bool> UpdateProfile(PlayerProfileDto profile)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            var json = JsonUtility.ToJson(profile);
            try
            {
                await PostAsync($"/api/users/{profile.PlayerId}/profile", json);
                _cachedProfile = null; // Invalidar cache
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update profile: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene lista de logros del jugador.
        /// </summary>
        public async Task<AchievementDto[]> GetAchievements(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            var id = playerId ?? _authService.CurrentPlayerId;
            var json = await GetAsync($"/api/users/{id}/achievements");
            var wrapper = JsonUtility.FromJson<AchievementWrapper>(json);
            return wrapper?.achievements ?? new AchievementDto[0];
        }

        /// <summary>
        /// Desbloquea un logro.
        /// </summary>
        public async Task<bool> UnlockAchievement(string achievementId)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            try
            {
                await PostAsync($"/api/users/{_authService.CurrentPlayerId}/achievements/{achievementId}", "");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to unlock achievement: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Helper para GET requests autenticadas.
        /// </summary>
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

        /// <summary>
        /// Helper para POST requests autenticadas.
        /// </summary>
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

        private void ClearCache()
        {
            _cachedProfile = null;
            _cacheExpiry = DateTime.MinValue;
        }
    }

    /// <summary>
    /// DTO para perfil del jugador.
    /// </summary>
    [System.Serializable]
    public sealed class PlayerProfileDto
    {
        public string PlayerId;
        public string DisplayName;
        public int Level;
        public int Experience;
        public string FactionPreference; // enum: Fire, Water, Nature, Light, Dark
        public long CreatedAt;
        public long LastSeenAt;
        public bool IsPremium;
    }

    /// <summary>
    /// DTO para estadísticas del jugador.
    /// </summary>
    [System.Serializable]
    public sealed class PlayerStatsDto
    {
        public string PlayerId;
        public int CurrentRating;
        public int HighestRating;
        public int TotalMatches;
        public int Wins;
        public int Losses;
        public float WinRate;
        public int RankedRating;
        public int RankedDivision; // 0=Bronze, 1=Silver, ..., 9=Mythic
        public int CasualRating;
    }

    /// <summary>
    /// DTO para logros.
    /// </summary>
    [System.Serializable]
    public sealed class AchievementDto
    {
        public string AchievementId;
        public string Title;
        public string Description;
        public int RewardExp;
        public bool IsUnlocked;
        public long UnlockedAt;
    }

    /// <summary>
    /// Wrapper para array de logros.
    /// </summary>
    [System.Serializable]
    private sealed class AchievementWrapper
    {
        public AchievementDto[] achievements;
    }
}
