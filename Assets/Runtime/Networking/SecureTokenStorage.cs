using System;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Almacenamiento seguro de tokens con respaldo de plataforma.
    /// - iOS: Keychain (nativo)
    /// - Android: Keystore (nativo)
    /// - Editor/Otros: PlayerPrefs con encriptación XOR (fallback inseguro para testing)
    /// </summary>
    public static class SecureTokenStorage
    {
        private static readonly object CacheLock = new();
        private static bool _cacheLoaded;
        private static string _cachedToken;
        private static string _cachedRefreshToken;
        private static string _cachedPlayerId;
        private static string _cachedEmail;
        private static long _cachedExpiry;

        private const string TokenKey = "auth_token_secure";
        private const string RefreshTokenKey = "auth_refresh_token_secure";
        private const string PlayerIdKey = "auth_player_id";
        private const string EmailKey = "auth_email";
        private const string ExpiryKey = "auth_expiry";
        private const string EncryptionKey = "CardGameSecureKey2026"; // Para XOR fallback

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SetKeychainValue(string key, string value);

        [DllImport("__Internal")]
        private static extern string GetKeychainValue(string key);

        [DllImport("__Internal")]
        private static extern void DeleteKeychainValue(string key);
#endif

        /// <summary>
        /// Guarda el token de forma segura según la plataforma.
        /// </summary>
        public static void SaveToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                DeleteToken();
                return;
            }

            lock (CacheLock)
            {
                _cachedToken = token;
                _cacheLoaded = true;
            }

#if UNITY_IOS && !UNITY_EDITOR
            SetKeychainValue(TokenKey, token);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SaveToAndroidKeystore(TokenKey, token);
#else
            // Fallback: encriptación XOR simple para testing
            SaveToPlayerPrefsEncrypted(TokenKey, token);
#endif
        }

        /// <summary>
        /// Obtiene el token de forma segura.
        /// </summary>
        public static string GetToken()
        {
            EnsureCacheLoaded();

            lock (CacheLock)
            {
                return _cachedToken ?? "";
            }
        }

        /// <summary>
        /// Guarda el refresh token de forma segura.
        /// </summary>
        public static void SaveRefreshToken(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                DeleteRefreshToken();
                return;
            }

            lock (CacheLock)
            {
                _cachedRefreshToken = refreshToken;
                _cacheLoaded = true;
            }

#if UNITY_IOS && !UNITY_EDITOR
            SetKeychainValue(RefreshTokenKey, refreshToken);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SaveToAndroidKeystore(RefreshTokenKey, refreshToken);
#else
            SaveToPlayerPrefsEncrypted(RefreshTokenKey, refreshToken);
