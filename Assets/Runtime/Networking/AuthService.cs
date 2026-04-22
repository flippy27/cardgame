using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class AuthService
    {
        private static readonly object SharedSessionLock = new();
        private static bool _sharedSessionLoaded;
        private static string _sharedPlayerId;
        private static string _sharedUserEmail;
        private static string _sharedToken;

        private readonly AuthApiClient _apiClient;
        private string _currentPlayerId;
        private string _currentUserEmail;
        private string _currentToken;

        public string CurrentPlayerId
        {
            get
            {
                RefreshSessionFromSharedCache();
                return _currentPlayerId;
            }
            private set => _currentPlayerId = value;
        }

        public string CurrentUserEmail
        {
            get
            {
                RefreshSessionFromSharedCache();
                return _currentUserEmail;
            }
            private set => _currentUserEmail = value;
        }

        public string CurrentToken
        {
            get
            {
                RefreshSessionFromSharedCache();
                return _currentToken;
            }
            private set => _currentToken = value;
        }

        public bool IsAuthenticated
        {
            get
            {
                RefreshSessionFromSharedCache();
                return !string.IsNullOrWhiteSpace(_currentToken) && !IsTokenExpired();
            }
        }

        public AuthService(string baseUrl = null)
        {
            _apiClient = new AuthApiClient(baseUrl);
            LoadTokenFromSession();
            GameLogger.Debug("Auth", "Initialized");
        }

        /// <summary>
        /// Refresca el token si está a punto de expirar.
        /// </summary>
        public async Task RefreshTokenIfNeeded()
        {
            var expiry = SecureTokenStorage.GetTokenExpiry();
            if (expiry == 0) return;

            var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiry);
            var now = DateTimeOffset.UtcNow;

            if (expiryTime - now < TimeSpan.FromMinutes(5))
            {
                Debug.LogWarning("[Auth] Token expiring soon, user needs to login again");
                Logout();
            }
        }

        /// <summary>
        /// Login con Email y Password.
        /// </summary>
        public async Task<bool> Login(string email, string password)
        {
            try
            {
                RequestValidator.ValidateEmail(email);
                RequestValidator.ValidatePassword(password);

                var response = await _apiClient.Login(email, password);

                if (response == null || string.IsNullOrWhiteSpace(response.token))
                {
                    GameLogger.Error("Auth", "Login response invalid");
                    GameEvents.RaiseAuthFailed(ApiErrorCode.PARSE_INVALID_FORMAT);
                    return false;
                }

                var tokenSubject = DecodeJwtSubject(response.token);
                var resolvedUserId = FirstNonEmpty(tokenSubject, response.resolvedUserId, response.userId);
                SetToken(email, response.token, resolvedUserId);

                GameLogger.Info("Auth", $"Login raw body userId='{response.userId}', api tokenSub='{response.tokenSubject}', auth tokenSub='{tokenSubject}'");

                if (!string.IsNullOrWhiteSpace(response.userId) &&
                    !string.IsNullOrWhiteSpace(tokenSubject) &&
                    !string.Equals(response.userId, tokenSubject, StringComparison.Ordinal))
                {
                    GameLogger.Warning("Auth", $"Login identity mismatch. Body userId='{response.userId}' but JWT sub='{tokenSubject}'. Using '{resolvedUserId}'.");
                }

                GameLogger.Info("Auth", $"Login successful: {email} - UserID: {resolvedUserId}");
                GameEvents.RaiseConnected();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                GameLogger.Error("Auth", "Login failed: Invalid credentials");
                GameEvents.RaiseAuthFailed(ApiErrorCode.AUTH_INVALID_CREDENTIALS);
                return false;
            }
            catch (Exception ex)
            {
                GameLogger.Error("Auth", $"Login exception: {ex.Message}");
                GameEvents.RaiseAuthFailed(ApiErrorCode.UNKNOWN);
                return false;
            }
        }

        /// <summary>
        /// Registro con Email, Username y Password.
        /// </summary>
        public async Task<bool> Register(string email, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ValidationException("Email, username, and password required.");

            try
            {
                var response = await _apiClient.Register(email, username, password);

                if (response == null || string.IsNullOrWhiteSpace(response.token))
                {
                    Debug.LogError("Register response invalid");
                    return false;
                }

                var tokenSubject = DecodeJwtSubject(response.token);
                var resolvedUserId = FirstNonEmpty(tokenSubject, response.resolvedUserId, response.userId);
                SetToken(email, response.token, resolvedUserId);
                Debug.Log($"Registration successful: {email}");
                GameEvents.RaiseConnected();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Register failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logout - limpia token y datos.
        /// </summary>
        public void Logout()
        {
            CurrentPlayerId = null;
            CurrentUserEmail = null;
            CurrentToken = null;
            ClearSharedSession();
            SecureTokenStorage.DeleteAll();
        }

        /// <summary>
        /// Carga token desde secure storage si existe.
        /// </summary>
        private void LoadTokenFromSession()
        {
            EnsureSharedSessionLoaded();
            ApplySharedSessionToInstance();
        }

        public void RefreshSessionFromStorage()
        {
            RefreshSessionFromSharedCache();
        }

        private void RefreshSessionFromSharedCache()
        {
            EnsureSharedSessionLoaded();
            ApplySharedSessionToInstance();
        }

        private static void EnsureSharedSessionLoaded()
        {
            lock (SharedSessionLock)
            {
                if (_sharedSessionLoaded)
                {
                    return;
                }

                _sharedPlayerId = SecureTokenStorage.GetPlayerId();
                _sharedUserEmail = SecureTokenStorage.GetEmail();
                _sharedToken = SecureTokenStorage.GetToken();
                _sharedSessionLoaded = true;
            }
        }

        private void ApplySharedSessionToInstance()
        {
            lock (SharedSessionLock)
            {
                _currentPlayerId = _sharedPlayerId;
                _currentUserEmail = _sharedUserEmail;
                _currentToken = _sharedToken;
            }
        }

        private static void UpdateSharedSession(string playerId, string email, string token)
        {
            lock (SharedSessionLock)
            {
                _sharedPlayerId = playerId;
                _sharedUserEmail = email;
                _sharedToken = token;
                _sharedSessionLoaded = true;
            }
        }

        private static void ClearSharedSession()
        {
            lock (SharedSessionLock)
            {
                _sharedPlayerId = null;
                _sharedUserEmail = null;
                _sharedToken = null;
                _sharedSessionLoaded = true;
            }
        }

        /// <summary>
        /// Guarda token en secure storage.
        /// </summary>
        private void SetToken(string email, string token, string userId)
        {
            _currentPlayerId = userId;
            _currentUserEmail = email;
            _currentToken = token;
            UpdateSharedSession(userId, email, token);

            SecureTokenStorage.SavePlayerId(userId);
            SecureTokenStorage.SaveEmail(email);
            SecureTokenStorage.SaveToken(token);
            SecureTokenStorage.SaveTokenExpiry(DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
        }

        /// <summary>
        /// Verifica si el token ha expirado.
        /// </summary>
        private bool IsTokenExpired()
        {
            var expiry = SecureTokenStorage.GetTokenExpiry();
            if (expiry == 0) return true;

            var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiry);
            return DateTimeOffset.UtcNow >= expiryTime;
        }

        private static string DecodeJwtSubject(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
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
                return TryExtractJsonString(payloadJson, "sub");
            }
            catch (Exception ex)
            {
                GameLogger.Warning("Auth", $"Failed to decode JWT subject: {ex.Message}");
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

        private static string TryExtractJsonString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var propertyToken = $"\"{propertyName}\"";
            var propertyIndex = json.IndexOf(propertyToken, StringComparison.Ordinal);
            if (propertyIndex < 0)
            {
                return null;
            }

            var colonIndex = json.IndexOf(':', propertyIndex + propertyToken.Length);
            if (colonIndex < 0)
            {
                return null;
            }

            var valueStart = json.IndexOf('"', colonIndex + 1);
            if (valueStart < 0)
            {
                return null;
            }

            var cursor = valueStart + 1;
            while (cursor < json.Length)
            {
                var quoteIndex = json.IndexOf('"', cursor);
                if (quoteIndex < 0)
                {
                    return null;
                }

                if (json[quoteIndex - 1] != '\\')
                {
                    return json.Substring(valueStart + 1, quoteIndex - valueStart - 1).Replace("\\\"", "\"");
                }

                cursor = quoteIndex + 1;
            }

            return null;
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
