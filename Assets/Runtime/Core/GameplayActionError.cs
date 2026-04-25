using System;
using UnityEngine;

namespace Flippy.CardDuelMobile.Core
{
    [Serializable]
    public sealed class GameplayActionErrorPayload
    {
        public string code;
        public string message;
    }

    public sealed class GameplayActionException : Exception
    {
        public string ErrorCode { get; }
        public int HttpStatus { get; }
        public string UserMessage { get; }

        public GameplayActionException(string errorCode, string userMessage, int httpStatus = 0, Exception innerException = null)
            : base(userMessage ?? errorCode ?? "Gameplay action failed.", innerException)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "unknown_gameplay_error" : errorCode;
            UserMessage = string.IsNullOrWhiteSpace(userMessage) ? "Gameplay action failed." : userMessage;
            HttpStatus = httpStatus;
        }
    }

    public static class GameplayActionErrorParser
    {
        public static bool TryParse(string rawPayload, out GameplayActionErrorPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                return false;
            }

            var trimmed = rawPayload.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                payload = JsonUtility.FromJson<GameplayActionErrorPayload>(trimmed);
                return payload != null &&
                       (!string.IsNullOrWhiteSpace(payload.code) || !string.IsNullOrWhiteSpace(payload.message));
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        public static Exception ToException(string rawPayload, int httpStatus = 0, string fallbackMessage = null)
        {
            if (TryParse(rawPayload, out var payload))
            {
                return new GameplayActionException(payload.code, payload.message, httpStatus);
            }

            return new InvalidOperationException(string.IsNullOrWhiteSpace(fallbackMessage) ? rawPayload : fallbackMessage);
        }

        public static string ToUserMessage(Exception exception, string actionName = null)
        {
            if (exception is GameplayActionException gameplayActionException)
            {
                return gameplayActionException.UserMessage;
            }

            if (TryParse(exception?.Message, out var payload))
            {
                return string.IsNullOrWhiteSpace(payload.message)
                    ? BuildFallbackMessage(actionName, exception?.Message)
                    : payload.message;
            }

            return BuildFallbackMessage(actionName, exception?.Message);
        }

        public static string ExtractCode(Exception exception)
        {
            if (exception is GameplayActionException gameplayActionException)
            {
                return gameplayActionException.ErrorCode;
            }

            return TryParse(exception?.Message, out var payload) ? payload.code : null;
        }

        public static bool ShouldRefreshSnapshot(Exception exception)
        {
            var code = ExtractCode(exception);
            return string.Equals(code, "not_your_turn", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFallbackMessage(string actionName, string message)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return string.IsNullOrWhiteSpace(message) ? "Gameplay action failed." : message;
            }

            return string.IsNullOrWhiteSpace(message)
                ? $"{actionName} failed."
                : $"{actionName} failed: {message}";
        }
    }
}
