using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Gestiona perfil y estadísticas del usuario.
    /// </summary>
    public sealed class UserProfileService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private UserProfileDto _cachedProfile;
        private UserStatsDto _cachedStats;
        private DateTimeOffset _lastProfileFetch = DateTimeOffset.MinValue;
        private DateTimeOffset _lastStatsFetch = DateTimeOffset.MinValue;
        private const int CACHE_MINUTES = 5;

        public UserProfileService(CardGameApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Obtiene el perfil del usuario.
        /// Cachea por 5 minutos.
        /// </summary>
        public async Task<UserProfileDto> GetProfileAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Profile] Not authenticated");
                return null;
            }

            var playerId = _authService.CurrentPlayerId;
            var now = DateTimeOffset.UtcNow;

            // Return cached if fresh
            if (_cachedProfile != null && (now - _lastProfileFetch).TotalMinutes < CACHE_MINUTES)
            {
                return _cachedProfile;
            }

            try
            {
                _cachedProfile = await _apiClient.GetUserProfileAsync(playerId);
                _lastProfileFetch = now;
                Debug.Log($"[Profile] Fetched profile: {_cachedProfile.username} (Rating: {_cachedProfile.rating})");
                return _cachedProfile;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Profile] Failed to fetch profile: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene las estadísticas del usuario.
        /// Cachea por 5 minutos.
        /// </summary>
        public async Task<UserStatsDto> GetStatsAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Stats] Not authenticated");
                return null;
            }

            var playerId = _authService.CurrentPlayerId;
            var now = DateTimeOffset.UtcNow;

            // Return cached if fresh
            if (_cachedStats != null && (now - _lastStatsFetch).TotalMinutes < CACHE_MINUTES)
            {
                return _cachedStats;
            }

            try
            {
                _cachedStats = await _apiClient.GetUserStatsAsync(playerId);
                _lastStatsFetch = now;
                Debug.Log($"[Stats] Fetched stats: {_cachedStats.wins}W-{_cachedStats.losses}L (WR: {_cachedStats.winRate:P2})");
                return _cachedStats;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Stats] Failed to fetch stats: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene ambos (perfil y stats) en paralelo.
        /// </summary>
        public async Task<(UserProfileDto profile, UserStatsDto stats)> GetFullDataAsync()
        {
            var profileTask = GetProfileAsync();
            var statsTask = GetStatsAsync();
            await Task.WhenAll(profileTask, statsTask);
            return (profileTask.Result, statsTask.Result);
        }

        /// <summary>
        /// Limpia la caché (ej: al logout).
        /// </summary>
        public void ClearCache()
        {
            _cachedProfile = null;
            _cachedStats = null;
            _lastProfileFetch = DateTimeOffset.MinValue;
            _lastStatsFetch = DateTimeOffset.MinValue;
            Debug.Log("[Profile] Cache cleared");
        }

        /// <summary>
        /// Fuerza refetch (ignora caché).
        /// </summary>
        public async Task<UserProfileDto> RefreshProfileAsync()
        {
            _cachedProfile = null;
            _lastProfileFetch = DateTimeOffset.MinValue;
            return await GetProfileAsync();
        }

        /// <summary>
        /// Fuerza refetch de stats (ignora caché).
        /// </summary>
        public async Task<UserStatsDto> RefreshStatsAsync()
        {
            _cachedStats = null;
            _lastStatsFetch = DateTimeOffset.MinValue;
            return await GetStatsAsync();
        }
    }
}
