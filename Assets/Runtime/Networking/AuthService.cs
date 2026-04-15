using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Maneja autenticación JWT con CardGameAPI.
    /// Almacena tokens en PlayerPrefs (simple), puede extenderse a secure storage.
    /// </summary>
    public sealed class AuthService
    {
        private const string TokenKey = "auth_token";
        private const string RefreshTokenKey = "auth_refresh_token";
        private const string PlayerIdKey = "auth_player_id";
        private const string ExpiryKey = "auth_expiry";

        private readonly CardGameApiClient _apiClient;

        public string CurrentPlayerId { get; private set; }
        public string CurrentToken { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentToken) && !IsTokenExpired();

        public AuthService(CardGameApiClient apiClient = null)
        {
            _apiClient = apiClient ?? new CardGameApiClient();
            LoadTokenFromStorage();
        }

        /// <summary>
        /// Intenta login con playerId y password (mock auth).
        /// </summary>
        public async Task<bool> Login(string playerId, string password)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(password))
            {
                throw new ValidationException("PlayerId and password required.");
            }

            try
            {
                // TODO: call /api/auth/login with playerId+password
                // For now: mock token generation for testing
                var token = GenerateMockToken(playerId);
                SetToken(playerId, token, expirySeconds: 3600);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Login failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logout: limpia tokens.
        /// </summary>
        public void Logout()
        {
            ClearTokens();
        }

        /// <summary>
        /// Obtiene el bearer token para headers Authorization.
        /// </summary>
        public string GetAuthorizationHeader()
        {
            return IsAuthenticated ? $"Bearer {CurrentToken}" : null;
        }

        /// <summary>
        /// Refresca token si está cerca de expirar.
        /// </summary>
        public async Task<bool> RefreshTokenIfNeeded()
        {
            if (!IsAuthenticated)
                return false;

            var expiryStr = PlayerPrefs.GetString(ExpiryKey, "0");
            if (!long.TryParse(expiryStr, out var expiry))
                return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var secondsUntilExpiry = expiry - now;

            // Si expira en menos de 5 minutos, intentar refresh
            if (secondsUntilExpiry < 300)
            {
                try
                {
                    // TODO: call /api/auth/refresh with refresh token
                    Debug.Log("Token refresh needed (not implemented yet)");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Token refresh failed: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        private void SetToken(string playerId, string token, int expirySeconds = 3600)
        {
            CurrentPlayerId = playerId;
            CurrentToken = token;

            var expiry = DateTimeOffset.UtcNow.AddSeconds(expirySeconds).ToUnixTimeSeconds();

            PlayerPrefs.SetString(PlayerIdKey, playerId);
            PlayerPrefs.SetString(TokenKey, token);
            PlayerPrefs.SetString(ExpiryKey, expiry.ToString());
            PlayerPrefs.Save();
        }

        private void LoadTokenFromStorage()
        {
            CurrentPlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
            CurrentToken = PlayerPrefs.GetString(TokenKey, "");

            if (IsTokenExpired())
            {
                ClearTokens();
            }
        }

        private void ClearTokens()
        {
            CurrentPlayerId = "";
            CurrentToken = "";
            PlayerPrefs.DeleteKey(PlayerIdKey);
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(ExpiryKey);
            PlayerPrefs.Save();
        }

        private bool IsTokenExpired()
        {
            var expiryStr = PlayerPrefs.GetString(ExpiryKey, "0");
            if (!long.TryParse(expiryStr, out var expiry))
                return true;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now >= expiry;
        }

        /// <summary>
        /// Mock token generation for testing (replace with real auth endpoint).
        /// </summary>
        private string GenerateMockToken(string playerId)
        {
            // Simple mock JWT-like token: header.payload.signature
            // Real implementation would call /api/auth/login
            var header = Base64Encode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = Base64Encode($"{{\"sub\":\"{playerId}\",\"iat\":{now},\"exp\":{now + 3600}}}");
            var signature = Base64Encode($"mock_signature_{playerId}");
            return $"{header}.{payload}.{signature}";
        }

        private static string Base64Encode(string text)
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
            return System.Convert.ToBase64String(textBytes).Replace("=", "").Replace("+", "-").Replace("/", "_");
        }
    }
}
