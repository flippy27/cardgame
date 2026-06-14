using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// Player progression (level + experience) endpoint.
    ///
    /// Base path: /api/v1/users/{userId}/progress
    /// Auth: JWT Bearer required (added by <see cref="HttpClientHelper"/>).
    ///
    /// The server owns the level curve (Pokémon Medium-Fast, total xp for level L = L^3, cap 100).
    /// The client NEVER computes it — it only renders the four values the API returns.
    /// </summary>
    public sealed class ProgressApiClient
    {
        private readonly string _baseUrl;

        public ProgressApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/users/{userId}/progress
        public async Task<UserProgressDto> GetProgress(string userId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/users/{userId}/progress");
            return JsonUtility.FromJson<UserProgressDto>(json);
        }

        /// <summary>
        /// Mirrors the server's progress payload. JsonUtility-safe: public camelCase fields, no
        /// properties. Server JSON is camelCase with integer/number fields, so these are numeric.
        /// </summary>
        [System.Serializable]
        public sealed class UserProgressDto
        {
            public long experience;       // total lifetime XP
            public int level;             // current level (1..100)
            public long xpIntoLevel;      // XP accumulated within the current level
            public long xpForNextLevel;   // XP span of the current level (0 at the level cap)
        }
    }
}
