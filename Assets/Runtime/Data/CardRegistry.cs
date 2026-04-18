using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Centralizado registry de todas las CardDefinitions.
    /// Carga las cards una sola vez desde CardCatalog y las cachea por cardId.
    /// Fuente única de verdad para card data.
    /// </summary>
    public static class CardRegistry
    {
        private static Dictionary<string, CardDefinition> _cardCache = new();
        private static CardCatalog _catalog;
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            _cardCache.Clear();

            // Intentar cargar CardCatalog desde Assets
            _catalog = Resources.Load<CardCatalog>("CardCatalog");

            // Si no está en Resources, buscar en Assets directamente (runtime fallback)
            if (_catalog == null)
            {
                // Buscar CardCatalog en todas partes
                var catalogs = Resources.LoadAll<CardCatalog>("");
                if (catalogs.Length > 0)
                {
                    _catalog = catalogs[0];
                }
            }

            if (_catalog == null)
            {
                Debug.LogWarning("[CardRegistry] CardCatalog not found. Create via Tools/Data/Generate Card Catalog");
                _initialized = true;
                return;
            }

            if (_catalog.cards == null || _catalog.cards.Length == 0)
            {
                Debug.LogWarning("[CardRegistry] CardCatalog has no cards assigned");
                _initialized = true;
                return;
            }

            foreach (var card in _catalog.cards)
            {
                if (card != null && !string.IsNullOrEmpty(card.cardId))
                {
                    _cardCache[card.cardId] = card;
                    Debug.Log($"[CardRegistry] Loaded card: {card.cardId}");
                }
            }

            _initialized = true;
            Debug.Log($"[CardRegistry] Initialized with {_cardCache.Count} cards from CardCatalog");
        }

        public static CardDefinition GetCard(string cardId)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            _cardCache.TryGetValue(cardId, out var card);
            return card;
        }

        public static IEnumerable<CardDefinition> GetAllCards()
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _cardCache.Values;
        }

        public static CardCatalog GetCatalog()
        {
            if (_catalog == null)
            {
                Initialize();
            }
            return _catalog;
        }

        public static void Clear()
        {
            _cardCache.Clear();
            _initialized = false;
        }
    }
}
