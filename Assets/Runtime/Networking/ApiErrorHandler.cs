using UnityEngine;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Manejo centralizado de errores de API.
    /// Convierte errores técnicos a mensajes user-friendly.
    /// </summary>
    public static class ApiErrorHandler
    {
        public static string GetUserFriendlyMessage(int statusCode, string error)
        {
            return statusCode switch
            {
                400 => "❌ Datos inválidos. Revisa tu entrada.",
                401 => "❌ Sesión expirada. Por favor inicia sesión de nuevo.",
                403 => "❌ No tienes permisos para esto.",
                404 => "❌ Recurso no encontrado.",
                429 => "⏳ Estás haciendo muchas peticiones. Espera un momento.",
                500 => "❌ Error del servidor. Intenta más tarde.",
                503 => "❌ Servidor en mantenimiento. Intenta luego.",
                _ => $"❌ Error: {error}"
            };
        }

        public static string GetStatusCodeName(int code)
        {
            return code switch
            {
                200 => "OK",
                201 => "Created",
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                409 => "Conflict",
                429 => "Too Many Requests",
                500 => "Internal Server Error",
                503 => "Service Unavailable",
                _ => $"Unknown ({code})"
            };
        }

        public static bool IsRetryable(int statusCode)
        {
            // Estos errores se pueden reintentar
            return statusCode switch
            {
                408 => true,  // Request Timeout
                429 => true,  // Too Many Requests
                500 => true,  // Internal Server Error
                502 => true,  // Bad Gateway
                503 => true,  // Service Unavailable
                504 => true,  // Gateway Timeout
                _ => false
            };
        }

        public static void LogError(string context, int statusCode, string error)
        {
            Debug.LogError($"[API Error] {context}\n" +
                          $"Status: {statusCode} {GetStatusCodeName(statusCode)}\n" +
                          $"Error: {error}");
        }

        public static void LogWarning(string context, string message)
        {
            Debug.LogWarning($"[API Warning] {context}: {message}");
        }
    }
}
