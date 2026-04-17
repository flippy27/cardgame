using System;

namespace Flippy.CardDuelMobile.Core
{
    public static class GameEvents
    {
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<string> OnError;
        public static event Action<ApiErrorCode> OnAuthFailed;
        public static event Action OnCardCatalogLoaded;
        public static event Action OnMatchStarted;
        public static event Action OnMatchEnded;

        public static void RaiseConnected()
        {
            UnityEngine.Debug.Log("[Events] Connected");
            OnConnected?.Invoke();
        }

        public static void RaiseDisconnected()
        {
            UnityEngine.Debug.Log("[Events] Disconnected");
            OnDisconnected?.Invoke();
        }

        public static void RaiseError(string message)
        {
            UnityEngine.Debug.LogError($"[Events] Error: {message}");
            OnError?.Invoke(message);
        }

        public static void RaiseAuthFailed(ApiErrorCode code)
        {
            UnityEngine.Debug.LogWarning($"[Events] Auth failed: {code}");
            OnAuthFailed?.Invoke(code);
        }

        public static void RaiseCardCatalogLoaded()
        {
            UnityEngine.Debug.Log("[Events] Card catalog loaded");
            OnCardCatalogLoaded?.Invoke();
        }

        public static void RaiseMatchStarted()
        {
            UnityEngine.Debug.Log("[Events] Match started");
            OnMatchStarted?.Invoke();
        }

        public static void RaiseMatchEnded()
        {
            UnityEngine.Debug.Log("[Events] Match ended");
            OnMatchEnded?.Invoke();
        }
    }
}
