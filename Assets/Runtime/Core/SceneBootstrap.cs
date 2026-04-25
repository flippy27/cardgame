using UnityEngine;
using UnityEngine.SceneManagement;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Handles scene navigation for the current two-scene setup.
    /// MainMenu acts as both login entry point and multiplayer lobby.
    /// MainGame is the authenticated gameplay scene.
    /// </summary>
    public sealed class SceneBootstrap : MonoBehaviour
    {
        private static SceneBootstrap _instance;

        public static string LoginSceneName = "MainMenu";
        public static string BattleSceneName = "MainGame";
        public static string MenuSceneName = "MainMenu";
        public static string MainGameSceneName = "MainGame";

        private AuthService _authService;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _authService = ServiceLocator.TryResolve<AuthService>(out var authService)
                ? authService
                : new AuthService();
        }

        private void Start()
        {
            var currentScene = SceneManager.GetActiveScene().name;

            // Only protect gameplay scenes; MainMenu itself can handle unauthenticated users.
            if ((currentScene == BattleSceneName || currentScene == MainGameSceneName) &&
                !_authService.IsAuthenticated)
            {
                LoadScene(MenuSceneName);
            }
        }

        public static void LoadScene(string sceneName)
        {
            //Debug.Log($"[SceneBootstrap] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public static void LoadBattle()
        {
            LoadScene(MainGameSceneName);
        }

        public static void LoadMenu()
        {
            LoadScene(MenuSceneName);
        }

        public static void LoadMainGame()
        {
            LoadScene(MainGameSceneName);
        }

        public static void LoadLoginAndLogout()
        {
            if (_instance != null && _instance._authService != null)
            {
                _instance._authService.Logout();
            }

            LoadScene(MenuSceneName);
        }

        public AuthService GetAuthService() => _authService;
    }
}
