using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Shows all cards from GET /api/v1/cards with an "owned" indicator from the player's summary.
    /// Can be opened in two modes:
    ///   - Browse mode: read-only, just look at cards
    ///   - DeckEdit mode: each card has an "Add to Deck" button, fires OnCardSelectedForDeck
    ///
    /// Hierarchy (CardCatalogPanel — starts inactive):
    ///   CardCatalogPanel
    ///   ├── Header
    ///   │   ├── TitleText
    ///   │   └── CloseButton
    ///   ├── FilterBar
    ///   │   ├── SearchField         → searchField
    ///   │   └── ClearFiltersButton  → clearFiltersButton
    ///   ├── CatalogScrollView
    ///   │   └── Viewport → Content  → catalogContent
    ///   │       └── [CatalogCardItem × N] → catalogItemPrefab
    ///   ├── Pagination
    ///   │   ├── PrevButton          → prevButton
    ///   │   ├── PageLabel           → pageLabel
    ///   │   └── NextButton          → nextButton
    ///   ├── StatusText              → statusText
    ///   └── LoadingOverlay
    /// </summary>
    public sealed class CardCatalogPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        [Header("Filters")]
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private Button clearFiltersButton;

        [Header("Grid")]
        [SerializeField] private Transform catalogContent;
        [SerializeField] private GameObject catalogItemPrefab;
        [SerializeField] private int pageSize = 12;

        [Header("Pagination")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI pageLabel;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        private CardGameApiClient _apiClient;
        private PlayerCardCollectionService _collectionService;

        private List<ServerCardDefinition> _allCards = new();
        private List<ServerCardDefinition> _filtered = new();
        private Dictionary<string, int> _ownedCounts = new();
        private string _searchText = string.Empty;
        private int _currentPage;
        private bool _deckEditMode;

        /// <summary>Fired when in DeckEdit mode and user clicks "Add to Deck".</summary>
        public event Action<ServerCardDefinition> OnCardSelectedForDeck;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (clearFiltersButton != null) clearFiltersButton.onClick.AddListener(ClearFilters);
            if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
            if (nextButton != null) nextButton.onClick.AddListener(NextPage);
            if (searchField != null) searchField.onEndEdit.AddListener(OnSearchChanged);
        }

        public void Show()
        {
            _deckEditMode = false;
            if (titleText != null) titleText.text = "Card Catalog";
            gameObject.SetActive(true);
            LoadAsync();
        }

        public void ShowForDeckEdit()
        {
            _deckEditMode = true;
            if (titleText != null) titleText.text = "Add Cards to Deck";
            gameObject.SetActive(true);
            LoadAsync();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private async void LoadAsync()
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ServiceLocator.TryResolve<CardGameApiClient>(out _apiClient);
            ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);

            if (_apiClient == null)
            {
                ShowStatus("API client unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var catalogTask = _apiClient.FetchAllCards();
                var summaryTask = _collectionService != null
                    ? _collectionService.GetSummaryAsync()
                    : Task.FromResult<PlayerCardsApiClient.PlayerCardSummaryDto>(null);

                await Task.WhenAll(catalogTask, summaryTask);

                _allCards = catalogTask.Result ?? new List<ServerCardDefinition>();

                _ownedCounts.Clear();
                var summary = summaryTask.Result;
                if (summary?.cards != null)
                {
                    foreach (var entry in summary.cards)
                    {
                        _ownedCounts[entry.cardId] = entry.ownedCopies;
                    }
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading catalog: {ex.Message}");
                Debug.LogError($"[Catalog] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void OnSearchChanged(string value)
        {
            _searchText = value ?? string.Empty;
            ApplyFilter();
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            if (searchField != null) searchField.text = string.Empty;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _filtered = string.IsNullOrWhiteSpace(_searchText)
                ? new List<ServerCardDefinition>(_allCards)
                : _allCards.FindAll(c =>
                    (c.displayName ?? c.name ?? c.cardId).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            _currentPage = 0;
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (catalogContent == null || catalogItemPrefab == null) return;
            foreach (Transform child in catalogContent) Destroy(child.gameObject);

            int totalPages = TotalPages;
            _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, totalPages - 1));

            int start = _currentPage * pageSize;
            int end = Mathf.Min(start + pageSize, _filtered.Count);

            for (int i = start; i < end; i++)
            {
                SpawnItem(_filtered[i]);
            }

            if (pageLabel != null) pageLabel.text = $"{_currentPage + 1} / {Mathf.Max(1, totalPages)}";
            if (prevButton != null) prevButton.interactable = _currentPage > 0;
            if (nextButton != null) nextButton.interactable = _currentPage < totalPages - 1;

            ShowStatus(_filtered.Count == 0 ? "No cards found." : string.Empty);
        }

        private void SpawnItem(ServerCardDefinition card)
        {
            var go = Instantiate(catalogItemPrefab, catalogContent);

            _ownedCounts.TryGetValue(card.cardId, out var owned);

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0) texts[0].text = card.displayName ?? card.name ?? card.cardId;
            if (texts.Length > 1) texts[1].text = $"ATK:{card.attack} HP:{card.health} Mana:{card.manaCost}";
            if (texts.Length > 2) texts[2].text = owned > 0 ? $"Owned: {owned}" : "Not owned";

            // Visuals via CardSurfaceVisualRenderer if present
            var renderer = go.GetComponent<CardSurfaceVisualRenderer>()
                          ?? go.GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            if (renderer != null)
                renderer.ApplyCard(card.cardId, "collection");

            var buttons = go.GetComponentsInChildren<Button>(true);
            if (_deckEditMode && buttons.Length > 0)
            {
                var capturedCard = card;
                buttons[0].gameObject.SetActive(true);
                buttons[0].onClick.AddListener(() =>
                {
                    OnCardSelectedForDeck?.Invoke(capturedCard);
                    Hide();
                });
            }
            else if (!_deckEditMode && buttons.Length > 0)
            {
                buttons[0].gameObject.SetActive(false);
            }
        }

        private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / pageSize));

        private void PrevPage() { if (_currentPage > 0) { _currentPage--; RebuildGrid(); } }
        private void NextPage() { if (_currentPage < TotalPages - 1) { _currentPage++; RebuildGrid(); } }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
