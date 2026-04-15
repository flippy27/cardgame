using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Pantalla principal del menú con login persistente.
    /// Maneja autenticación, persistencia, y acceso a API.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [Header("Login Panel")]
        public InputField playerIdInput;
        public InputField passwordInput;
        public Button loginButton;
        public Button registerButton;
        public Text statusText;
        public CanvasGroup loginPanelGroup;

        [Header("Main Menu Panel")]
        public Button playButton;
        public Button deckBuilderButton;
        public Button leaderboardButton;
        public Button profileButton;
        public Button settingsButton;
        public Button apiTestButton;
        public Button logoutButton;
        public Text playerNameText;
        public CanvasGroup menuPanelGroup;

        [Header("Screens")]
        public LeaderboardScreen leaderboardScreen;
        public ProfileScreen profileScreen;
        public GameObject settingsPanel;

        [Header("Loading")]
        public CanvasGroup loadingGroup;
        public Text loadingText;

        private AuthService _authService;
        private bool _isLoading;

        private void Start()
        {
            _authService = new AuthService();

            // Setup UI
            if (loginButton != null) loginButton.onClick.AddListener(HandleLogin);
            if (registerButton != null) registerButton.onClick.AddListener(HandleRegister);
            if (logoutButton != null) logoutButton.onClick.AddListener(HandleLogout);
            if (apiTestButton != null) apiTestButton.onClick.AddListener(HandleApiTest);

            // Menu buttons
            if (playButton != null) playButton.onClick.AddListener(HandlePlayButtonPressed);
            if (deckBuilderButton != null) deckBuilderButton.onClick.AddListener(() => SetStatus("Deck Builder: Not implemented yet"));
            if (leaderboardButton != null) leaderboardButton.onClick.AddListener(HandleLeaderboardButtonPressed);
            if (profileButton != null) profileButton.onClick.AddListener(HandleProfileButtonPressed);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettingsButtonPressed);

            // Mostrar login o menú según persistencia
            if (_authService.IsAuthenticated)
            {
                ShowMainMenu();
            }
            else
            {
                ShowLoginPanel();
            }
        }

        private void ShowLoginPanel()
        {
            if (loginPanelGroup != null)
            {
                loginPanelGroup.alpha = 1;
                loginPanelGroup.interactable = true;
                loginPanelGroup.blocksRaycasts = true;
            }

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 0;
                menuPanelGroup.interactable = false;
                menuPanelGroup.blocksRaycasts = false;
            }

            if (statusText != null) statusText.text = "Ingresa tus credenciales";
        }

        private void ShowMainMenu()
        {
            if (loginPanelGroup != null)
            {
                loginPanelGroup.alpha = 0;
                loginPanelGroup.interactable = false;
                loginPanelGroup.blocksRaycasts = false;
            }

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 1;
                menuPanelGroup.interactable = true;
                menuPanelGroup.blocksRaycasts = true;
            }

            if (playerNameText != null)
                playerNameText.text = $"¡Hola, {_authService.CurrentPlayerId}!";

            if (statusText != null) statusText.text = "Autenticado";
        }

        private async void HandleLogin()
        {
            if (_isLoading) return;

            var playerId = playerIdInput?.text ?? "";
            var password = passwordInput?.text ?? "";

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("❌ Ingresa Player ID y Password");
                return;
            }

            _isLoading = true;
            ShowLoading(true, "Autenticando...");

            try
            {
                var success = await _authService.Login(playerId, password);
                if (success)
                {
                    SetStatus("✅ Login exitoso");
                    ShowMainMenu();
                }
                else
                {
                    SetStatus("❌ Credenciales inválidas");
                }
            }
            catch (System.Exception ex)
            {
                SetStatus($"❌ Error: {ex.Message}");
                Debug.LogError($"Login error: {ex}");
            }
            finally
            {
                ShowLoading(false, "");
                _isLoading = false;
            }
        }

        private async void HandleRegister()
        {
            if (_isLoading) return;

            var playerId = playerIdInput?.text ?? "";
            var password = passwordInput?.text ?? "";

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("❌ Ingresa Player ID y Password");
                return;
            }

            _isLoading = true;
            ShowLoading(true, "Registrando...");

            try
            {
                // TODO: Implementar RegisterAsync en AuthService
                SetStatus("⚠️ Registro no implementado aún");
                Debug.LogWarning("Register endpoint not yet implemented");
            }
            catch (System.Exception ex)
            {
                SetStatus($"❌ Error: {ex.Message}");
            }
            finally
            {
                ShowLoading(false, "");
                _isLoading = false;
            }
        }

        private void HandleLogout()
        {
            _authService.Logout();
            ShowLoginPanel();
            SetStatus("Sesión cerrada");

            if (playerIdInput != null) playerIdInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
        }

        private void HandleApiTest()
        {
            SetStatus("🔧 Pantalla de testing de API (TODO)");
            // TODO: Abrir pantalla de testing de API
        }

        private void HandlePlayButtonPressed()
        {
            SetStatus("Cargando batalla...");
            SceneBootstrap.LoadBattle();
        }

        private void HandleLeaderboardButtonPressed()
        {
            SetStatus("Abriendo leaderboard...");
            if (leaderboardScreen != null)
            {
                leaderboardScreen.gameObject.SetActive(true);
            }
            else
            {
                SetStatus("Error: LeaderboardScreen no asignado");
            }
        }

        private void HandleProfileButtonPressed()
        {
            SetStatus("Abriendo perfil...");
            if (profileScreen != null)
            {
                profileScreen.gameObject.SetActive(true);
            }
            else
            {
                SetStatus("Error: ProfileScreen no asignado");
            }
        }

        private void HandleSettingsButtonPressed()
        {
            SetStatus("Abriendo configuración...");
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(!settingsPanel.activeSelf);
            }
            else
            {
                SetStatus("Error: SettingsPanel no asignado");
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                Debug.Log($"[MainMenu] {message}");
            }
        }

        private void ShowLoading(bool show, string message)
        {
            if (loadingGroup != null)
            {
                loadingGroup.alpha = show ? 1 : 0;
                loadingGroup.interactable = show;
                loadingGroup.blocksRaycasts = show;
            }

            if (loadingText != null)
                loadingText.text = message;
        }

        private void OnDestroy()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(HandleLogin);
            if (registerButton != null) registerButton.onClick.RemoveListener(HandleRegister);
            if (logoutButton != null) logoutButton.onClick.RemoveListener(HandleLogout);
            if (apiTestButton != null) apiTestButton.onClick.RemoveListener(HandleApiTest);
            if (playButton != null) playButton.onClick.RemoveListener(HandlePlayButtonPressed);
            if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(HandleLeaderboardButtonPressed);
            if (profileButton != null) profileButton.onClick.RemoveListener(HandleProfileButtonPressed);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleSettingsButtonPressed);
        }
    }
}
