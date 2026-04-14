using System;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Bus mínimo para propagar snapshots a la UI.
    /// </summary>
    public static class BattleSnapshotBus
    {
        public static event Action<string> SnapshotReceived;

        /// <summary>
        /// Publica snapshot serializado.
        /// </summary>
        public static void Publish(string json)
        {
            SnapshotReceived?.Invoke(json);
        }
    }
}
