using UnityEngine;

namespace Flippy.CardDuelMobile.Core
{
    public enum LogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3 }

    public static class GameLogger
    {
        private static LogLevel _minLevel = LogLevel.Debug;

        public static void SetLogLevel(LogLevel level) => _minLevel = level;

        public static void Debug(string tag, string message)
        {
            if (_minLevel <= LogLevel.Debug)
                UnityEngine.Debug.Log($"[{tag}] {message}");
        }

        public static void Info(string tag, string message)
        {
            if (_minLevel <= LogLevel.Info)
                UnityEngine.Debug.Log($"[{tag}] {message}");
        }

        public static void Warning(string tag, string message)
        {
            if (_minLevel <= LogLevel.Warning)
                UnityEngine.Debug.LogWarning($"[{tag}] {message}");
        }

        public static void Error(string tag, string message)
        {
            if (_minLevel <= LogLevel.Error)
                UnityEngine.Debug.LogError($"[{tag}] {message}");
        }

        public static void Error(string tag, string message, System.Exception ex)
        {
            if (_minLevel <= LogLevel.Error)
                UnityEngine.Debug.LogError($"[{tag}] {message}\n{ex}");
        }
    }
}
