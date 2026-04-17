using UnityEngine;
using Flippy.CardDuelMobile.Services;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.UI;

namespace Flippy.CardDuelMobile.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_initialized) return;

            GameLogger.Info("Bootstrap", "Initializing game services");

            // 1. Load config
            ConfigManager.LoadConfig();
            GameLogger.SetLogLevel(ConfigManager.GetLogLevel());

            // 2. Register services in DI
            var apiClient = new CardGameApiClient();
            ServiceLocator.Register(apiClient);

            var authService = new AuthService();
            ServiceLocator.Register(authService);

            var userService = new Services.UserService(authService);
            ServiceLocator.Register<IUserService>(userService);

            var gameService = new Services.GameService(apiClient);
            ServiceLocator.Register<IGameService>(gameService);

            // 3. Start background tasks
            var healthPinger = new GameObject("HealthCheckPinger").AddComponent<HealthCheckPinger>();
            healthPinger.Initialize(ConfigManager.GetApiBaseUrl());
            DontDestroyOnLoad(healthPinger.gameObject);

            // 4. Ensure MatchUIManager exists in scene
            var matchUiManager = FindFirstObjectByType<MatchUIManager>();
            if (matchUiManager == null)
            {
                var matchUiGo = new GameObject("MatchUIManager");
                matchUiManager = matchUiGo.AddComponent<MatchUIManager>();
                GameLogger.Info("Bootstrap", "Created MatchUIManager");
            }

            GameLogger.Info("Bootstrap", "Game initialized successfully");
            GameEvents.RaiseConnected();

            _initialized = true;
        }
    }
}
