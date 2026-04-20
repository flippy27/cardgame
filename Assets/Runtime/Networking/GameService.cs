using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Coordinador único para todos los servicios de red.
    /// Punto de entrada para: auth, cartas, match history, etc.
    /// </summary>
    public sealed class GameService : MonoBehaviour
    {
        public static GameService Instance { get; private set; }

        public CardGameApiClient ApiClient { get; private set; }
        public AuthService AuthService { get; private set; }
        public CardCatalogCache CardCatalog { get; private set; }
        public MatchHistoryService MatchHistory { get; private set; }
        public UserService UserService { get; private set; }
        public MatchmakingService Matchmaking { get; private set; }
        public LocalCacheService LocalCache { get; private set; }
        public OfflineSyncService OfflineSync { get; private set; }

        public bool IsReady { get; private set; }
        public bool IsCatalogReady => CardCatalog.IsLoaded;
        public bool IsAuthenticated => AuthService.IsAuthenticated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeServices();
        }

        private void InitializeServices()
        {
            try
            {
                var apiBaseUrl = ConfigManager.GetApiBaseUrl();

                ApiClient = new CardGameApiClient(apiBaseUrl);
                AuthService = new AuthService(apiBaseUrl);
                CardCatalog = new CardCatalogCache(ApiClient);
                ServiceLocator.Register(CardCatalog);

                var userApiClient = new UserApiClient(apiBaseUrl);
                UserService = new UserService(userApiClient, AuthService);

                var matchHistoryApiClient = new MatchHistoryApiClient(apiBaseUrl);
                MatchHistory = new MatchHistoryService(matchHistoryApiClient, AuthService);

                var matchmakingApiClient = new MatchmakingApiClient(apiBaseUrl);
                Matchmaking = new MatchmakingService(matchmakingApiClient, AuthService);

                LocalCache = new LocalCacheService();
                OfflineSync = new OfflineSyncService(LocalCache, ApiClient);

                IsReady = true;
                Debug.Log($"GameService initialized. API: {apiBaseUrl}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize GameService: {ex.Message}");
                IsReady = false;
            }
        }

        /// <summary>
        /// Inicializa todo lo necesario para empezar a jugar.
        /// Carga catálogo de cartas.
        /// </summary>
        public async Task<bool> Bootstrap()
        {
            if (!IsReady)
            {
                Debug.LogError("GameService not ready");
                return false;
            }

            try
            {
                Debug.Log("Bootstrapping: loading card catalog...");
                await CardCatalog.LoadCatalog();

                if (!CardCatalog.IsLoaded)
                {
                    Debug.LogError("Failed to load card catalog");
                    return false;
                }

                var (total, withAbilities) = CardCatalog.GetStats();
                Debug.Log($"Card catalog loaded: {total} cards, {withAbilities} with abilities");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Bootstrap failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Login con player ID y password.
        /// </summary>
        public async Task<bool> Login(string playerId, string password)
        {
            if (!IsReady)
            {
                Debug.LogError("GameService not ready");
                return false;
            }

            try
            {
                var result = await AuthService.Login(playerId, password);
                if (result)
                {
                    Debug.Log($"Logged in as {playerId}");
                    return true;
                }

                Debug.LogError("Login failed");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Login error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logout.
        /// </summary>
        public void Logout()
        {
            AuthService.Logout();
            MatchHistory.ClearCache();
            Debug.Log("Logged out");
        }

        /// <summary>
        /// Carga historial de matches del jugador autenticado.
        /// </summary>
        public async Task<MatchHistoryPage> LoadMatchHistory(int page = 1, int pageSize = 20)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            try
            {
                var history = await MatchHistory.FetchHistory(AuthService.CurrentPlayerId, page, pageSize);
                return history;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load match history: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene estadísticas del catálogo.
        /// </summary>
        public CardStatsDto GetCardStats()
        {
            if (!IsCatalogReady)
            {
                throw new InvalidGameStateException("Catalog not loaded. Call Bootstrap() first.");
            }

            var (total, withAbilities) = CardCatalog.GetStats();
            return new CardStatsDto
            {
                totalCards = total,
                cardsWithAbilities = withAbilities,
                manaCostAvg = 2.5f, // placeholder
                attackAvg = 2.3f,
                healthAvg = 2.8f
            };
        }

        /// <summary>
        /// Valida un mazo contra el catálogo.
        /// </summary>
        public DeckValidationResult ValidateDeck(System.Collections.Generic.IEnumerable<string> cardIds)
        {
            if (!IsCatalogReady)
            {
                return new DeckValidationResult
                {
                    IsValid = false
                };
            }

            return CardCatalog.ValidateDeck(cardIds);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
