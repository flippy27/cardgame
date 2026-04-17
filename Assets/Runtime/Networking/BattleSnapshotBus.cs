using System;
using UnityEngine;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Bus mínimo para propagar snapshots a la UI.
    /// </summary>
    public static class BattleSnapshotBus
    {
        public static event Action<string> SnapshotReceived;
        private static string _lastSnapshot;

        /// <summary>
        /// Publica snapshot serializado.
        /// </summary>
        public static void Publish(string json)
        {
            _lastSnapshot = json;
            SnapshotReceived?.Invoke(json);
        }

        /// <summary>
        /// Suscribirse y recibir inmediatamente el último snapshot si existe.
        /// </summary>
        public static void SubscribeAndGetLast(Action<string> handler)
        {
            SnapshotReceived += handler;
            if (!string.IsNullOrEmpty(_lastSnapshot))
            {
                Debug.Log("[BattleSnapshotBus] Invoking cached snapshot immediately");
                handler(_lastSnapshot);
            }
        }
    }
}
