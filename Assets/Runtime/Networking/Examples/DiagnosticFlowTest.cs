using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking.Examples
{
    /// <summary>
    /// Diagnóstico completo del flujo de autenticación e integración.
    /// Prueba cada paso y loguea exactamente dónde falla.
    /// </summary>
    public sealed class DiagnosticFlowTest : MonoBehaviour
    {
        [SerializeField] private string testEmail = "playerone@flippy.com";
        [SerializeField] private string testPassword = "password123";

        private AuthService _authService;

        public async void StartDiagnosticFlow()
        {
            try
            {
                Debug.Log("=== DIAGNOSTIC FLOW START ===");

                // Step 1: Login
                await DiagnoseLoginAsync();

                // Step 2: Get token
                await DiagnoseTokenStorageAsync();

                // Step 3: Get cards (protected endpoint)
                await DiagnoseCardsAsync();

                // Step 4: Get decks (protected endpoint)
                await DiagnoseDecksAsync();

                // Step 5: Get user profile (protected endpoint)
                await DiagnoseProfileAsync();

                Debug.Log("=== DIAGNOSTIC FLOW COMPLETE ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Diagnostic] Fatal error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task DiagnoseLoginAsync()
        {
            Debug.Log("[Diagnostic] === STEP 1: LOGIN ===");

            var baseUrl = ConfigManager.GetApiBaseUrl();
            _authService = new AuthService(baseUrl);

            Debug.Log($"[Diagnostic] API URL: {baseUrl}");
            Debug.Log($"[Diagnostic] Email: {testEmail}");

            try
            {
                bool success = await _authService.Login(testEmail, testPassword);

                if (success)
                {
                    Debug.Log($"✅ [Diagnostic] Login successful");
                    Debug.Log($"   Player ID: {_authService.CurrentPlayerId}");
                    Debug.Log($"   Email: {_authService.CurrentUserEmail}");
                    Debug.Log($"   Token length: {_authService.CurrentToken?.Length ?? 0}");
                }
                else
                {
                    Debug.LogError("❌ [Diagnostic] Login failed (returned false)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [Diagnostic] Login exception: {ex.Message}");
            }

            await Task.Delay(500);
        }

        private async Task DiagnoseTokenStorageAsync()
        {
            Debug.Log("[Diagnostic] === STEP 2: TOKEN STORAGE ===");

            var storedToken = SecureTokenStorage.GetToken();
            var playerId = SecureTokenStorage.GetPlayerId();
            var email = SecureTokenStorage.GetEmail();

            if (string.IsNullOrEmpty(storedToken))
            {
                Debug.LogError("❌ [Diagnostic] Token not stored in SecureTokenStorage");
            }
            else
            {
                Debug.Log($"✅ [Diagnostic] Token stored: {storedToken.Substring(0, Math.Min(20, storedToken.Length))}...");
            }

            Debug.Log($"[Diagnostic] Stored Player ID: {playerId}");
            Debug.Log($"[Diagnostic] Stored Email: {email}");

            await Task.Delay(100);
        }

        private async Task DiagnoseCardsAsync()
        {
            Debug.Log("[Diagnostic] === STEP 3: GET CARDS (Protected) ===");

            try
            {
                var baseUrl = ConfigManager.GetApiBaseUrl();
                var client = new CardApiClient(baseUrl);
                var cards = await client.FetchAllCards();

                if (cards != null && cards.Count > 0)
                {
                    Debug.Log($"✅ [Diagnostic] GET /cards successful: {cards.Count} cards");
                    if (cards.Count > 0)
                    {
                        var firstCard = cards[0];
                        Debug.Log($"   First card: {firstCard.displayName} (cost: {firstCard.manaCost})");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️  [Diagnostic] GET /cards returned empty list");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [Diagnostic] GET /cards failed: {ex.Message}");
            }

            await Task.Delay(500);
        }

        private async Task DiagnoseDecksAsync()
        {
            Debug.Log("[Diagnostic] === STEP 4: GET DECKS (Protected) ===");

            try
            {
                var baseUrl = ConfigManager.GetApiBaseUrl();
                var client = new CardApiClient(baseUrl);
                var decks = await client.FetchPlayerDecks(_authService.CurrentPlayerId);

                if (decks != null && decks.Count > 0)
                {
                    Debug.Log($"✅ [Diagnostic] GET /decks successful: {decks.Count} decks");
                    foreach (var deck in decks)
                    {
                        Debug.Log($"   - {deck.displayName} ({deck.cardIds.Count} cards)");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️  [Diagnostic] GET /decks returned empty list");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [Diagnostic] GET /decks failed: {ex.Message}");
            }

            await Task.Delay(500);
        }

        private async Task DiagnoseProfileAsync()
        {
            Debug.Log("[Diagnostic] === STEP 5: GET PROFILE (Protected) ===");

            try
            {
                var baseUrl = ConfigManager.GetApiBaseUrl();
                var client = new UserApiClient(baseUrl);
                var profile = await client.GetProfile(_authService.CurrentPlayerId);

                if (profile != null)
                {
                    Debug.Log($"✅ [Diagnostic] GET /profile successful");
                    Debug.Log($"   Username: {profile.username}");
                    //Debug.Log($"   Rating: {profile.rating?.ratingValue ?? 1200}");
                }
                else
                {
                    Debug.LogWarning("⚠️  [Diagnostic] GET /profile returned null");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [Diagnostic] GET /profile failed: {ex.Message}");
            }

            await Task.Delay(500);
        }

        public void PrintDiagnosticSummary()
        {
            Debug.Log("=== DIAGNOSTIC SUMMARY ===");
            Debug.Log($"API URL: {ConfigManager.GetApiBaseUrl()}");
            Debug.Log($"Auth Service: {(_authService != null ? "Initialized" : "Not initialized")}");
            Debug.Log($"Current Token: {(_authService?.CurrentToken != null ? "Valid" : "None")}");
            Debug.Log($"Stored Token: {(string.IsNullOrEmpty(SecureTokenStorage.GetToken()) ? "None" : "Valid")}");
            Debug.Log($"Player ID: {_authService?.CurrentPlayerId}");
            Debug.Log($"Email: {_authService?.CurrentUserEmail}");
        }
    }
}
