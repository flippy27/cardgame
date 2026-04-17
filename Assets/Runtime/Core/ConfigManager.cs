using UnityEngine;

namespace Flippy.CardDuelMobile.Core
{
    [System.Serializable]
    public class AppSettings
    {
        public ApiSettings api = new();
        public GameSettings game = new();
        public HealthSettings health = new();
        public SecuritySettings security = new();
    }

    [System.Serializable]
    public class ApiSettings
    {
        public string baseUrl = "http://localhost:5000";
        public int timeoutSeconds = 30;
        public int maxRetries = 3;
        public int retryDelayMs = 500;
    }

    [System.Serializable]
    public class GameSettings
    {
        public string logLevel = "Debug";
        public bool enableOfflineMode = true;
        public int cacheTimeSeconds = 3600;
        public bool enableMetrics = true;
    }

    [System.Serializable]
    public class HealthSettings
    {
        public int pingIntervalSeconds = 30;
        public int maxConsecutiveFailures = 3;
    }

    [System.Serializable]
    public class SecuritySettings
    {
        public bool enableRequestSigning = true;
        public bool enableCertPinning = false;
    }

    public static class ConfigManager
    {
        private static AppSettings _config;

        public static void LoadConfig()
        {
            var json = Resources.Load<TextAsset>("config");
            if (json == null)
            {
                GameLogger.Warning("Config", "config.json not found, using defaults");
                _config = new AppSettings();
                return;
            }

            try
            {
                _config = JsonUtility.FromJson<AppSettings>(json.text);
                GameLogger.Info("Config", "Loaded config from Resources/config.json");
            }
            catch (System.Exception ex)
            {
                GameLogger.Error("Config", "Failed to parse config", ex);
                _config = new AppSettings();
            }
        }

        public static string GetApiBaseUrl() => _config?.api?.baseUrl ?? "http://localhost:5000";
        public static int GetApiTimeout() => _config?.api?.timeoutSeconds ?? 30;
        public static int GetMaxRetries() => _config?.api?.maxRetries ?? 3;
        public static LogLevel GetLogLevel()
        {
            var level = _config?.game?.logLevel ?? "Debug";
            return System.Enum.TryParse<LogLevel>(level, out var result) ? result : LogLevel.Debug;
        }
        public static bool IsOfflineModeEnabled() => _config?.game?.enableOfflineMode ?? true;
        public static int GetCacheTTL() => _config?.game?.cacheTimeSeconds ?? 3600;
    }
}
