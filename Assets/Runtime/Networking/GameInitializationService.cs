using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Maneja la inicialización del juego después del login.
    /// Carga catálogo, decks del jugador, profile, etc.
    /// Se ejecuta una sola vez cuando el usuario se autentica.
    /// </summary>
    public sealed class GameInitializationService : MonoBehaviour
    {
        private CardCatalogCache _cardCatalog;
        private DeckManagementService _deckService;
        private UserProfileService _profileService;
        private bool _initialized = false;

        public static GameInitializationService Instance { get; private set; }

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

        private void OnEnable()
        {
            // Subscribe to auth events
            GameEvents.Connected += OnAuthConnected;
            GameEvents.Disconnected += OnAuthDisconnected;
        }

        private void OnDisable()
        {
            GameEvents.Connected -= OnAuthConnected;
            GameEvents.Disconnected -= OnAuthDisconnected;
        }

        /// <summary>
        /// Se dispara cuando el usuario se autentica.
        /// Carga catálogo y decks del jugador.
        /// </summary>
        private async void OnAuthConnected()
        {
            if (_initialized)
                return;

            GameLogger.Info("Init", "User authenticated, initializing game data");

            // Wait for services to be registered
            await Task.Delay(500);

            try
            {
                // Get services from locator
                _cardCatalog = ServiceLocator.Get<CardCatalogCache>();
                _deckService = ServiceLocator.Get<DeckManagementService>();
                _profileService = ServiceLocator.Get<UserProfileService>();

                if (_cardCatalog == null || _deckService == null || _profileService == null)
                {
                    GameLogger.Warning("Init", "Some services not registered yet");
                    return;
                }

                // Load catalog (critical for gameplay)
                GameLogger.Info("Init", "Loading card catalog...");
                await _cardCatalog.LoadCatalog();

                if (!_cardCatalog.IsLoaded)
                {
                    GameLogger.Error("Init", "Failed to load card catalog");
                    return;
                }

                // Load player's decks
                GameLogger.Info("Init", "Loading player decks...");
                var decks = await _deckService.GetPlayerDecksAsync();
                GameLogger.Info("Init", $"Loaded {decks.Count} decks");

                // Load player profile (non-critical, just for UI)
                GameLogger.Info("Init", "Loading player profile...");
                var profile = await _profileService.GetProfileAsync();
                if (profile != null)
                {
                    GameLogger.Info("Init", $"Player: {profile.username} (Rating: {profile.rating})");
                }

                _initialized = true;
                GameLogger.Info("Init", "Game initialization complete");
            }
            catch (System.Exception ex)
            {
                GameLogger.Error("Init", $"Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Se dispara cuando el usuario se desautentica.
        /// Limpia cachés y datos.
        /// </summary>
        private void OnAuthDisconnected()
        {
            if (_cardCatalog != null)
                _cardCatalog.Clear();

            if (_deckService != null)
                _deckService.ClearCache();

            if (_profileService != null)
                _profileService.ClearCache();

            _initialized = false;
            GameLogger.Info("Init", "Game data cleared");
        }

        /// <summary>
        /// Verifica si el catálogo está cargado.
        /// </summary>
        public bool IsCatalogReady => _cardCatalog != null && _cardCatalog.IsLoaded;

        /// <summary>
        /// Verifica si la inicialización está completa.
        /// </summary>
        public bool IsInitialized => _initialized && IsCatalogReady;
    }
}
