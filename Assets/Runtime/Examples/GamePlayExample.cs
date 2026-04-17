using UnityEngine;
using System.Threading.Tasks;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Services;

namespace Flippy.CardDuelMobile.Examples
{
    /// <summary>
    /// Example usage of the new service-based architecture.
    /// Shows how to use DI, services, events, logging, metrics, and caching.
    /// </summary>
    public class GamePlayExample : MonoBehaviour
    {
        private IUserService _userService;
        private IGameService _gameService;

        private async void Start()
        {
            // GameBootstrap.Initialize() runs automatically at startup
            // Resolve services from DI container
            _userService = ServiceLocator.Resolve<IUserService>();
            _gameService = ServiceLocator.Resolve<IGameService>();

            // Subscribe to events
            GameEvents.OnConnected += OnServerConnected;
            GameEvents.OnAuthFailed += OnAuthFailed;
            GameEvents.OnCardCatalogLoaded += OnCardsLoaded;

            // Example flow
            await ExampleGameFlow();
        }

        private async Task ExampleGameFlow()
        {
            GameLogger.Info("Example", "Starting game flow");

            // 1. Login
            var loginSuccess = await _userService.Login("player@example.com", "password123");
            if (!loginSuccess)
            {
                GameLogger.Error("Example", "Login failed");
                return;
            }

            GameLogger.Info("Example", $"Logged in as {_userService.GetCurrentUserId()}");

            // 2. Initialize game (loads catalog from cache or network)
            var initSuccess = await _gameService.Initialize();
            if (!initSuccess)
            {
                GameLogger.Error("Example", "Game init failed");
                return;
            }

            // 3. Check cache (offline support)
            // (Note: string not serializable. Use custom classes instead)
            GameLogger.Info("Example", "Cache ready for offline mode");

            // 4. Start match
            await _gameService.StartMatch("match-123");

            // 5. Print metrics
            MetricsCollector.PrintStats();
        }

        private void OnServerConnected()
        {
            GameLogger.Info("Example", "Server connected!");
        }

        private void OnAuthFailed(ApiErrorCode code)
        {
            GameLogger.Warning("Example", $"Auth failed: {code}");
        }

        private void OnCardsLoaded()
        {
            GameLogger.Info("Example", "Cards loaded, ready to play");
        }

        private void OnDestroy()
        {
            GameEvents.OnConnected -= OnServerConnected;
            GameEvents.OnAuthFailed -= OnAuthFailed;
            GameEvents.OnCardCatalogLoaded -= OnCardsLoaded;
        }
    }
}
