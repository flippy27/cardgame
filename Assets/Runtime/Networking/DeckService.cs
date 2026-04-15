using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Maneja crear, guardar, cargar y eliminar mazos.
    /// Valida mazos contra catálogo de cartas.
    /// </summary>
    public sealed class DeckService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private readonly CardCatalogCache _cardCatalog;
        private Dictionary<string, DeckDto> _decksCache;

        public DeckService(CardGameApiClient apiClient, AuthService authService, CardCatalogCache cardCatalog)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _cardCatalog = cardCatalog ?? throw new ArgumentNullException(nameof(cardCatalog));
            _decksCache = new Dictionary<string, DeckDto>();
        }

        /// <summary>
        /// Carga todos los mazos del jugador.
        /// </summary>
        public async Task<List<DeckDto>> LoadDecks(string playerId = null)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            var id = playerId ?? _authService.CurrentPlayerId;

            try
            {
                var json = await GetAsync($"/api/users/{id}/decks");
                var wrapper = JsonUtility.FromJson<DeckListWrapper>(json);
                var decks = wrapper?.decks?.ToList() ?? new List<DeckDto>();

                // Cache
                foreach (var deck in decks)
                {
                    _decksCache[deck.DeckId] = deck;
                }

                return decks;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load decks: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene un mazo específico.
        /// </summary>
        public async Task<DeckDto> GetDeck(string deckId)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            // Check cache first
            if (_decksCache.TryGetValue(deckId, out var cached))
            {
                return cached;
            }

            try
            {
                var json = await GetAsync($"/api/decks/{deckId}");
                var deck = JsonUtility.FromJson<DeckDto>(json);
                _decksCache[deckId] = deck;
                return deck;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load deck {deckId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Crea un nuevo mazo.
        /// </summary>
        public async Task<DeckDto> CreateDeck(string name, string description, string[] cardIds)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            // Validar mazo
            var validation = _cardCatalog.ValidateDeck(cardIds);
            if (!validation.IsValid)
            {
                throw new ValidationException($"Invalid deck: {string.Join(", ", validation.Errors)}");
            }

            var deck = new DeckDto
            {
                DeckId = Guid.NewGuid().ToString(),
                PlayerId = _authService.CurrentPlayerId,
                Name = name,
                Description = description,
                CardIds = cardIds,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            try
            {
                var json = JsonUtility.ToJson(deck);
                await PostAsync($"/api/decks", json);
                _decksCache[deck.DeckId] = deck;
                return deck;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create deck: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Actualiza un mazo existente.
        /// </summary>
        public async Task<bool> UpdateDeck(string deckId, string name = null, string description = null, string[] cardIds = null)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            var deck = await GetDeck(deckId);
            if (deck == null)
            {
                throw new InvalidGameStateException($"Deck {deckId} not found.");
            }

            // Validar cartas si se proporcionan
            if (cardIds != null)
            {
                var validation = _cardCatalog.ValidateDeck(cardIds);
                if (!validation.IsValid)
                {
                    throw new ValidationException($"Invalid deck: {string.Join(", ", validation.Errors)}");
                }
                deck.CardIds = cardIds;
            }

            if (!string.IsNullOrWhiteSpace(name))
                deck.Name = name;

            if (!string.IsNullOrWhiteSpace(description))
                deck.Description = description;

            deck.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                var json = JsonUtility.ToJson(deck);
                await PatchAsync($"/api/decks/{deckId}", json);
                _decksCache[deckId] = deck;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update deck: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Elimina un mazo.
        /// </summary>
        public async Task<bool> DeleteDeck(string deckId)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidGameStateException("Not authenticated. Login first.");
            }

            try
            {
                await DeleteAsync($"/api/decks/{deckId}");
                _decksCache.Remove(deckId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete deck: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Valida un mazo contra el catálogo.
        /// </summary>
        public DeckValidationResult ValidateDeck(string[] cardIds)
        {
            if (!_cardCatalog.IsLoaded)
            {
                throw new InvalidGameStateException("Card catalog not loaded. Call Bootstrap() first.");
            }

            return _cardCatalog.ValidateDeck(cardIds);
        }

        /// <summary>
        /// Limpia cache de mazos.
        /// </summary>
        public void ClearCache()
        {
            _decksCache.Clear();
        }

        // Helper methods

        private async Task<string> GetAsync(string endpoint)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url);
            request.method = "GET";
            request.timeout = _apiClient.TimeoutSeconds;
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }

            return request.downloadHandler.text;
        }

        private async Task PostAsync(string endpoint, string body)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
            request.timeout = _apiClient.TimeoutSeconds;
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }
        }

        private async Task PatchAsync(string endpoint, string body)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url, "PATCH");
            request.timeout = _apiClient.TimeoutSeconds;
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }
        }

        private async Task DeleteAsync(string endpoint)
        {
            var url = $"{_apiClient.BaseUrl}{endpoint}";
            await _authService.RefreshTokenIfNeeded();

            using var request = new UnityEngine.Networking.UnityWebRequest(url, "DELETE");
            request.timeout = _apiClient.TimeoutSeconds;
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", _authService.GetAuthorizationHeader());

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"Request failed: {request.responseCode} - {request.error}");
            }
        }
    }

    /// <summary>
    /// DTO para mazo.
    /// </summary>
    [System.Serializable]
    public sealed class DeckDto
    {
        public string DeckId;
        public string PlayerId;
        public string Name;
        public string Description;
        public string[] CardIds;
        public long CreatedAt;
        public long UpdatedAt;
        public int WinRate; // percentage
        public int Matches; // total matches played
    }

    /// <summary>
    /// Wrapper para lista de mazos.
    /// </summary>
    [System.Serializable]
    internal sealed class DeckListWrapper
    {
        public DeckDto[] decks;
    }
}