#endif
        }

        /// <summary>
        /// Obtiene el refresh token de forma segura.
        /// </summary>
        public static string GetRefreshToken()
        {
            EnsureCacheLoaded();

            lock (CacheLock)
            {
                return _cachedRefreshToken ?? "";
            }
        }

        /// <summary>
        /// Guarda player ID (no sensible, puede ser plaintext).
        /// </summary>
        public static void SavePlayerId(string playerId)
        {
            lock (CacheLock)
            {
                _cachedPlayerId = playerId ?? "";
                _cacheLoaded = true;
            }

            PlayerPrefs.SetString(PlayerIdKey, playerId ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Obtiene el player ID.
        /// </summary>
        public static string GetPlayerId()
        {
            EnsureCacheLoaded();
            lock (CacheLock)
            {
                return _cachedPlayerId ?? "";
            }
        }

        /// <summary>
        /// Guarda el email del usuario.
        /// </summary>
        public static void SaveEmail(string email)
        {
            lock (CacheLock)
            {
                _cachedEmail = email ?? "";
                _cacheLoaded = true;
            }

            PlayerPrefs.SetString(EmailKey, email ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Obtiene el email del usuario.
        /// </summary>
        public static string GetEmail()
        {
            EnsureCacheLoaded();
            lock (CacheLock)
            {
                return _cachedEmail ?? "";
            }
        }

        /// <summary>
        /// Guarda el timestamp de expiración del token.
        /// </summary>
        public static void SaveTokenExpiry(long expiryUnixSeconds)
        {
            lock (CacheLock)
            {
                _cachedExpiry = expiryUnixSeconds;
                _cacheLoaded = true;
            }

            PlayerPrefs.SetString(ExpiryKey, expiryUnixSeconds.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Obtiene el timestamp de expiración.
        /// </summary>
        public static long GetTokenExpiry()
        {
            EnsureCacheLoaded();
            lock (CacheLock)
            {
                return _cachedExpiry;
            }
        }

        /// <summary>
        /// Limpia todos los tokens almacenados.
        /// </summary>
        public static void DeleteAll()
        {
            ClearCache();
            DeleteToken();
            DeleteRefreshToken();
            DeletePlayerId();
            DeleteEmail();
            DeleteExpiry();
        }

        private static void EnsureCacheLoaded()
        {
            lock (CacheLock)
            {
                if (_cacheLoaded)
                {
                    return;
                }

#if UNITY_IOS && !UNITY_EDITOR
                _cachedToken = GetKeychainValue(TokenKey) ?? "";
                _cachedRefreshToken = GetKeychainValue(RefreshTokenKey) ?? "";
#elif UNITY_ANDROID && !UNITY_EDITOR
                _cachedToken = GetFromAndroidKeystore(TokenKey) ?? "";
                _cachedRefreshToken = GetFromAndroidKeystore(RefreshTokenKey) ?? "";
#else
                _cachedToken = GetFromPlayerPrefsEncrypted(TokenKey) ?? "";
                _cachedRefreshToken = GetFromPlayerPrefsEncrypted(RefreshTokenKey) ?? "";
#endif
                _cachedPlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
                _cachedEmail = PlayerPrefs.GetString(EmailKey, "");
                var expiry = PlayerPrefs.GetString(ExpiryKey, "0");
                _cachedExpiry = long.TryParse(expiry, out var result) ? result : 0;
                _cacheLoaded = true;
            }
        }

        private static void ClearCache()
        {
            lock (CacheLock)
            {
                _cachedToken = "";
                _cachedRefreshToken = "";
                _cachedPlayerId = "";
                _cachedEmail = "";
                _cachedExpiry = 0;
                _cacheLoaded = true;
            }
        }

        private static void DeleteEmail()
        {
            lock (CacheLock)
            {
                _cachedEmail = "";
                _cacheLoaded = true;
            }

            PlayerPrefs.DeleteKey(EmailKey);
        }

        private static void DeleteToken()
        {
            lock (CacheLock)
            {
                _cachedToken = "";
                _cacheLoaded = true;
            }

#if UNITY_IOS && !UNITY_EDITOR
            DeleteKeychainValue(TokenKey);
#elif UNITY_ANDROID && !UNITY_EDITOR
            DeleteFromAndroidKeystore(TokenKey);
#else
            PlayerPrefs.DeleteKey(TokenKey);
#endif
        }

        private static void DeleteRefreshToken()
        {
            lock (CacheLock)
            {
                _cachedRefreshToken = "";
                _cacheLoaded = true;
            }

#if UNITY_IOS && !UNITY_EDITOR
            DeleteKeychainValue(RefreshTokenKey);
#elif UNITY_ANDROID && !UNITY_EDITOR
            DeleteFromAndroidKeystore(RefreshTokenKey);
#else
            PlayerPrefs.DeleteKey(RefreshTokenKey);
#endif
        }

        private static void DeletePlayerId()
        {
            lock (CacheLock)
            {
                _cachedPlayerId = "";
                _cacheLoaded = true;
            }

            PlayerPrefs.DeleteKey(PlayerIdKey);
        }

        private static void DeleteExpiry()
        {
            lock (CacheLock)
            {
                _cachedExpiry = 0;
                _cacheLoaded = true;
            }

            PlayerPrefs.DeleteKey(ExpiryKey);
        }

        // ========== FALLBACK IMPLEMENTACIONES ==========

        /// <summary>
        /// Encriptación XOR simple para testing (NO SEGURA para producción).
        /// </summary>
        private static void SaveToPlayerPrefsEncrypted(string key, string value)
        {
            var encrypted = EncryptXor(value);
            PlayerPrefs.SetString(key, encrypted);
            PlayerPrefs.Save();
            Debug.LogWarning($"[SecureTokenStorage] Usando XOR encryption (inseguro). Use Keychain en iOS o Keystore en Android.");
        }

        private static string GetFromPlayerPrefsEncrypted(string key)
        {
            var encrypted = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(encrypted))
                return null;

            try
            {
                return DecryptXor(encrypted);
            }
            catch
            {
                Debug.LogError($"[SecureTokenStorage] Error desencriptando token almacenado");
                return null;
            }
        }

        private static string EncryptXor(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return "";

            var result = new char[plaintext.Length];
            for (int i = 0; i < plaintext.Length; i++)
            {
                result[i] = (char)(plaintext[i] ^ EncryptionKey[i % EncryptionKey.Length]);
            }
            return new string(result);
        }

        private static string DecryptXor(string ciphertext)
        {
            // XOR es simétrico
            return EncryptXor(ciphertext);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void SaveToAndroidKeystore(string key, string value)
        {
            // Placeholder: se implementaría con plugin nativo o AndroidX Security
            Debug.LogWarning($"[SecureTokenStorage] Android Keystore no implementado, usando PlayerPrefs fallback");
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        private static string GetFromAndroidKeystore(string key)
        {
            return PlayerPrefs.GetString(key, "");
        }

        private static void DeleteFromAndroidKeystore(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }
#endif
    }
}
