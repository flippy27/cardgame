using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Manages player decks via server API. The server is authoritative for all deck state.
    /// </summary>
    public sealed class DeckManagementService
    {
        private readonly CardGameApiClient _apiClient;
        private readonly AuthService _authService;
        private List<DeckDto> _cachedDecks;
        private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;
        private const int CacheMinutes = 5;

        public const int MinCards = 20;
        public const int MaxCards = 60;
        public const int MaxCopiesPerCard = 3;

        public DeckManagementService(CardGameApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task<List<DeckDto>> GetPlayerDecksAsync(bool forceRefresh = false)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return new List<DeckDto>();
            }

            var now = DateTimeOffset.UtcNow;
            if (!forceRefresh && _cachedDecks != null && (now - _lastFetch).TotalMinutes < CacheMinutes)
            {
                return new List<DeckDto>(_cachedDecks);
            }

            try
            {
                _cachedDecks = await _apiClient.FetchPlayerDecksAsync(_authService.CurrentPlayerId);
                _lastFetch = now;
                Debug.Log($"[Decks] Fetched {_cachedDecks.Count} decks");
                return new List<DeckDto>(_cachedDecks);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] FetchPlayerDecks failed: {ex.Message}");
                return _cachedDecks ?? new List<DeckDto>();
            }
        }

        public async Task<DeckDto> CreateDeckAsync(string displayName, List<string> cardIds)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return null;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Debug.LogError("[Decks] displayName required");
                return null;
            }

            if (!ValidateCardList(cardIds, out var msg))
            {
                Debug.LogError($"[Decks] Invalid deck: {msg}");
                return null;
            }

            try
            {
                // New deck: deckId = null → server assigns one; use upsert endpoint
                var result = await _apiClient.UpsertDeckAsync(_authService.CurrentPlayerId, null, displayName, cardIds);
                InvalidateCache();
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] CreateDeck failed: {ex.Message}");
                throw;
            }
        }

        public async Task<DeckDto> UpdateDeckAsync(string deckId, string displayName, List<string> cardIds)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return null;
            }

            if (string.IsNullOrWhiteSpace(deckId))
            {
                Debug.LogError("[Decks] deckId required");
                return null;
            }

            if (!ValidateCardList(cardIds, out var msg))
            {
                Debug.LogError($"[Decks] Invalid deck: {msg}");
                return null;
            }

            try
            {
                var result = await _apiClient.UpsertDeckAsync(_authService.CurrentPlayerId, deckId, displayName, cardIds);
                InvalidateCache();
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] UpdateDeck failed: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteDeckAsync(string deckId)
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Decks] Not authenticated");
                return false;
            }

            try
            {
                await _apiClient.DeleteDeckAsync(_authService.CurrentPlayerId, deckId);
                InvalidateCache();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Decks] DeleteDeck failed: {ex.Message}");
                return false;
            }
        }

        public void InvalidateCache()
        {
            _cachedDecks = null;
            _lastFetch = DateTimeOffset.MinValue;
        }

        // Alias kept for callers that use the old name
        public void ClearCache() => InvalidateCache();

        /// <summary>Validates card count and max-copies-per-card rules.</summary>
        public bool ValidateCardList(List<string> cardIds, out string errorMessage)
        {
            if (cardIds == null || cardIds.Count < MinCards)
            {
                errorMessage = $"Deck needs at least {MinCards} cards (has {cardIds?.Count ?? 0})";
                return false;
            }
            if (cardIds.Count > MaxCards)
            {
                errorMessage = $"Deck can have at most {MaxCards} cards";
                return false;
            }

            var counts = new Dictionary<string, int>();
            foreach (var id in cardIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                counts.TryGetValue(id, out var n);
                counts[id] = n + 1;
                if (counts[id] > MaxCopiesPerCard)
                {
                    errorMessage = $"Card '{id}' exceeds max {MaxCopiesPerCard} copies";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
