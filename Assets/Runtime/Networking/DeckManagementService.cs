using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Gestiona decks del jugador: obtener, crear, actualizar.
    /// </summary>
    public sealed class DeckManagementService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private Dictionary<string, DeckDto> _playerDecks = new();
        private DateTimeOffset _lastDecksFetch = DateTimeOffset.MinValue;
        private const int CACHE_MINUTES = 30;

        public DeckManagementService(CardGameApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Obtiene los decks del jugador autenticado.
        /// Cachea por 30 minutos.
        /// </summary>
        public async Task<List<DeckDto>> GetPlayerDecksAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return new List<DeckDto>();
            }

            var playerId = _authService.CurrentPlayerId;
            var now = DateTimeOffset.UtcNow;

            // Return cached if fresh
            if (_playerDecks.Count > 0 && (now - _lastDecksFetch).TotalMinutes < CACHE_MINUTES)
            {
                Debug.Log($"[Decks] Returned {_playerDecks.Count} cached decks");
                return new List<DeckDto>(_playerDecks.Values);
            }

            try
            {
                var decks = await _apiClient.FetchPlayerDecksAsync(playerId);
                _playerDecks.Clear();

                foreach (var deck in decks)
                {
                    _playerDecks[deck.deckId] = deck;
                }

                _lastDecksFetch = now;
                Debug.Log($"[Decks] Fetched {_playerDecks.Count} decks from server");
                return new List<DeckDto>(_playerDecks.Values);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] Failed to fetch decks: {ex.Message}");
                return new List<DeckDto>();
            }
        }

        /// <summary>
        /// Obtiene un deck específico por ID.
        /// </summary>
        public DeckDto GetDeck(string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId))
                return null;

            _playerDecks.TryGetValue(deckId, out var deck);
            return deck;
        }

        /// <summary>
        /// Crea un nuevo deck.
        /// </summary>
        public async Task<bool> CreateDeckAsync(string displayName, List<string> cardIds)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Debug.LogError("[Decks] DisplayName cannot be empty");
                return false;
            }

            if (cardIds == null || cardIds.Count == 0)
            {
                Debug.LogError("[Decks] Deck must contain at least one card");
                return false;
            }

            var playerId = _authService.CurrentPlayerId;
            var deckId = $"deck_{Guid.NewGuid().ToString().Substring(0, 8)}";

            try
            {
                var success = await _apiClient.UpsertDeckAsync(playerId, deckId, displayName, cardIds);
                if (success)
                {
                    // Invalidate cache to fetch fresh decks
                    _playerDecks.Clear();
                    _lastDecksFetch = DateTimeOffset.MinValue;
                    Debug.Log($"[Decks] Created deck '{displayName}' ({deckId})");
                }
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] Failed to create deck: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Actualiza un deck existente.
        /// </summary>
        public async Task<bool> UpdateDeckAsync(string deckId, string displayName, List<string> cardIds)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return false;
            }

            if (string.IsNullOrWhiteSpace(deckId) || string.IsNullOrWhiteSpace(displayName))
            {
                Debug.LogError("[Decks] DeckId and DisplayName cannot be empty");
                return false;
            }

            if (cardIds == null || cardIds.Count == 0)
            {
                Debug.LogError("[Decks] Deck must contain at least one card");
                return false;
            }

            var playerId = _authService.CurrentPlayerId;

            try
            {
                var success = await _apiClient.UpsertDeckAsync(playerId, deckId, displayName, cardIds);
                if (success)
                {
                    // Invalidate cache
                    _playerDecks.Clear();
                    _lastDecksFetch = DateTimeOffset.MinValue;
                    Debug.Log($"[Decks] Updated deck '{displayName}'");
                }
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] Failed to update deck: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Limpia la caché (ej: al logout).
        /// </summary>
        public void ClearCache()
        {
            _playerDecks.Clear();
            _lastDecksFetch = DateTimeOffset.MinValue;
            Debug.Log("[Decks] Cache cleared");
        }

        /// <summary>
        /// Valida que un deck tenga al menos el mínimo de cartas requeridas.
        /// </summary>
        public bool IsValidDeck(List<string> cardIds, int minCards = 1, int maxCards = 100)
        {
            if (cardIds == null)
                return false;

            var count = cardIds.Count;
            return count >= minCards && count <= maxCards;
        }
    }
}
