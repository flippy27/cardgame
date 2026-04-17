using System.Threading.Tasks;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Services
{
    public interface IGameService
    {
        Task<bool> Initialize();
        Task LoadCardCatalog();
        Task<bool> StartMatch(string matchId);
        Task<bool> EndMatch(string matchId);
        bool IsInitialized { get; }
    }

    public class GameService : IGameService
    {
        private readonly Networking.CardGameApiClient _apiClient;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        public GameService(Networking.CardGameApiClient apiClient = null)
        {
            _apiClient = apiClient ?? new Networking.CardGameApiClient();
        }

        public async Task<bool> Initialize()
        {
            try
            {
                await LoadCardCatalog();
                _initialized = true;
                GameLogger.Info("Game", "Initialized");
                return true;
            }
            catch (System.Exception ex)
            {
                GameLogger.Error("Game", "Initialize failed", ex);
                return false;
            }
        }

        public async Task LoadCardCatalog()
        {
            var cards = await _apiClient.FetchAllCards();
            Core.LocalCache.Set("cards", cards, ttlSeconds: 3600);
            GameLogger.Info("Game", $"Loaded {cards.Count} cards");
            GameEvents.RaiseCardCatalogLoaded();
        }

        public async Task<bool> StartMatch(string matchId)
        {
            GameLogger.Info("Game", $"Match started: {matchId}");
            GameEvents.RaiseMatchStarted();
            return true;
        }

        public async Task<bool> EndMatch(string matchId)
        {
            GameLogger.Info("Game", $"Match ended: {matchId}");
            GameEvents.RaiseMatchEnded();
            return true;
        }
    }
}
