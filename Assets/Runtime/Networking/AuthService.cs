using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Autenticación JWT con CardGameAPI (endpoints reales).
    /// Almacena tokens de forma segura usando SecureTokenStorage.
    ///
    /// Endpoints:
    /// - POST /api/auth/login (Email, Password) -> JWT token + userId
    /// - POST /api/auth/register (Email, Username, Password) -> JWT token + userId
    ///
    /// Tokens válidos 24 horas. Se cargan desde secure storage al iniciar.
    /// </summary>
    public sealed class AuthService
    {
        private readonly string _baseUrl;

        public string CurrentPlayerId { get; private set; }
        public string CurrentUserEmail { get; private set; }
        public string CurrentToken { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentToken) && !IsTokenExpired();

        public AuthService(string baseUrl = "http://localhost:5000")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            LoadTokenFromSecureStorage();
        }

        /// <summary>
        /// Refresca el token si está a punto de expirar.
        /// </summary>
        public async Task RefreshTokenIfNeeded()
        {
            var expiry = SecureTokenStorage.GetTokenExpiry();
            if (expiry == 0) return; // No expiry set

            var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiry);
            var now = DateTimeOffset.UtcNow;

            // Refresh if token expires in less than 5 minutes
            if (expiryTime - now < TimeSpan.FromMinutes(5))
            {
                Debug.LogWarning("[Auth] Token expiring soon, user needs to login again");
                // Token expired - user needs to login again
                Logout();
            }
        }

        /// <summary>
        /// Login con Email y Password.
        /// Retorna true si es exitoso, false si credenciales inválidas.
        /// </summary>
        public async Task<bool> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new ValidationException("Email and password required.");
            }

            try
            {
                var request = new LoginRequest { email = email, password = password };
                var json = JsonUtility.ToJson(request);

                using var webRequest = UnityWebRequest.PostWwwForm($"{_baseUrl}/api/auth/login", "application/json");
                webRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 30;

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone) await Task.Delay(10);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Login failed: {webRequest.responseCode} - {webRequest.error}");
                    return false;
                }

                var response = JsonUtility.FromJson<AuthResponse>(webRequest.downloadHandler.text);
                if (response == null || string.IsNullOrWhiteSpace(response.token))
                {
                    Debug.LogError("Login response invalid");
                    return false;
                }

                SetToken(email, response.token, response.userId);
                Debug.Log($"Login successful: {email}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Login error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registro con Email, Username y Password.
        /// Retorna true si es exitoso.
        /// </summary>
        public async Task<bool> Register(string email, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new ValidationException("Email, username, and password required.");
            }

            try
            {
                var request = new RegisterRequest { email = email, username = username, password = password };
                var json = JsonUtility.ToJson(request);

                using var webRequest = UnityWebRequest.PostWwwForm($"{_baseUrl}/api/auth/register", "application/json");
                webRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 30;

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone) await Task.Delay(10);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Register failed: {webRequest.responseCode} - {webRequest.error}");
                    return false;
                }

                var response = JsonUtility.FromJson<AuthResponse>(webRequest.downloadHandler.text);
                if (response == null || string.IsNullOrWhiteSpace(response.token))
                {
                    Debug.LogError("Register response invalid");
                    return false;
                }

                SetToken(email, response.token, response.userId);
                Debug.Log($"Registration successful: {email}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Register error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logout: limpia tokens locales.
        /// </summary>
        public void Logout()
        {
            ClearTokens();
            Debug.Log("Logged out");
        }

        /// <summary>
        /// Obtiene el Bearer token para headers Authorization.
        /// </summary>
        public string GetAuthorizationHeader()
        {
            return IsAuthenticated ? $"Bearer {CurrentToken}" : null;
        }

        private void SetToken(string email, string token, string userId)
        {
            CurrentUserEmail = email;
            CurrentPlayerId = userId;
            CurrentToken = token;

            SecureTokenStorage.SavePlayerId(userId);
            SecureTokenStorage.SaveToken(token);
            SecureTokenStorage.SaveTokenExpiry(DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
        }

        private void LoadTokenFromSecureStorage()
        {
            CurrentPlayerId = SecureTokenStorage.GetPlayerId();
            CurrentToken = SecureTokenStorage.GetToken();

            if (string.IsNullOrWhiteSpace(CurrentToken) || IsTokenExpired())
            {
                ClearTokens();
            }
        }

        private void ClearTokens()
        {
            CurrentPlayerId = "";
            CurrentUserEmail = "";
            CurrentToken = "";
            SecureTokenStorage.DeleteAll();
        }

        private bool IsTokenExpired()
        {
            var expiry = SecureTokenStorage.GetTokenExpiry();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now >= expiry;
        }

        [System.Serializable]
        private sealed class LoginRequest
        {
            public string email;
            public string password;
        }

        [System.Serializable]
        private sealed class RegisterRequest
        {
            public string email;
            public string username;
            public string password;
        }

        [System.Serializable]
        private sealed class AuthResponse
        {
            public string token;
            public string userId;
            public string username;
            public string email;
        }
    }
}
