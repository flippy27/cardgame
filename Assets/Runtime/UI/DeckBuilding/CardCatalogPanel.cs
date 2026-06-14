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
        [SerializeField] private bool deckEditShowsOwnedCardsOnly = true;
        [SerializeField] private bool hideAfterDeckSelection;

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
            DeckBuilderTextScale.Apply(titleText, DeckBuilderTextScale.Role.Header);
            DeckBuilderTextScale.Apply(pageLabel, DeckBuilderTextScale.Role.Label);
            DeckBuilderTextScale.ApplyAutoSize(statusText, DeckBuilderTextScale.Role.Status);

            ApplyKenneySkin();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (clearFiltersButton != null) clearFiltersButton.onClick.AddListener(ClearFilters);
            if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
            if (nextButton != null) nextButton.onClick.AddListener(NextPage);
            if (searchField != null) searchField.onEndEdit.AddListener(OnSearchChanged);
        }

        /// <summary>Temporary Kenney art skin (code-side, no prefab wiring). Null-safe.</summary>
        private void ApplyKenneySkin()
        {
            if (!KenneyUiSkin.Available) return;
            KenneyUiSkin.EnsureWindowBackdrop(this);
            if (catalogContent != null)
            {
                var viewport = catalogContent.parent;
                if (viewport != null)
                {
                    var vimg = viewport.GetComponent<Image>();
                    if (vimg != null) KenneyUiSkin.SkinInsetImage(vimg);
                    else KenneyUiSkin.EnsureInsetBackdrop(viewport);
                    KenneyUiSkin.SkinScrollbarsUnder(viewport);
                }
            }
            if (searchField != null) KenneyUiSkin.SkinInputImage(searchField.GetComponent<Image>());
            KenneyUiSkin.SkinButtonWithLabel(closeButton, KenneyUiSkin.ButtonStyle.Icon, "X");
            KenneyUiSkin.SkinButtonWithLabel(clearFiltersButton, KenneyUiSkin.ButtonStyle.Nav, "Clear");
            KenneyUiSkin.SkinButton(prevButton, KenneyUiSkin.ButtonStyle.Icon);
            KenneyUiSkin.SkinButton(nextButton, KenneyUiSkin.ButtonStyle.Icon);
            KenneyUiSkin.ApplyFontUnder(this);
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
            _filtered = new List<ServerCardDefinition>();
            foreach (var card in _allCards)
            {
                if (card == null)
                {
                    continue;
                }

                var cardId = card.cardId ?? string.Empty;
                if (_deckEditMode && deckEditShowsOwnedCardsOnly &&
                    (string.IsNullOrWhiteSpace(cardId) || !_ownedCounts.TryGetValue(cardId, out var owned) || owned <= 0))
                {
                    continue;
                }

                var searchableName = card.displayName ?? card.name ?? card.cardId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_searchText) &&
                    searchableName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _filtered.Add(card);
            }

            _currentPage = 0;
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (catalogContent == null || catalogItemPrefab == null) return;

            // Sane grid so catalog cells wrap into columns at a readable size. Idempotent.
            KenneyUiSkin.EnsureGrid(catalogContent, cellSize: new Vector2(240f, 320f), spacing: new Vector2(16f, 16f), padding: 14);

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
            if (texts.Length > 0)
            {
                texts[0].text = card.displayName ?? card.name ?? card.cardId;
                DeckBuilderTextScale.ApplyAutoSize(texts[0], DeckBuilderTextScale.Role.CardName);
            }
            if (texts.Length > 1)
            {
                texts[1].text = $"ATK:{card.attack} HP:{card.health} Mana:{card.manaCost}";
                DeckBuilderTextScale.Apply(texts[1], DeckBuilderTextScale.Role.Label);
            }
            if (texts.Length > 2)
            {
                texts[2].text = owned > 0 ? $"Owned: {owned}" : "Not owned";
                DeckBuilderTextScale.Apply(texts[2], DeckBuilderTextScale.Role.Label);
            }

            // Visuals via CardSurfaceVisualRenderer if present
            var renderer = go.GetComponent<CardSurfaceVisualRenderer>()
                          ?? go.GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            if (renderer != null)
                renderer.ApplyCard(card.cardId, "collection");

            var buttons = go.GetComponentsInChildren<Button>(true);
            if (KenneyUiSkin.Available && buttons.Length > 0)
                KenneyUiSkin.SkinButton(buttons[0], KenneyUiSkin.ButtonStyle.Primary);
            if (_deckEditMode && buttons.Length > 0)
            {
                var capturedCard = card;
                buttons[0].gameObject.SetActive(true);
                buttons[0].onClick.AddListener(() =>
                {
                    OnCardSelectedForDeck?.Invoke(capturedCard);
                    if (hideAfterDeckSelection)
                    {
                        Hide();
                    }
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
