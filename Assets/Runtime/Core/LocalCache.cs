using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.Core
{
    public static class LocalCache
    {
        private const string CACHE_PREFIX = "cache_";

        public static void Set<T>(string key, T value, int ttlSeconds = -1)
        {
            try
            {
                var json = JsonUtility.ToJson(value);
                PlayerPrefs.SetString(CACHE_PREFIX + key, json);
                if (ttlSeconds > 0)
                    PlayerPrefs.SetInt(CACHE_PREFIX + key + "_expires", (int)(Time.realtimeSinceStartup + ttlSeconds));
                PlayerPrefs.Save();
                GameLogger.Debug("Cache", $"SET {key}");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Cache", $"Failed to cache {key}", ex);
            }
        }

        public static bool TryGet<T>(string key, out T value) where T : new()
        {
            try
            {
                var cacheKey = CACHE_PREFIX + key;
                if (!PlayerPrefs.HasKey(cacheKey))
                {
                    value = default;
                    return false;
                }

                var expiresKey = cacheKey + "_expires";
                if (PlayerPrefs.HasKey(expiresKey))
                {
                    var expires = PlayerPrefs.GetInt(expiresKey);
                    if (Time.realtimeSinceStartup > expires)
                    {
                        PlayerPrefs.DeleteKey(cacheKey);
                        PlayerPrefs.DeleteKey(expiresKey);
                        value = default;
                        return false;
                    }
                }

                var json = PlayerPrefs.GetString(cacheKey);
                value = JsonUtility.FromJson<T>(json);
                GameLogger.Debug("Cache", $"HIT {key}");
                return true;
            }
            catch (Exception ex)
            {
                GameLogger.Error("Cache", $"Failed to retrieve {key}", ex);
                value = default;
                return false;
            }
        }

        public static void Clear(string key)
        {
            PlayerPrefs.DeleteKey(CACHE_PREFIX + key);
            PlayerPrefs.DeleteKey(CACHE_PREFIX + key + "_expires");
        }

        public static void ClearAll()
        {
            PlayerPrefs.DeleteAll();
            GameLogger.Info("Cache", "Cleared all");
        }
    }
}
