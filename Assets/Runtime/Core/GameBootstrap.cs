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

            // 1. Load config (FIRST - before creating any services)
            ConfigManager.LoadConfig();
            GameLogger.SetLogLevel(ConfigManager.GetLogLevel());
            var apiBaseUrl = ConfigManager.GetApiBaseUrl();
            GameLogger.Info("Bootstrap", $"API Base URL: {apiBaseUrl}");

            // 2. Register services in DI (with config from ConfigManager)
            var apiClient = new CardGameApiClient(apiBaseUrl);
            ServiceLocator.Register(apiClient);

            var authService = new AuthService(apiBaseUrl);
            ServiceLocator.Register(authService);

            var userService = new Services.UserService(authService);
            ServiceLocator.Register<IUserService>(userService);

            var gameService = new Services.GameService(apiClient);
            ServiceLocator.Register<IGameService>(gameService);

            // Register specialized networking services
            var deckService = new DeckManagementService(apiClient, authService);
            ServiceLocator.Register<DeckManagementService>(deckService);

            var profileService = new UserProfileService(apiClient, authService);
            ServiceLocator.Register<UserProfileService>(profileService);

            var leaderboardService = new LeaderboardService(apiClient, authService);
            ServiceLocator.Register<LeaderboardService>(leaderboardService);

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

            // 5. Ensure MatchActionService exists
            var actionService = FindFirstObjectByType<MatchActionService>();
            if (actionService == null)
            {
                var actionServiceGo = new GameObject("MatchActionService");
                actionService = actionServiceGo.AddComponent<MatchActionService>();
                GameLogger.Info("Bootstrap", "Created MatchActionService");
            }

            // 6. Ensure MatchCheckpointService exists
            var checkpointService = FindFirstObjectByType<MatchCheckpointService>();
            if (checkpointService == null)
            {
                var checkpointServiceGo = new GameObject("MatchCheckpointService");
                checkpointService = checkpointServiceGo.AddComponent<MatchCheckpointService>();
                GameLogger.Info("Bootstrap", "Created MatchCheckpointService");
            }

            // 7. Ensure GameInitializationService exists (auto-loads catalog on login)
            var initService = FindFirstObjectByType<GameInitializationService>();
            if (initService == null)
            {
                var initServiceGo = new GameObject("GameInitializationService");
                initService = initServiceGo.AddComponent<GameInitializationService>();
                GameLogger.Info("Bootstrap", "Created GameInitializationService");
            }

            GameLogger.Info("Bootstrap", "Game initialized successfully");
            GameEvents.RaiseConnected();

            _initialized = true;
        }
    }
}
