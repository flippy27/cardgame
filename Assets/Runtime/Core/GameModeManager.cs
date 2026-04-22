using UnityEngine;
using UnityEngine.SceneManagement;
using Flippy.CardDuelMobile.SinglePlayer;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Gestiona el modo de juego actual (local vs online con Netcode).
    /// Activa/desactiva coordinadores según el modo.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [SerializeField] private bool _isLocalMode = true;
        private LocalSinglePlayerCoordinator _localCoord;
        private NetworkBootstrap _networkBootstrap;

        public bool IsLocalMode => _isLocalMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RefreshSceneReferences();
            ApplyMode();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// Activa modo local (AI vs Player, sin Netcode).
        /// </summary>
        public void SetLocalMode()
        {
            _isLocalMode = true;
            ApplyMode();
            GameLogger.Info("GameMode", "Switched to LOCAL mode");
        }

        /// <summary>
        /// Activa modo online (Multiplayer con Netcode for GameObjects).
        /// </summary>
        public void SetOnlineMode()
        {
            _isLocalMode = false;
            ApplyMode();
            GameLogger.Info("GameMode", "Switched to ONLINE mode");
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshSceneReferences();
            ApplyMode();
        }

        private void RefreshSceneReferences()
        {
            _localCoord = FindFirstObjectByType<LocalSinglePlayerCoordinator>();
            _networkBootstrap = FindFirstObjectByType<NetworkBootstrap>();
        }

        private void ApplyMode()
        {
            if (_localCoord != null)
            {
                _localCoord.gameObject.SetActive(_isLocalMode);
            }

            if (_networkBootstrap != null)
            {
                _networkBootstrap.gameObject.SetActive(!_isLocalMode);
            }
        }
    }
}
