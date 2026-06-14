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
        [SerializeField] private Button deckManagementButton;
        [SerializeField] private Button cardCatalogButton;
        [SerializeField] private Button salvageButton; // optional: created in code if not wired

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        [Header("Panels")]
        [SerializeField] private CraftingPanel craftingPanel;
        [SerializeField] private CardDetailPanel cardDetailPanel;
        [SerializeField] private DeckListPanel deckListPanel;
        [SerializeField] private CardCatalogPanel cardCatalogPanel;

        // ---- Services ----
        private PlayerCardCollectionService _collectionService;
        private InventoryService _inventoryService;

        // ---- State ----
        private List<PlayerCardsApiClient.PlayerCardSummaryEntryDto> _allEntries = new();
        private List<PlayerCardsApiClient.PlayerCardSummaryEntryDto> _filteredEntries = new();
        private readonly CardCollectionFilter _filter = new();
        private int _currentPage;

        // Salvage screen — created in code (full-screen, no prefab), mirroring how craftingPanel is shown.
        private SalvageScreen _salvageScreen;

        // ---- Lifecycle ----

        // NEW: by default this legacy screen now bootstraps the componentized, bottom-tabbed
        // DeckBuilderShell (Decks | Create | Salvage) instead of the old single-album layout. The
        // shell is built entirely in code and reuses the same services, so no scene rewiring is
        // needed. Set this to false (in the Inspector) to fall back to the original screen.
        [Header("Shell")]
        [SerializeField] private bool useTabbedShell = true;

        private DeckBuilderShell _shell;

        private void Start()
        {
            if (useTabbedShell)
            {
                BootstrapTabbedShell();
                return;
            }

            ApplyTextScale();
            ApplyKenneySkin();
            WireButtons();
            PopulateDropdowns();
            SubscribePanelEvents();
            LoadDataAsync();
        }

        // Spawns the new tabbed shell as a child of the root canvas and hides this screen's own
        // legacy chrome (the shell draws its own full-screen UI on top). No prefab wiring.
        private void BootstrapTabbedShell()
        {
            // Hide every legacy visual under this root so the old album doesn't show behind the shell.
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                if (g != null) g.enabled = false;

            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform : transform;
            _shell = DeckBuilderShell.Create(parent);
        }

        private void ApplyTextScale()
        {
            DeckBuilderTextScale.Apply(dustAmountText, DeckBuilderTextScale.Role.Label);
            DeckBuilderTextScale.Apply(pageLabel, DeckBuilderTextScale.Role.Label);
            DeckBuilderTextScale.ApplyAutoSize(statusText, DeckBuilderTextScale.Role.Status);
        }

        /// <summary>
        /// Applies the temporary Kenney art skin in code (no prefab wiring). Null-safe:
        /// if the sprites aren't present under Resources this is a no-op.
        /// </summary>
        private void ApplyKenneySkin()
        {
            if (!KenneyUiSkin.Available) return;

            // Framed brown window behind the whole screen. The root has no Image of its own,
            // so this creates a stretched backdrop child behind the existing layout (which is
            // why the old SkinPanelWindow(this) silently did nothing — there was no target).
            KenneyUiSkin.EnsureWindowBackdrop(this);

            // Scroll-view inset background (beige) — skin both the Viewport and the ScrollView
            // behind it (whichever carries the grey image), so the card list reads as a framed
            // beige panel sitting on the brown window. Falls back to a backdrop child if neither
            // has an Image.
            var viewport = cardGridContent != null ? cardGridContent.parent : null;
            if (viewport != null)
            {
                var vimg = viewport.GetComponent<Image>();
                if (vimg != null) KenneyUiSkin.SkinInsetImage(vimg);
                var scroll = viewport.parent;
                var simg = scroll != null ? scroll.GetComponent<Image>() : null;
                if (simg != null) KenneyUiSkin.SkinInsetImage(simg);
                if (vimg == null && simg == null) KenneyUiSkin.EnsureInsetBackdrop(viewport);
                KenneyUiSkin.SkinScrollbarsUnder(scroll != null ? scroll : viewport);
            }

            // Filters: search input + rarity/faction dropdowns.
            if (searchField != null) KenneyUiSkin.SkinInputImage(searchField.GetComponent<Image>());
            KenneyUiSkin.SkinDropdown(rarityDropdown);
            KenneyUiSkin.SkinDropdown(factionDropdown);

            // Buttons. Give captions to the ones that otherwise render their GameObject name.
            KenneyUiSkin.SkinButtonWithLabel(backButton, KenneyUiSkin.ButtonStyle.Nav, "Back");
            KenneyUiSkin.SkinButton(deckManagementButton, KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SkinButton(cardCatalogButton, KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SkinButtonWithLabel(clearFiltersButton, KenneyUiSkin.ButtonStyle.Nav, "Clear");
            // Page arrows are small square icon buttons.
            KenneyUiSkin.SkinButton(prevPageButton, KenneyUiSkin.ButtonStyle.Icon);
            KenneyUiSkin.SkinButton(nextPageButton, KenneyUiSkin.ButtonStyle.Icon);
            // Primary action.
            KenneyUiSkin.SkinButtonWithLabel(craftCardsButton, KenneyUiSkin.ButtonStyle.Primary, "Craft");

            // Unify typography on the Kenney theme font (no-op if the TTF/font asset is absent).
            KenneyUiSkin.ApplyFontUnder(this);
        }

        private void OnDestroy()
        {
            if (craftingPanel != null) craftingPanel.OnCraftSuccess -= OnInventoryOrCollectionChanged;
            if (cardDetailPanel != null) cardDetailPanel.OnUpgradeSuccess -= OnInventoryOrCollectionChanged;
            if (_salvageScreen != null)
            {
                _salvageScreen.OnSalvaged -= OnInventoryOrCollectionChanged;
                _salvageScreen.OnOpenDeckRequested -= OnSalvageOpenDeck;
            }
        }

        private void WireButtons()
        {
            if (backButton != null) backButton.onClick.AddListener(OnBack);
            if (craftCardsButton != null) craftCardsButton.onClick.AddListener(OnCraftClicked);
            if (deckManagementButton != null) deckManagementButton.onClick.AddListener(OnDecksClicked);
            if (cardCatalogButton != null) cardCatalogButton.onClick.AddListener(OnCatalogClicked);
            EnsureSalvageButton();
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
            if (deckListPanel != null)
            {
                deckListPanel.Hide();
            }
            if (cardCatalogPanel != null)
            {
                cardCatalogPanel.Hide();
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

            // Guarantee a sane grid (cell size / spacing / padding) so cells render at a
            // readable size and wrap into columns instead of overlapping. Idempotent.
            // OVERHAUL: compact vertical list of rows (was a grid). Drop the scene's GridLayoutGroup so
            // the VerticalLayoutGroup can drive the rows.
            var grid = cardGridContent.GetComponent<GridLayoutGroup>();
            if (grid != null) Destroy(grid);
            KenneyUiSkin.EnsureVerticalList(cardGridContent, spacing: 10f, padding: 12);

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
        private void OnDecksClicked() => deckListPanel?.Show();
        private void OnCatalogClicked() => cardCatalogPanel?.Show();

        // ---- Salvage ----

        // Salvage is a full-screen inverse-crafting workshop created in code (no prefab wiring),
        // mirroring how craftingPanel is shown. The nav button is created in code if the scene
        // didn't wire one (so no manual editor step is required).
        private void EnsureSalvageButton()
        {
            if (salvageButton == null)
            {
                // Place the button just under the existing Craft button (or top-right) so it sits in the
                // action bar without needing layout edits.
                // Bottom-LEFT corner of the screen so it never overlaps the Craft button (which lives
                // bottom-centre/right). Matches the Craft button's size when available.
                var craftRt = craftCardsButton != null ? (RectTransform)craftCardsButton.transform : null;
                var size = (craftRt != null && craftRt.sizeDelta != Vector2.zero) ? craftRt.sizeDelta : new Vector2(240f, 84f);
                var parent = craftRt != null && craftRt.parent != null ? craftRt.parent : transform;
                var go = new GameObject("SalvageButton", typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = size;
                rt.anchoredPosition = new Vector2(48f, 60f);

                var labelGo = new GameObject("Label", typeof(RectTransform));
                var lt = labelGo.AddComponent<TextMeshProUGUI>();
                labelGo.transform.SetParent(go.transform, false);
                KenneyUiSkin.Fill(lt);
                lt.text = "Salvage";
                lt.alignment = TextAlignmentOptions.Center;
                lt.color = Color.white;

                salvageButton = go.GetComponent<Button>();
            }

            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButtonWithLabel(salvageButton, KenneyUiSkin.ButtonStyle.Primary, "Salvage");
            salvageButton.onClick.AddListener(OnSalvageClicked);
        }

        private void OnSalvageClicked()
        {
            if (_salvageScreen == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                var parent = canvas != null ? canvas.rootCanvas.transform : transform;
                _salvageScreen = SalvageScreen.Create(parent);
                _salvageScreen.OnSalvaged += OnInventoryOrCollectionChanged;
                _salvageScreen.OnOpenDeckRequested += OnSalvageOpenDeck;
            }
            _salvageScreen.Show();
        }

        // Deep-link from a locked card in the Salvage screen → open that deck's editor.
        private void OnSalvageOpenDeck(string deckId)
        {
            _salvageScreen?.Hide();
            if (deckListPanel != null) deckListPanel.OpenDeckById(deckId);
        }

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
