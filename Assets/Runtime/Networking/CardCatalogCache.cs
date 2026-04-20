using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Carga y cachea catálogo de cartas desde API.
    /// Proporciona acceso local sin llamadas repetidas a red.
    /// </summary>
    public sealed class CardCatalogCache
    {
        private readonly CardGameApiClient _apiClient;
        private Dictionary<string, ServerCardDefinition> _cache;
        private bool _isLoading;
        private bool _isLoaded;
        private Exception _loadError;

        public bool IsLoaded => _isLoaded;
        public bool IsLoading => _isLoading;
        public Exception LoadError => _loadError;

        public CardCatalogCache(CardGameApiClient apiClient = null)
        {
            _apiClient = apiClient ?? new CardGameApiClient();
            _cache = new Dictionary<string, ServerCardDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Carga catálogo desde API (si no está cacheado).
        /// </summary>
        public async Task LoadCatalog()
        {
            if (_isLoaded || _isLoading)
                return;

            _isLoading = true;
            _loadError = null;

            try
            {
                var cards = await _apiClient.FetchAllCards();

                // Filter out cards with null/empty cardId
                var validCards = cards.Where(c => !string.IsNullOrWhiteSpace(c.cardId)).ToList();
                if (validCards.Count < cards.Count)
                {
                    Debug.LogWarning($"[Catalog] Filtered {cards.Count - validCards.Count} cards with null/empty cardId");
                }

                _cache = validCards.ToDictionary(c => c.cardId, c => c, StringComparer.OrdinalIgnoreCase);
                _isLoaded = true;
                Debug.Log($"[Catalog] Loaded {_cache.Count} valid cards");
            }
            catch (Exception ex)
            {
                _loadError = ex;
                Debug.LogError($"Failed to load card catalog: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Obtiene una carta del cache.
        /// Retorna null si no existe o no está cargado.
        /// </summary>
        public ServerCardDefinition GetCard(string cardId)
        {
            if (!_isLoaded)
            {
                Debug.LogWarning("Catalog not loaded. Call LoadCatalog() first.");
                return null;
            }

            _cache.TryGetValue(cardId, out var card);
            return card;
        }

        /// <summary>
        /// Obtiene todas las cartas cacheadas.
        /// Retorna lista vacía si no está cargado.
        /// </summary>
        public IReadOnlyDictionary<string, ServerCardDefinition> GetAll()
        {
            if (!_isLoaded)
            {
                Debug.LogWarning("Catalog not loaded. Call LoadCatalog() first.");
                return new Dictionary<string, ServerCardDefinition>();
            }

            return _cache;
        }

        /// <summary>
        /// Valida una lista de card IDs contra el catálogo.
        /// </summary>
        public DeckValidationResult ValidateDeck(IEnumerable<string> cardIds)
        {
            if (!_isLoaded)
            {
                return new DeckValidationResult
                {
                    IsValid = false
                };
            }

            var ids = cardIds.ToList();
            var unknownCards = ids.Where(id => !_cache.ContainsKey(id)).ToList();

            if (unknownCards.Any())
            {
                var result = new DeckValidationResult { IsValid = false };
                foreach (var unknownId in unknownCards)
                {
                    result.AddError($"Unknown card: {unknownId}");
                }
                return result;
            }

            return DeckValidator.ValidateCardIds(ids);
        }

        /// <summary>
        /// Limpia el cache (para reload forzado o logout).
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _isLoaded = false;
            _isLoading = false;
            _loadError = null;
        }

        /// <summary>
        /// Estadísticas del catálogo.
        /// </summary>
        public (int total, int withAbilities) GetStats()
        {
            if (!_isLoaded)
                return (0, 0);

            return (
                _cache.Count,
                _cache.Values.Count(c => c.abilities != null && c.abilities.Length > 0)
            );
        }
    }
}
