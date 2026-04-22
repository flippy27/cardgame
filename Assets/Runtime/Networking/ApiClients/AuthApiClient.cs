using System;
using System.Text;
using System.Text.RegularExpressions;
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
            return ParseAuthResponse(responseJson, "login");
        }

        public async Task<AuthResponse> Register(string email, string username, string password)
        {
            var request = new RegisterRequest { email = email, username = username, password = password };
            var json = JsonUtility.ToJson(request);
            var responseJson = await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/auth/register", json);
            return ParseAuthResponse(responseJson, "register");
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
            public string username;
            public string email;
            public string tokenSubject;
            public string resolvedUserId;
            public string rawJson;
        }

        private static AuthResponse ParseAuthResponse(string responseJson, string operation)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                Debug.LogError($"[AuthApiClient] Empty {operation} response.");
                return null;
            }

            var parsed = JsonUtility.FromJson<AuthResponse>(responseJson) ?? new AuthResponse();
            parsed.rawJson = responseJson;

            parsed.token = FirstNonEmpty(parsed.token, TryExtractJsonStringValue(responseJson, "token"));
            parsed.userId = FirstNonEmpty(parsed.userId, TryExtractJsonStringValue(responseJson, "userId"));
            parsed.username = FirstNonEmpty(parsed.username, TryExtractJsonStringValue(responseJson, "username"));
            parsed.email = FirstNonEmpty(parsed.email, TryExtractJsonStringValue(responseJson, "email"));
            parsed.tokenSubject = TryDecodeJwtClaim(parsed.token, "sub");
            parsed.resolvedUserId = FirstNonEmpty(parsed.tokenSubject, parsed.userId);

            Debug.Log($"[AuthApiClient] {operation} raw response: {responseJson}");
            Debug.Log($"[AuthApiClient] {operation} parsed userId='{parsed.userId}', tokenSub='{parsed.tokenSubject}', resolvedUserId='{parsed.resolvedUserId}'");

            if (!string.IsNullOrWhiteSpace(parsed.userId) &&
                !string.IsNullOrWhiteSpace(parsed.tokenSubject) &&
                !string.Equals(parsed.userId, parsed.tokenSubject, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[AuthApiClient] {operation} response mismatch. Body userId='{parsed.userId}' but JWT sub='{parsed.tokenSubject}'. Using JWT sub as canonical player ID.");
            }

            return parsed;
        }

        private static string TryExtractJsonStringValue(string json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            var pattern = $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"";
            var match = Regex.Match(json, pattern, RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return null;
            }

            return Regex.Unescape(match.Groups["value"].Value);
        }

        private static string TryDecodeJwtClaim(string token, string claimName)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(claimName))
            {
                return null;
            }

            var segments = token.Split('.');
            if (segments.Length < 2)
            {
                return null;
            }

            try
            {
                var payloadJson = DecodeBase64Url(segments[1]);
                return TryExtractJsonStringValue(payloadJson, claimName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AuthApiClient] Failed to decode JWT claim '{claimName}': {ex.Message}");
                return null;
            }
        }

        private static string DecodeBase64Url(string base64Url)
        {
            var normalized = base64Url.Replace('-', '+').Replace('_', '/');
            var remainder = normalized.Length % 4;
            if (remainder > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');
            }

            var bytes = Convert.FromBase64String(normalized);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
