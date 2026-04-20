using UnityEngine;
using UnityEngine.SceneManagement;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Bootstrap que maneja la navegación de escenas.
    /// Asegura que:
    /// - Session se mantiene persistente entre escenas
    /// - Usuario es redirigido a login si no está autenticado
    /// - Transiciones son suaves
    /// </summary>
    public sealed class SceneBootstrap : MonoBehaviour
    {
        private static SceneBootstrap _instance;

        public static string LoginSceneName = "LoginScene";
        public static string BattleSceneName = "BattleScene";
        public static string MenuSceneName = "MenuScene";
        public static string MainGameSceneName = "MainGame";

        private AuthService _authService;

        private void Awake()
        {
            // Singleton pattern para persistencia entre escenas
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _authService = new AuthService();
        }

        private void Start()
        {
            // Lógica de bootstrap
            var currentScene = SceneManager.GetActiveScene().name;

            if (currentScene == LoginSceneName)
            {
                if (_authService.IsAuthenticated)
                {
                    // Ya autenticado, ir a menú principal
                    LoadScene(MenuSceneName);
                }
            }
            else if (currentScene == BattleSceneName || currentScene == MenuSceneName)
            {
                if (!_authService.IsAuthenticated)
                {
                    // No autenticado, ir a login
                    LoadScene(LoginSceneName);
                }
            }
        }

        /// <summary>
        /// Navega a una escena de forma segura.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            Debug.Log($"[SceneBootstrap] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// Navega a juego (batalla).
        /// </summary>
        public static void LoadBattle()
        {
            LoadScene(BattleSceneName);
        }

        /// <summary>
        /// Navega a menú principal.
        /// </summary>
        public static void LoadMenu()
        {
            LoadScene(MenuSceneName);
        }

        /// <summary>
        /// Navega a gameplay 3D.
        /// </summary>
        public static void LoadMainGame()
        {
            LoadScene(MainGameSceneName);
        }

        /// <summary>
        /// Navega a login y limpia sesión.
        /// </summary>
        public static void LoadLoginAndLogout()
        {
            if (_instance != null && _instance._authService != null)
            {
                _instance._authService.Logout();
            }

            LoadScene(LoginSceneName);
        }

        public AuthService GetAuthService() => _authService;
    }
}
