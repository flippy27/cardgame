using UnityEngine;
using Unity.Netcode;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Ayuda a mantener un stack de red estable en la escena.
    /// </summary>
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        public NetworkManager networkManager;
        public MpsGameSessionService sessionService;

        private void Awake()
        {
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }

            if (sessionService == null)
            {
                sessionService = FindFirstObjectByType<MpsGameSessionService>();
            }
        }

        /// <summary>
        /// Cierra NGO si sigue vivo.
        /// </summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }
    }
}
