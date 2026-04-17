using UnityEngine;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.SinglePlayer;

namespace Flippy.CardDuelMobile.Core
{
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        private CardDuelNetworkCoordinator _networkCoordinator;
        private LocalSinglePlayerCoordinator _localCoordinator;
        private bool _isLocalMode;

        public bool IsLocalMode => _isLocalMode;
        public bool IsOnlineMode => !_isLocalMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _networkCoordinator = FindFirstObjectByType<CardDuelNetworkCoordinator>();
            _localCoordinator = FindFirstObjectByType<LocalSinglePlayerCoordinator>();

            SetOnlineMode();
        }

        public void SetLocalMode()
        {
            GameLogger.Info("GameMode", "Switching to LOCAL mode");
            _isLocalMode = true;

            // Disable network first
            if (_networkCoordinator != null)
            {
                _networkCoordinator.enabled = false;
                GameLogger.Info("GameMode", "Disabled CardDuelNetworkCoordinator");
            }

            // Enable and start local
            if (_localCoordinator != null)
            {
                _localCoordinator.enabled = true;
                if (!_localCoordinator.IsActive)
                {
                    _localCoordinator.StartMatch();
                    GameLogger.Info("GameMode", "Started LocalSinglePlayerCoordinator match");
                }
                else
                {
                    GameLogger.Info("GameMode", "LocalSinglePlayerCoordinator already active");
                }
            }
        }

        public void SetOnlineMode()
        {
            GameLogger.Info("GameMode", "Switching to ONLINE mode");
            _isLocalMode = false;

            // Disable local and reset it
            if (_localCoordinator != null)
            {
                GameLogger.Info("GameMode", "Stopping LocalSinglePlayerCoordinator");
                _localCoordinator.enabled = false;
                _localCoordinator.gameObject.SetActive(false);
                _localCoordinator.gameObject.SetActive(true);
                _localCoordinator.enabled = false;
                GameLogger.Info("GameMode", "Disabled and reset LocalSinglePlayerCoordinator");
            }

            // Enable network
            if (_networkCoordinator != null)
            {
                _networkCoordinator.enabled = true;
                GameLogger.Info("GameMode", "Enabled CardDuelNetworkCoordinator");
            }
        }
    }
}
