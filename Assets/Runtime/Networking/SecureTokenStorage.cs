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
        private const string TokenKey = "auth_token_secure";
        private const string RefreshTokenKey = "auth_refresh_token_secure";
        private const string PlayerIdKey = "auth_player_id";
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
#if UNITY_IOS && !UNITY_EDITOR
            return GetKeychainValue(TokenKey) ?? "";
#elif UNITY_ANDROID && !UNITY_EDITOR
            return GetFromAndroidKeystore(TokenKey) ?? "";
#else
            return GetFromPlayerPrefsEncrypted(TokenKey) ?? "";
#endif
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
#if UNITY_IOS && !UNITY_EDITOR
            return GetKeychainValue(RefreshTokenKey) ?? "";
#elif UNITY_ANDROID && !UNITY_EDITOR
            return GetFromAndroidKeystore(RefreshTokenKey) ?? "";
#else
            return GetFromPlayerPrefsEncrypted(RefreshTokenKey) ?? "";
#endif
        }

        /// <summary>
        /// Guarda player ID (no sensible, puede ser plaintext).
        /// </summary>
        public static void SavePlayerId(string playerId)
        {
            PlayerPrefs.SetString(PlayerIdKey, playerId ?? "");
        }

        /// <summary>
        /// Obtiene el player ID.
        /// </summary>
        public static string GetPlayerId()
        {
            return PlayerPrefs.GetString(PlayerIdKey, "");
        }

        /// <summary>
        /// Guarda el timestamp de expiración del token.
        /// </summary>
        public static void SaveTokenExpiry(long expiryUnixSeconds)
        {
            PlayerPrefs.SetString(ExpiryKey, expiryUnixSeconds.ToString());
        }

        /// <summary>
        /// Obtiene el timestamp de expiración.
        /// </summary>
        public static long GetTokenExpiry()
        {
            var expiry = PlayerPrefs.GetString(ExpiryKey, "0");
            return long.TryParse(expiry, out var result) ? result : 0;
        }

        /// <summary>
        /// Limpia todos los tokens almacenados.
        /// </summary>
        public static void DeleteAll()
        {
            DeleteToken();
            DeleteRefreshToken();
            DeletePlayerId();
            DeleteExpiry();
        }

        private static void DeleteToken()
        {
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
            PlayerPrefs.DeleteKey(PlayerIdKey);
        }

        private static void DeleteExpiry()
        {
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
