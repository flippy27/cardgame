using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class CardInventoryService
    {
        private CardInventoryApiClient _apiClient;
        private AuthService _authService;
        private Dictionary<string, CardInventoryApiClient.PlayerCardDto> _cachedCards = new();
        private DateTimeOffset _cacheTime = DateTimeOffset.MinValue;
        private const int CACHE_MINUTES = 30;

        public CardInventoryService(CardInventoryApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? new CardInventoryApiClient();
            _authService = authService;
        }

        public async Task<List<CardInventoryApiClient.PlayerCardDto>> FetchInventoryAsync()
        {
            if (!_authService.IsAuthenticated)
            {
                Debug.LogError("[Inventory] Not authenticated");
                return new List<CardInventoryApiClient.PlayerCardDto>();
            }

            var now = DateTimeOffset.UtcNow;
            if (_cachedCards.Count > 0 && (now - _cacheTime).TotalMinutes < CACHE_MINUTES)
                return new List<CardInventoryApiClient.PlayerCardDto>(_cachedCards.Values);

            try
            {
                var dto = await _apiClient.FetchInventory();
                _cachedCards.Clear();
                if (dto?.items != null)
                {
                    foreach (var card in dto.items)
                        _cachedCards[card.cardId] = card;
                }

                _cacheTime = now;
                Debug.Log($"[Inventory] Loaded {_cachedCards.Count} cards");
                return new List<CardInventoryApiClient.PlayerCardDto>(_cachedCards.Values);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Inventory] Failed to fetch: {ex.Message}");
                return new List<CardInventoryApiClient.PlayerCardDto>();
            }
        }

        public CardInventoryApiClient.PlayerCardDto GetCard(string cardId)
        {
            return _cachedCards.TryGetValue(cardId, out var card) ? card : null;
        }

        public void ClearCache()
        {
            _cachedCards.Clear();
            _cacheTime = DateTimeOffset.MinValue;
        }
    }
}
