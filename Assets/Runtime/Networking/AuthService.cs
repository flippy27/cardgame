using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class AuthService
    {
        private readonly AuthApiClient _apiClient;

        public string CurrentPlayerId { get; private set; }
        public string CurrentUserEmail { get; private set; }
        public string CurrentToken { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentToken) && !IsTokenExpired();

        public AuthService(string baseUrl = null)
        {
            _apiClient = new AuthApiClient(baseUrl);
            LoadTokenFromSecureStorage();
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

                SetToken(email, response.token, response.userId);
                GameLogger.Info("Auth", $"Login successful: {email}");
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

                SetToken(email, response.token, response.userId);
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
            SecureTokenStorage.DeleteAll();
        }

        /// <summary>
        /// Carga token desde secure storage si existe.
        /// </summary>
        private void LoadTokenFromSecureStorage()
        {
            CurrentPlayerId = SecureTokenStorage.GetPlayerId();
            CurrentUserEmail = SecureTokenStorage.GetEmail();
            CurrentToken = SecureTokenStorage.GetToken();
        }

        /// <summary>
        /// Guarda token en secure storage.
        /// </summary>
        private void SetToken(string email, string token, string userId)
        {
            CurrentPlayerId = userId;
            CurrentUserEmail = email;
            CurrentToken = token;

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

    }
}
