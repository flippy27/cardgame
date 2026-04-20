using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class UserApiClient
    {
        private readonly string _baseUrl;

        public UserApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task<UserProfileDto> GetProfile(string playerId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/users/{playerId}/profile");
            return JsonUtility.FromJson<UserProfileDto>(json);
        }

        public async Task<UserStatsDto> GetStats(string playerId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/users/{playerId}/stats");
            return JsonUtility.FromJson<UserStatsDto>(json);
        }

        public async Task UpdateProfile(UserProfileDto profile)
        {
            var json = JsonUtility.ToJson(profile);
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/users/{profile.userId}/profile", json);
        }

        public async Task<AchievementDto[]> GetAchievements(string playerId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/users/{playerId}/achievements");
            var wrapper = JsonUtility.FromJson<AchievementWrapper>(json);
            return wrapper?.achievements ?? new AchievementDto[0];
        }

        public async Task UnlockAchievement(string playerId, string achievementId)
        {
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/users/{playerId}/achievements/{achievementId}", "{}");
        }

        [System.Serializable]
        public sealed class AchievementDto
        {
            public string achievementId;
            public string title;
            public string description;
            public int rewardExp;
            public bool isUnlocked;
            public long unlockedAt;
        }

        [System.Serializable]
        internal sealed class AchievementWrapper
        {
            public AchievementDto[] achievements;
        }
    }
}
