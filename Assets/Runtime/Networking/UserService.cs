using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class UserService
    {
        private readonly UserApiClient _apiClient;
        private readonly AuthService _authService;
        private UserProfileDto _cachedProfile;
        private DateTime _cacheExpiry;

        public UserService(UserApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task<UserProfileDto> GetProfile(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            var id = playerId ?? _authService.CurrentPlayerId;

            if (_cachedProfile != null && DateTime.UtcNow < _cacheExpiry && _cachedProfile.userId == id)
                return _cachedProfile;

            try
            {
                _cachedProfile = await _apiClient.GetProfile(id);
                _cacheExpiry = DateTime.UtcNow.AddMinutes(5);
                return _cachedProfile;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Profile] Failed to fetch profile: {ex.Message}");
                throw;
            }
        }

        public async Task<UserStatsDto> GetStats(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            var id = playerId ?? _authService.CurrentPlayerId;
            return await _apiClient.GetStats(id);
        }

        public async Task<bool> UpdateProfile(UserProfileDto profile)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            try
            {
                await _apiClient.UpdateProfile(profile);
                _cachedProfile = null;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Profile] Failed to update profile: {ex.Message}");
                return false;
            }
        }

        public async Task<UserApiClient.AchievementDto[]> GetAchievements(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            var id = playerId ?? _authService.CurrentPlayerId;
            return await _apiClient.GetAchievements(id);
        }

        public async Task<bool> UnlockAchievement(string achievementId)
        {
            if (!_authService.IsAuthenticated)
                throw new InvalidGameStateException("Not authenticated. Login first.");

            try
            {
                await _apiClient.UnlockAchievement(_authService.CurrentPlayerId, achievementId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Achievements] Failed to unlock achievement: {ex.Message}");
                return false;
            }
        }

        private void ClearCache()
        {
            _cachedProfile = null;
            _cacheExpiry = DateTime.MinValue;
        }
    }
}
