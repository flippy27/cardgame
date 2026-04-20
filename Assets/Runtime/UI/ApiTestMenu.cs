#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Menú de testing para probar endpoints de API directamente.
    /// Accessible desde Settings o con debug key.
    ///
    /// Facilita:
    /// - Testing de autenticación
    /// - Llamadas manuales a endpoints
    /// - Inspección de respuestas JSON
    /// - Debugging sin necesidad de builds
    /// </summary>
    public sealed class ApiTestMenu : MonoBehaviour
    {
        [Header("Auth Inputs")]
        public InputField emailInput;
        public InputField passwordInput;
        public InputField usernameInput;

        [Header("Deck Inputs")]
        public InputField deckIdInput;

        [Header("User Inputs")]
        public InputField userIdInput;

        [Header("Match Inputs")]
        public InputField matchIdInput;

        [Header("Buttons")]
        public Button loginButton;
        public Button registerButton;
        public Button logoutButton;
        public Button getProfileButton;
        public Button listDecksButton;
        public Button listCardsButton;
        public Button searchCardsButton;
        public Button getMatchHistoryButton;
        public Button clearOutputButton;
        public Button copyOutputButton;

        [Header("Output")]
        public Text outputText;
        public ScrollRect outputScroll;
        public CanvasGroup menuGroup;

        private AuthService _authService;
        private CardGameApiClient _apiClient;
        private List<string> _requestHistory = new();

        private void Start()
        {
            _authService = new AuthService();
            _apiClient = new CardGameApiClient();

            // Setup buttons
            if (loginButton != null) loginButton.onClick.AddListener(HandleLogin);
            if (registerButton != null) registerButton.onClick.AddListener(HandleRegister);
            if (logoutButton != null) logoutButton.onClick.AddListener(HandleLogout);
            if (getProfileButton != null) getProfileButton.onClick.AddListener(HandleGetProfile);
            if (listDecksButton != null) listDecksButton.onClick.AddListener(HandleListDecks);
            if (listCardsButton != null) listCardsButton.onClick.AddListener(HandleListCards);
            if (searchCardsButton != null) searchCardsButton.onClick.AddListener(HandleSearchCards);
            if (getMatchHistoryButton != null) getMatchHistoryButton.onClick.AddListener(HandleGetMatchHistory);
            if (clearOutputButton != null) clearOutputButton.onClick.AddListener(ClearOutput);
            if (copyOutputButton != null) copyOutputButton.onClick.AddListener(CopyOutput);

            // Hide menu initially
            if (menuGroup != null)
            {
                menuGroup.alpha = 0;
                menuGroup.interactable = false;
                menuGroup.blocksRaycasts = false;
            }

            Log("🔧 API Testing Menu Ready");
            Log($"Auth: {(_authService.IsAuthenticated ? "✅ Authenticated" : "❌ Not authenticated")}");
            Log("Press F10 to toggle menu");
        }

        private void Update()
        {
            // F10 toggle menu
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
            {
                ToggleMenu();
            }
        }

        private void ToggleMenu()
        {
            if (menuGroup == null) return;

            var show = menuGroup.alpha < 0.5f;
            menuGroup.alpha = show ? 1 : 0;
            menuGroup.interactable = show;
            menuGroup.blocksRaycasts = show;
        }

        // ========== AUTH ==========

        private async void HandleLogin()
        {
            var email = emailInput?.text ?? "";
            var password = passwordInput?.text ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Log("❌ Email y password requeridos");
                return;
            }

            Log($"📡 POST /api/auth/login");
            Log($"   Email: {email}");

            try
            {
                var success = await _authService.Login(email, password);
                if (success)
                {
                    Log($"✅ Login exitoso");
                    Log($"   UserId: {_authService.CurrentPlayerId}");
                    Log($"   Token: {_authService.CurrentToken?.Substring(0, 20)}...");
                }
                else
                {
                    Log($"❌ Credenciales inválidas");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        private async void HandleRegister()
        {
            var email = emailInput?.text ?? "";
            var username = usernameInput?.text ?? "";
            var password = passwordInput?.text ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Log("❌ Email, username y password requeridos");
                return;
            }

            Log($"📡 POST /api/auth/register");
            Log($"   Email: {email}, Username: {username}");

            try
            {
                var success = await _authService.Register(email, username, password);
                if (success)
                {
                    Log($"✅ Registro exitoso");
                    Log($"   UserId: {_authService.CurrentPlayerId}");
                }
                else
                {
                    Log($"❌ Error registrando");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        private void HandleLogout()
        {
            Log("📡 Logout");
            _authService.Logout();
            Log("✅ Logged out");
        }

        // ========== USER ==========

        private async void HandleGetProfile()
        {
            var userId = userIdInput?.text ?? _authService.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                Log("❌ UserId requerido o login primero");
                return;
            }

            Log($"📡 GET /api/users/{userId}/profile");

            try
            {
                // TODO: Implementar endpoint real
                Log("⚠️ Endpoint no implementado aún");
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        // ========== DECKS ==========

        private async void HandleListDecks()
        {
            var userId = userIdInput?.text ?? _authService.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                Log("❌ UserId requerido o login primero");
                return;
            }

            Log($"📡 GET /api/users/{userId}/decks");

            try
            {
                // TODO: Implementar endpoint real
                Log("⚠️ Endpoint no implementado aún");
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        // ========== CARDS ==========

        private async void HandleListCards()
        {
            Log($"📡 GET /api/cards");

            try
            {
                var cards = await _apiClient.FetchAllCards();
                Log($"✅ Obtenidas {cards.Count} cartas");
                foreach (var card in cards.Take(5))
                {
                    Log($"   - {card.cardId}: {card.displayName} ({card.manaCost} mana)");
                }
                if (cards.Count > 5)
                    Log($"   ... y {cards.Count - 5} más");
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        private async void HandleSearchCards()
        {
            var query = emailInput?.text ?? ""; // Reusar field para query
            if (string.IsNullOrWhiteSpace(query))
            {
                Log("❌ Busca requerida (usa email input)");
                return;
            }

            Log($"📡 GET /api/cards/search?q={query}");

            try
            {
                var results = await _apiClient.SearchCards(query);
                Log($"✅ Encontradas {results.Count} cartas");
                foreach (var card in results.Take(5))
                {
                    Log($"   - {card.displayName}");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        // ========== MATCHES ==========

        private async void HandleGetMatchHistory()
        {
            var userId = userIdInput?.text ?? _authService.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                Log("❌ UserId requerido");
                return;
            }

            Log($"📡 GET /api/matches/history/{userId}?page=1&pageSize=10");

            try
            {
                // TODO: Implementar endpoint real
                Log("⚠️ Endpoint no implementado aún");
            }
            catch (Exception ex)
            {
                Log($"❌ Error: {ex.Message}");
            }
        }

        // ========== UTILITIES ==========

        private void ClearOutput()
        {
            _requestHistory.Clear();
            if (outputText != null) outputText.text = "";
        }

        private void CopyOutput()
        {
            if (outputText != null)
            {
                GUIUtility.systemCopyBuffer = outputText.text;
                Log("📋 Copiado al clipboard");
            }
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"[{timestamp}] {message}";

            _requestHistory.Add(line);
            if (_requestHistory.Count > 100)
                _requestHistory.RemoveAt(0);

            if (outputText != null)
            {
                outputText.text = string.Join("\n", _requestHistory);

                // Auto-scroll to bottom
                if (outputScroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    outputScroll.verticalNormalizedPosition = 0;
                }
            }

            Debug.Log(line);
        }

        private void OnDestroy()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(HandleLogin);
            if (registerButton != null) registerButton.onClick.RemoveListener(HandleRegister);
            if (logoutButton != null) logoutButton.onClick.RemoveListener(HandleLogout);
            if (getProfileButton != null) getProfileButton.onClick.RemoveListener(HandleGetProfile);
            if (listDecksButton != null) listDecksButton.onClick.RemoveListener(HandleListDecks);
            if (listCardsButton != null) listCardsButton.onClick.RemoveListener(HandleListCards);
            if (searchCardsButton != null) searchCardsButton.onClick.RemoveListener(HandleSearchCards);
            if (getMatchHistoryButton != null) getMatchHistoryButton.onClick.RemoveListener(HandleGetMatchHistory);
            if (clearOutputButton != null) clearOutputButton.onClick.RemoveListener(ClearOutput);
            if (copyOutputButton != null) copyOutputButton.onClick.RemoveListener(CopyOutput);
        }
    }
}
#endif
