using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class AuthApiClient
    {
        private readonly string _baseUrl;

        public AuthApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task<AuthResponse> Login(string email, string password)
        {
            var request = new LoginRequest { email = email, password = password };
            var json = JsonUtility.ToJson(request);
            var responseJson = await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/auth/login", json);
            return JsonUtility.FromJson<AuthResponse>(responseJson);
        }

        public async Task<AuthResponse> Register(string email, string username, string password)
        {
            var request = new RegisterRequest { email = email, username = username, password = password };
            var json = JsonUtility.ToJson(request);
            var responseJson = await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/auth/register", json);
            return JsonUtility.FromJson<AuthResponse>(responseJson);
        }

        [System.Serializable]
        public sealed class LoginRequest
        {
            public string email;
            public string password;
        }

        [System.Serializable]
        public sealed class RegisterRequest
        {
            public string email;
            public string username;
            public string password;
        }

        [System.Serializable]
        public sealed class AuthResponse
        {
            public string token;
            public string userId;
        }
    }
}
