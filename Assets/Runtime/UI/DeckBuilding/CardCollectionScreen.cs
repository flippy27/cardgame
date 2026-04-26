using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Main controller for the DeckBuildingScene.
    /// Displays the player's card album (grouped by card type) with filters, pagination,
    /// a crafting panel, and a card detail / upgrade panel.
    ///
    /// Data flow on open:
    ///   1. PlayerCardCollectionService.GetSummaryAsync()  → fills album grid
    ///   2. InventoryService.GetCardDustAsync()            → fills dust badge
    ///
    /// Scene hierarchy (DeckBuildingRoot — attach this component here):
    ///
    ///   DeckBuildingRoot
    ///   ├── Header
    ///   │   ├── TitleText           (Text) "My Collection"
    ///   │   ├── DustBadge
    ///   │   │   ├── DustIcon        (Image)
    ///   │   │   └── DustAmountText  (Text)          → dustAmountText
    ///   │   └── BackButton          (Button)          → backButton
    ///   ├── FilterBar
    ///   │   ├── SearchField         (InputField)      → searchField
    ///   │   ├── RarityDropdown      (Dropdown)        → rarityDropdown
    ///   │   │     "All Rarities" | "Common" | "Rare" | "Epic" | "Legendary"
    ///   │   ├── FactionDropdown     (Dropdown)        → factionDropdown
    ///   │   │     "All Factions" | "Ember" | "Tidal" | "Grove" | "Alloy" | "Void"
    ///   │   └── ClearFiltersButton  (Button)          → clearFiltersButton
    ///   ├── CollectionScrollView
    ///   │   └── Viewport → Content  (GridLayoutGroup + ContentSizeFitter)
    ///   │                                             → cardGridContent
    ///   ├── Pagination
    ///   │   ├── PrevButton          (Button)          → prevPageButton
    ///   │   ├── PageLabel           (Text) "1 / 4"   → pageLabel
    ///   │   └── NextButton          (Button)          → nextPageButton
    ///   ├── ActionBar
    ///   │   └── CraftCardsButton    (Button)          → craftCardsButton
    ///   ├── StatusText              (Text)            → statusText
    ///   ├── LoadingOverlay          (GameObject)      → loadingOverlay
    ///   ├── CraftingPanel           (CraftingPanel)   → craftingPanel  [starts inactive]
    ///   └── CardDetailPanel         (CardDetailPanel) → cardDetailPanel [starts inactive]
    /// </summary>
    public sealed class CardCollectionScreen : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI dustAmountText;
        [SerializeField] private Button backButton;

        [Header("Filters")]
        [SerializeField] private TMP_InputField  searchField;
        [SerializeField] private TMP_Dropdown  rarityDropdown;
        [SerializeField] private TMP_Dropdown factionDropdown;
        [SerializeField] private Button clearFiltersButton;

        [Header("Grid")]
        [SerializeField] private Transform cardGridContent;
        [SerializeField] private GameObject cardItemPrefab;
        [SerializeField] private int pageSize = 12;

        [Header("Pagination")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TextMeshProUGUI pageLabel;

        [Header("Actions")]
        [SerializeField] private Button craftCardsButton;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        [Header("Panels")]
        [SerializeField] private CraftingPanel craftingPanel;
        [SerializeField] private CardDetailPanel cardDetailPanel;

        // ---- Services ----
        private PlayerCardCollectionService _collectionService;
        private InventoryService _inventoryService;

        // ---- State ----
        private List<PlayerCardsApiClient.PlayerCardSummaryEntryDto> _allEntries = new();
        private List<PlayerCardsApiClient.PlayerCardSummaryEntryDto> _filteredEntries = new();
        private readonly CardCollectionFilter _filter = new();
        private int _currentPage;

        // ---- Lifecycle ----

        private void Start()
        {
            WireButtons();
            PopulateDropdowns();
            SubscribePanelEvents();
            LoadDataAsync();
        }

        private void OnDestroy()
        {
            if (craftingPanel != null) craftingPanel.OnCraftSuccess -= OnInventoryOrCollectionChanged;
            if (cardDetailPanel != null) cardDetailPanel.OnUpgradeSuccess -= OnInventoryOrCollectionChanged;
        }

        private void WireButtons()
        {
            if (backButton != null) backButton.onClick.AddListener(OnBack);
            if (craftCardsButton != null) craftCardsButton.onClick.AddListener(OnCraftClicked);
            if (prevPageButton != null) prevPageButton.onClick.AddListener(OnPrevPage);
            if (nextPageButton != null) nextPageButton.onClick.AddListener(OnNextPage);
            if (clearFiltersButton != null) clearFiltersButton.onClick.AddListener(OnClearFilters);
            if (searchField != null) searchField.onEndEdit.AddListener(OnSearchChanged);
            if (rarityDropdown != null) rarityDropdown.onValueChanged.AddListener(OnRarityChanged);
            if (factionDropdown != null) factionDropdown.onValueChanged.AddListener(OnFactionChanged);
        }

        private void SubscribePanelEvents()
        {
            if (craftingPanel != null)
            {
                craftingPanel.Hide();
                craftingPanel.OnCraftSuccess += OnInventoryOrCollectionChanged;
            }
            if (cardDetailPanel != null)
            {
                cardDetailPanel.Hide();
                cardDetailPanel.OnUpgradeSuccess += OnInventoryOrCollectionChanged;
            }
        }

        // ---- Data Loading ----

        private async void LoadDataAsync()
        {
            SetLoading(true);

            ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);
            ServiceLocator.TryResolve<InventoryService>(out _inventoryService);

            if (_collectionService == null)
            {
                ShowStatus("Collection service unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var summaryTask = _collectionService.GetSummaryAsync();
                var dustTask = _inventoryService != null
                    ? _inventoryService.GetCardDustAsync()
                    : Task.FromResult(0);

                await Task.WhenAll(summaryTask, dustTask);

                var summary = summaryTask.Result;
                _allEntries = summary?.cards != null
                    ? new List<PlayerCardsApiClient.PlayerCardSummaryEntryDto>(summary.cards)
                    : new List<PlayerCardsApiClient.PlayerCardSummaryEntryDto>();

                RefreshDustDisplay(dustTask.Result);
                ApplyFilters();
                ShowStatus(string.Empty);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading collection: {ex.Message}");
                Debug.LogError($"[Collection] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void OnInventoryOrCollectionChanged()
        {
            // Invalidate caches so next load fetches fresh data
            _collectionService?.InvalidateSummaryCache();
            _inventoryService?.InvalidateCache();
            LoadDataAsync();
        }

        // ---- Filters ----

        private void PopulateDropdowns()
        {
            if (rarityDropdown != null)
            {
                rarityDropdown.ClearOptions();
                rarityDropdown.AddOptions(new List<string>
                    { "All Rarities", "Common", "Rare", "Epic", "Legendary" });
            }
            if (factionDropdown != null)
            {
                factionDropdown.ClearOptions();
                factionDropdown.AddOptions(new List<string>
                    { "All Factions", "Ember", "Tidal", "Grove", "Alloy", "Void" });
            }
        }

        private void OnSearchChanged(string value) { _filter.SearchText = value; ApplyFilters(); }
        private void OnRarityChanged(int idx) { _filter.Rarity = idx == 0 ? (CardRarity?)null : (CardRarity)(idx - 1); ApplyFilters(); }
        private void OnFactionChanged(int idx) { _filter.Faction = idx == 0 ? (CardFaction?)null : (CardFaction)(idx - 1); ApplyFilters(); }

        private void OnClearFilters()
        {
            _filter.Clear();
            if (searchField != null) searchField.text = string.Empty;
            if (rarityDropdown != null) rarityDropdown.value = 0;
            if (factionDropdown != null) factionDropdown.value = 0;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _filteredEntries = _filter.Apply(_allEntries);
            _currentPage = 0;
            RebuildGrid();
        }

        // ---- Grid ----

        private void RebuildGrid()
        {
            if (cardGridContent == null || cardItemPrefab == null) return;

            foreach (Transform child in cardGridContent) Destroy(child.gameObject);

            int total = TotalPages;
            _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, total - 1));

            int start = _currentPage * pageSize;
            int end = Mathf.Min(start + pageSize, _filteredEntries.Count);

            for (int i = start; i < end; i++)
            {
                var go = Instantiate(cardItemPrefab, cardGridContent);
                var item = go.GetComponent<CardCollectionItem>();
                item?.Bind(_filteredEntries[i], OnCardSelected);
            }

            UpdatePagination();

            if (_filteredEntries.Count == 0)
                ShowStatus(_filter.IsActive ? "No cards match filters." : "No cards in collection.");
            else
                ShowStatus(string.Empty);
        }

        // ---- Pagination ----

        private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)_filteredEntries.Count / pageSize));

        private void UpdatePagination()
        {
            int total = TotalPages;
            if (pageLabel != null) pageLabel.text = $"{_currentPage + 1} / {total}";
            if (prevPageButton != null) prevPageButton.interactable = _currentPage > 0;
            if (nextPageButton != null) nextPageButton.interactable = _currentPage < total - 1;
        }

        private void OnPrevPage() { if (_currentPage > 0) { _currentPage--; RebuildGrid(); } }
        private void OnNextPage() { if (_currentPage < TotalPages - 1) { _currentPage++; RebuildGrid(); } }

        // ---- Actions ----

        private void OnCraftClicked() => craftingPanel?.Show();

        private void OnCardSelected(PlayerCardsApiClient.PlayerCardSummaryEntryDto entry)
        {
            if (entry.ownedInstances == null || entry.ownedInstances.Length == 0) return;
            // Open detail for the first owned instance.
            // Extension: if ownedCopies > 1, show an instance picker first.
            cardDetailPanel?.Show(entry.ownedInstances[0].id);
        }

        private void OnBack() => SceneBootstrap.LoadScene(SceneBootstrap.MenuSceneName);

        // ---- Helpers ----

        private void RefreshDustDisplay(int amount)
        {
            if (dustAmountText != null) dustAmountText.text = amount.ToString("N0");
        }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
