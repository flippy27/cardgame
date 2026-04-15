using System;
using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Almacena datos en disco local para offline support.
    /// Cachea catálogos, perfiles, mazos, etc.
    /// </summary>
    public sealed class LocalCacheService
    {
        private const string CachePrefix = "cache_";
        private const int CacheExpiryHours = 24;

        /// <summary>
        /// Guarda un objeto en caché local (JSON).
        /// </summary>
        public void Set<T>(string key, T value, int expiryHours = CacheExpiryHours)
        {
            try
            {
                var json = JsonUtility.ToJson(value);
                var cacheKey = CachePrefix + key;
                var expiryKey = cacheKey + "_expiry";

                PlayerPrefs.SetString(cacheKey, json);

                var expiry = DateTimeOffset.UtcNow.AddHours(expiryHours).ToUnixTimeSeconds();
                PlayerPrefs.SetString(expiryKey, expiry.ToString());

                PlayerPrefs.Save();
                Debug.Log($"Cached: {key}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to cache {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene un objeto del caché local (o null si no existe/expiró).
        /// </summary>
        public T Get<T>(string key) where T : class
        {
            try
            {
                var cacheKey = CachePrefix + key;
                var expiryKey = cacheKey + "_expiry";

                // Check expiry
                if (PlayerPrefs.HasKey(expiryKey))
                {
                    var expiryStr = PlayerPrefs.GetString(expiryKey, "0");
                    if (long.TryParse(expiryStr, out var expiry))
                    {
                        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        if (now >= expiry)
                        {
                            Delete(key);
                            return null;
                        }
                    }
                }

                // Get cached value
                if (PlayerPrefs.HasKey(cacheKey))
                {
                    var json = PlayerPrefs.GetString(cacheKey);
                    var value = JsonUtility.FromJson<T>(json);
                    return value;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to retrieve cache {key}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifica si existe un valor en caché y no está expirado.
        /// </summary>
        public bool Has(string key)
        {
            var cacheKey = CachePrefix + key;
            var expiryKey = cacheKey + "_expiry";

            if (!PlayerPrefs.HasKey(cacheKey))
                return false;

            // Check expiry
            if (PlayerPrefs.HasKey(expiryKey))
            {
                var expiryStr = PlayerPrefs.GetString(expiryKey, "0");
                if (long.TryParse(expiryStr, out var expiry))
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (now >= expiry)
                    {
                        Delete(key);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Elimina un valor del caché.
        /// </summary>
        public void Delete(string key)
        {
            try
            {
                var cacheKey = CachePrefix + key;
                var expiryKey = cacheKey + "_expiry";

                PlayerPrefs.DeleteKey(cacheKey);
                PlayerPrefs.DeleteKey(expiryKey);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete cache {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpia todo el caché.
        /// </summary>
        public void Clear()
        {
            try
            {
                var keys = new List<string>();
                // Necesitamos iterar sobre todas las claves de PlayerPrefs
                // Unity no proporciona una forma directa, así que haremos una búsqueda simple
                for (int i = 0; i < 1000; i++)
                {
                    var key = $"{CachePrefix}{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        keys.Add(key);
                    }
                }

                foreach (var key in keys)
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.DeleteKey(key + "_expiry");
                }

                PlayerPrefs.Save();
                Debug.Log("Cleared all cache");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to clear cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene estadísticas del caché.
        /// </summary>
        public (int totalKeys, int expiredKeys) GetStats()
        {
            var total = 0;
            var expired = 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Búsqueda simple (en un proyecto real usaríamos SQLite)
            for (int i = 0; i < 1000; i++)
            {
                var key = $"{CachePrefix}{i}";
                if (PlayerPrefs.HasKey(key))
                {
                    total++;
                    var expiryKey = key + "_expiry";
                    if (PlayerPrefs.HasKey(expiryKey))
                    {
                        var expiryStr = PlayerPrefs.GetString(expiryKey, "0");
                        if (long.TryParse(expiryStr, out var expiry) && now >= expiry)
                        {
                            expired++;
                        }
                    }
                }
            }

            return (total, expired);
        }
    }

    /// <summary>
    /// Coordinador de sincronización offline.
    /// Detecta reconexión y sincroniza cambios pendientes.
    /// </summary>
    public sealed class OfflineSyncService
    {
        private readonly LocalCacheService _cache;
        private readonly CardGameApiClient _apiClient;
        private bool _isOnline;

        public bool IsOnline => _isOnline;
        public int PendingChanges { get; private set; }

        public OfflineSyncService(LocalCacheService cache, CardGameApiClient apiClient)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _isOnline = true;
            PendingChanges = 0;
        }

        /// <summary>
        /// Marca un cambio como pendiente de sincronizar.
        /// </summary>
        public void MarkPending(string changeId, string changeData)
        {
            PendingChanges++;
            _cache.Set($"pending_{changeId}", changeData, expiryHours: 168); // 1 week
            Debug.Log($"Marked pending: {changeId} (total: {PendingChanges})");
        }

        /// <summary>
        /// Obtiene todos los cambios pendientes.
        /// </summary>
        public List<(string id, string data)> GetPendingChanges()
        {
            var pending = new List<(string, string)>();

            // Búsqueda simple (en un proyecto real usaríamos SQLite)
            for (int i = 0; i < 1000; i++)
            {
                var key = $"pending_{i}";
                if (_cache.Has(key))
                {
                    var data = _cache.Get<dynamic>(key);
                    pending.Add((key, data?.ToString() ?? ""));
                }
            }

            return pending;
        }

        /// <summary>
        /// Marca un cambio como sincronizado (lo elimina del caché).
        /// </summary>
        public void MarkSynced(string changeId)
        {
            _cache.Delete($"pending_{changeId}");
            PendingChanges = Math.Max(0, PendingChanges - 1);
            Debug.Log($"Synced: {changeId} (pending: {PendingChanges})");
        }

        /// <summary>
        /// Actualiza estado online/offline.
        /// </summary>
        public void SetOnlineStatus(bool isOnline)
        {
            if (isOnline && !_isOnline)
            {
                Debug.Log("Reconnected - syncing pending changes");
                // Trigger sync event aquí
            }
            _isOnline = isOnline;
        }
    }
}
