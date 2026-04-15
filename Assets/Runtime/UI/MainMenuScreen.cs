using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;

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

            // Placeholder para otros botones
            if (playButton != null) playButton.onClick.AddListener(() => SetStatus("Play: Not implemented"));
            if (deckBuilderButton != null) deckBuilderButton.onClick.AddListener(() => SetStatus("Deck Builder: Not implemented"));
            if (leaderboardButton != null) leaderboardButton.onClick.AddListener(() => SetStatus("Leaderboard: Not implemented"));
            if (profileButton != null) profileButton.onClick.AddListener(() => SetStatus("Profile: Not implemented"));
            if (settingsButton != null) settingsButton.onClick.AddListener(() => SetStatus("Settings: Not implemented"));

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
        }
    }
}
