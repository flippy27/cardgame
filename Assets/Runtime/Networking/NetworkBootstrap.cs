using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Coordinador de red que inicializa y mantiene servicios estables.
    /// Inicializa: GameService, NetworkManager, MpsGameSessionService.
    /// </summary>
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5000";
        [SerializeField] private bool autoBootstrapGameService = true;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private MpsGameSessionService sessionService;

        private GameService _gameService;
        private bool _bootstrapped;

        private void Awake()
        {
            // GameService singleton se crea si no existe
            if (GameService.Instance == null)
            {
                var go = new GameObject("GameService");
                _gameService = go.AddComponent<GameService>();
            }
            else
            {
                _gameService = GameService.Instance;
            }

            // Encontrar otros servicios
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }

            if (sessionService == null)
            {
                sessionService = FindFirstObjectByType<MpsGameSessionService>();
            }
        }

        private async void Start()
        {
            if (autoBootstrapGameService && !_bootstrapped)
            {
                await BootstrapGameService();
            }
        }

        /// <summary>
        /// Inicializa GameService: carga catálogo de cartas.
        /// </summary>
        public async Task<bool> BootstrapGameService()
        {
            if (_bootstrapped)
            {
                Debug.LogWarning("GameService already bootstrapped");
                return true;
            }

            try
            {
                Debug.Log("Bootstrapping GameService...");
                var success = await _gameService.Bootstrap();

                if (success)
                {
                    _bootstrapped = true;
                    Debug.Log("GameService bootstrap complete");
                    return true;
                }

                Debug.LogError("GameService bootstrap failed");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GameService bootstrap error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cierra NGO y libera recursos.
        /// </summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            if (_gameService != null)
            {
                _gameService.Logout();
            }
        }

        private void OnDestroy()
        {
            // No destruir GameService, es singleton
        }
    }
}
