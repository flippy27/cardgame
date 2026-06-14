using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// The componentized, bottom-tabbed deck-builder. ONE screen with a bottom tab bar
    /// (Decks | Create | Salvage | My Cards); every tab shares the reusable <see cref="CardGridView"/> +
    /// <see cref="CardCellView"/> — the ONLY per-tab difference is what a card click does:
    ///   • Decks   — pick/create a deck, then a collection grid where clicking ADDS to the deck.
    ///   • Create  — a grid of craftable cards; clicking opens <see cref="CardCraftOverviewPanel"/>.
    ///   • Salvage — a multi-select grid; clicking toggles trash selection, then confirm/salvage.
    ///   • My Cards— a grid of OWNED cards (★N level badge); clicking opens <see cref="CardUpgradeView"/>.
    ///
    /// Built entirely in code (no prefab wiring). Attach to the DeckBuildingRoot GameObject (it can
    /// replace <see cref="CardCollectionScreen"/> in the scene), or call <see cref="Create"/>. Uses the
    /// existing services via <see cref="ServiceLocator"/>.
    /// </summary>
    public sealed class DeckBuilderShell : MonoBehaviour
    {
        private enum Tab { Decks = 0, Create = 1, Salvage = 2, MyCards = 3 }

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };
        private static readonly Color[] RarityColors =
        {
            new Color(0.65f, 0.65f, 0.65f), new Color(0.20f, 0.50f, 1.00f),
            new Color(0.60f, 0.10f, 0.90f), new Color(1.00f, 0.78f, 0.10f),
        };

        // ---- Services ----
        private PlayerCardCollectionService _collectionService;
        private InventoryService _inventoryService;
        private CraftingService _craftingService;
        private SalvageApiClient _salvageApi;
        private DeckManagementService _deckService;
        private CardCatalogCache _catalog;
        private ProgressApiClient _progressApi;
        private AuthService _authService;

        // ---- Shell UI ----
        private RectTransform _root;
        private RectTransform _contentArea;     // region above the tab bar where each tab lives
        private DeckBuilderTabBar _tabBar;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _dustText;
        private TextMeshProUGUI _statusText;

        // ---- Player progression HUD (level + XP bar) ----
        private TextMeshProUGUI _levelText;
        private Image _xpBarFill;
        private TextMeshProUGUI _xpText;
        private GameObject _loadingOverlay;
        private Tab _activeTab = Tab.Decks;
        private bool _built;

        // ---- Tab roots ----
        private RectTransform _decksTabRoot;
        private RectTransform _createTabRoot;
        private RectTransform _salvageTabRoot;
        private RectTransform _myCardsTabRoot;

        // ---- Decks tab ----
        private RectTransform _deckListRoot;   // list of decks + "New Deck"
        private RectTransform _deckEditRoot;   // collection grid + current-deck list + save
        private CardGridView _deckCollectionGrid;
        private TextMeshProUGUI _deckEditTitle;
        private TMP_InputField _deckNameInput;
        private Transform _deckContentsList;
        private TextMeshProUGUI _deckCountText;
        private TextMeshProUGUI _deckValidationText;
        private Button _deckSaveButton;
        private DeckDto _editingDeck;
        private bool _isNewDeck;
        private readonly Dictionary<string, int> _deckCounts = new();   // cardId -> copies in deck
        private readonly Dictionary<string, int> _ownedCounts = new();  // cardId -> owned copies
        private readonly Dictionary<string, int> _ownedLevels = new();  // cardId -> highest upgrade level owned
        private readonly Dictionary<string, string> _cardNames = new();

        // ---- Create tab ----
        private CardGridView _createGrid;
        private List<CraftingApiClient.CraftableCardDto> _craftableCards = new();
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _inventory = new();
        private CardCraftOverviewPanel _craftOverview;

        // ---- Salvage tab ----
        private CardGridView _salvageGrid;
        private List<SalvageApiClient.SalvageableCardDto> _salvageCards = new();
        private readonly HashSet<string> _salvageSelected = new();
        private readonly Dictionary<string, int> _salvageQty = new();
        private Button _trashBtn;
        private TextMeshProUGUI _trashCountText;
        private RectTransform _trashCanRect;
        private GameObject _salvageConfirmModal;
        private TextMeshProUGUI _salvageConfirmSummary;
        private bool _salvageBusy;

        // ---- My Cards tab (owned cards → leveling view) ----
        private CardGridView _myCardsGrid;
        // cardId → the owned instance we open the leveling view for (the HIGHEST-level copy), and its level.
        private readonly Dictionary<string, string> _myCardsInstanceId = new();
        private readonly Dictionary<string, int> _myCardsLevel = new();
        private CardUpgradeView _upgradeView;

        // ---- Lifecycle ----

        public static DeckBuilderShell Create(Transform parent)
        {
            var go = new GameObject("DeckBuilderShell", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<DeckBuilderShell>();
        }

        private void Start()
        {
            EnsureBuilt();
            ResolveServices();
            SelectTab(Tab.Decks);
            LoadCommonAsync();
        }

        private void ResolveServices()
        {
            ServiceLocator.TryResolve(out _collectionService);
            ServiceLocator.TryResolve(out _inventoryService);
            ServiceLocator.TryResolve(out _craftingService);
            ServiceLocator.TryResolve(out _deckService);
            ServiceLocator.TryResolve(out _catalog);
            ServiceLocator.TryResolve(out _authService);
            if (!ServiceLocator.TryResolve(out _progressApi) || _progressApi == null)
                _progressApi = new ProgressApiClient();
            if (!ServiceLocator.TryResolve(out _salvageApi) || _salvageApi == null)
                _salvageApi = new SalvageApiClient();
        }

        // ---- Build the shell chrome ----

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _root = transform as RectTransform;
            // Fill the canvas.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var crt = (RectTransform)canvas.rootCanvas.transform;
                _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
                _root.pivot = new Vector2(0.5f, 0.5f);
                _root.anchoredPosition = Vector2.zero;
                _root.sizeDelta = crt.rect.size;
            }
            else
            {
                _root.anchorMin = Vector2.zero;
                _root.anchorMax = Vector2.one;
                _root.offsetMin = Vector2.zero;
                _root.offsetMax = Vector2.zero;
            }

            // Opaque base + brown window backdrop.
            var baseImg = gameObject.AddComponent<Image>();
            baseImg.color = new Color(0.10f, 0.09f, 0.12f, 1f);
            baseImg.raycastTarget = true;
            if (KenneyUiSkin.Available) KenneyUiSkin.EnsureWindowBackdrop(this);

            // Header: title (left), dust (right), back (top-right X).
            _titleText = MakeText(_root, "TitleText", "Deck Builder", TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.SetRect(_titleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(60f, -56f), new Vector2(640f, 90f));
            DeckBuilderTextScale.Apply(_titleText, DeckBuilderTextScale.Role.Header);

            _dustText = MakeText(_root, "DustText", "0", TextAlignmentOptions.MidlineRight);
            KenneyUiSkin.SetRect(_dustText, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-360f, -56f), new Vector2(240f, 70f));
            DeckBuilderTextScale.Apply(_dustText, DeckBuilderTextScale.Role.Label);

            var backBtn = MakeButton(_root, "BackButton", "Back", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(backBtn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -36f), new Vector2(300f, 140f));
            backBtn.onClick.AddListener(OnBack);

            // Player progression HUD (level badge + XP bar) under the title, persistent across tabs.
            BuildProgressHud(_root);

            // Content area: between the header and the tab bar.
            var contentGo = new GameObject("ContentArea", typeof(RectTransform));
            _contentArea = (RectTransform)contentGo.transform;
            _contentArea.SetParent(_root, false);
            _contentArea.anchorMin = new Vector2(0f, 0f);
            _contentArea.anchorMax = new Vector2(1f, 1f);
            _contentArea.pivot = new Vector2(0.5f, 0.5f);
            _contentArea.offsetMin = new Vector2(24f, DeckBuilderTabBar.BarHeight + 70f); // above tab bar + status
            _contentArea.offsetMax = new Vector2(-24f, -192f);                            // below the (taller) header

            // Status line above the tab bar.
            _statusText = MakeText(_root, "StatusText", string.Empty, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_statusText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, DeckBuilderTabBar.BarHeight + 6f), new Vector2(1000f, 56f));
            _statusText.raycastTarget = false;
            DeckBuilderTextScale.ApplyAutoSize(_statusText, DeckBuilderTextScale.Role.Status);

            // Tab roots.
            _decksTabRoot = MakeTabRoot("DecksTab");
            _createTabRoot = MakeTabRoot("CreateTab");
            _salvageTabRoot = MakeTabRoot("SalvageTab");
            _myCardsTabRoot = MakeTabRoot("MyCardsTab");

            BuildDecksTab();
            BuildCreateTab();
            BuildSalvageTab();
            BuildMyCardsTab();

            // Loading overlay (covers everything).
            _loadingOverlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(Image));
            var loRt = (RectTransform)_loadingOverlay.transform;
            loRt.SetParent(_root, false);
            KenneyUiSkin.Fill(loRt);
            _loadingOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var loLabel = MakeText(loRt, "LoadingText", "Loading...", TextAlignmentOptions.Center);
            KenneyUiSkin.Fill(loLabel);
            DeckBuilderTextScale.Apply(loLabel, DeckBuilderTextScale.Role.Header);
            _loadingOverlay.SetActive(false);

            // Bottom tab bar (last so it draws above content; loading overlay set above it intentionally).
            _tabBar = DeckBuilderTabBar.Create(_root, "Decks", "Create", "Salvage", "My Cards");
            _tabBar.OnTabSelected += i => SelectTab((Tab)i);
            _loadingOverlay.transform.SetAsLastSibling();

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        private RectTransform MakeTabRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_contentArea, false);
            KenneyUiSkin.Fill(rt);
            go.SetActive(false);
            return rt;
        }

        private void SelectTab(Tab tab)
        {
            _activeTab = tab;
            if (_decksTabRoot != null) _decksTabRoot.gameObject.SetActive(tab == Tab.Decks);
            if (_createTabRoot != null) _createTabRoot.gameObject.SetActive(tab == Tab.Create);
            if (_salvageTabRoot != null) _salvageTabRoot.gameObject.SetActive(tab == Tab.Salvage);
            if (_myCardsTabRoot != null) _myCardsTabRoot.gameObject.SetActive(tab == Tab.MyCards);
            if (_tabBar != null && _tabBar.SelectedIndex != (int)tab) _tabBar.Select((int)tab, notify: false);

            if (_titleText != null)
                _titleText.text = tab switch
                {
                    Tab.Decks => "Decks",
                    Tab.Create => "Create",
                    Tab.Salvage => "Salvage",
                    _ => "My Cards",
                };

            ShowStatus(string.Empty);

            switch (tab)
            {
                case Tab.Decks: ShowDeckList(); break;
                case Tab.Create: LoadCreateAsync(); break;
                case Tab.Salvage: LoadSalvageAsync(); break;
                case Tab.MyCards: LoadMyCardsAsync(); break;
            }
        }

        private async void LoadCommonAsync()
        {
            ResolveServices();
            RefreshProgressAsync(); // player level + XP bar
            if (_inventoryService != null)
            {
                try { RefreshDust(await _inventoryService.GetCardDustAsync()); }
                catch (Exception ex) { Debug.LogWarning($"[DeckBuilderShell] dust load failed: {ex.Message}"); }
            }
            // Preload the owned-card counts so the deck-edit collection grid is ready.
            await LoadOwnedCountsAsync();
        }

        // ====================================================================
        //  DECKS TAB
        // ====================================================================

        private void BuildDecksTab()
        {
            // --- Deck list sub-view ---
            _deckListRoot = MakeChild(_decksTabRoot, "DeckListView");
            KenneyUiSkin.Fill(_deckListRoot);

            var newDeckBtn = MakeButton(_deckListRoot, "NewDeckButton", "New Deck", KenneyUiSkin.ButtonStyle.Primary);
            KenneyUiSkin.SetRect(newDeckBtn, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(500f, 124f));
            newDeckBtn.onClick.AddListener(() => OpenDeckEditor(null));

            var listScroll = new GameObject("DeckScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var lsRt = (RectTransform)listScroll.transform;
            lsRt.SetParent(_deckListRoot, false);
            lsRt.anchorMin = new Vector2(0f, 0f);
            lsRt.anchorMax = new Vector2(1f, 1f);
            lsRt.offsetMin = new Vector2(8f, 8f);
            lsRt.offsetMax = new Vector2(-8f, -160f);
            var lsImg = listScroll.GetComponent<Image>();
            lsImg.color = new Color(0f, 0f, 0f, 0.18f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(lsImg);
            var lsVp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var lsVpRt = (RectTransform)lsVp.transform;
            lsVpRt.SetParent(lsRt, false);
            KenneyUiSkin.Fill(lsVpRt);
            lsVp.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            lsVp.GetComponent<Mask>().showMaskGraphic = false;
            var lsContent = new GameObject("Content", typeof(RectTransform));
            var lsContentRt = (RectTransform)lsContent.transform;
            lsContentRt.SetParent(lsVpRt, false);
            lsContentRt.anchorMin = new Vector2(0f, 1f);
            lsContentRt.anchorMax = new Vector2(1f, 1f);
            lsContentRt.pivot = new Vector2(0.5f, 1f);
            lsContentRt.offsetMin = Vector2.zero;
            lsContentRt.offsetMax = Vector2.zero;
            var sr = listScroll.GetComponent<ScrollRect>();
            sr.content = lsContentRt; sr.viewport = lsVpRt; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            _deckListContent = lsContentRt;

            // --- Deck editor sub-view (collection grid + current-deck list + save) ---
            _deckEditRoot = MakeChild(_decksTabRoot, "DeckEditView");
            KenneyUiSkin.Fill(_deckEditRoot);
            _deckEditRoot.gameObject.SetActive(false);

            _deckEditTitle = MakeText(_deckEditRoot, "EditTitle", "New Deck", TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.SetRect(_deckEditTitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -8f), new Vector2(420f, 72f));
            DeckBuilderTextScale.Apply(_deckEditTitle, DeckBuilderTextScale.Role.Header);

            // Deck name input (top).
            var nameGo = new GameObject("DeckNameInput", typeof(RectTransform), typeof(Image));
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.SetParent(_deckEditRoot, false);
            KenneyUiSkin.SetRect(nameRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(440f, -8f), new Vector2(560f, 72f));
            var nameImg = nameGo.GetComponent<Image>();
            nameImg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInputImage(nameImg);
            _deckNameInput = nameGo.AddComponent<TMP_InputField>();
            var nameText = MakeText(nameRt, "Text", string.Empty, TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.Fill(nameText, 16f, 16f, 6f, 6f);
            nameText.enableWordWrapping = false;
            var namePh = MakeText(nameRt, "Placeholder", "Deck name...", TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.Fill(namePh, 16f, 16f, 6f, 6f);
            namePh.color = new Color(1f, 1f, 1f, 0.45f);
            namePh.enableWordWrapping = false;
            _deckNameInput.textViewport = nameRt;
            _deckNameInput.textComponent = nameText;
            _deckNameInput.placeholder = namePh;

            var backToList = MakeButton(_deckEditRoot, "BackToList", "‹ Decks", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(backToList, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-220f, -8f), new Vector2(200f, 72f));
            backToList.onClick.AddListener(ShowDeckList);

            _deckSaveButton = MakeButton(_deckEditRoot, "SaveButton", "Save", KenneyUiSkin.ButtonStyle.Primary);
            KenneyUiSkin.SetRect(_deckSaveButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(190f, 72f));
            _deckSaveButton.onClick.AddListener(OnSaveDeckClicked);

            // The collection grid takes the LEFT 2/3; the current-deck contents the RIGHT 1/3.
            var gridHost = MakeChild(_deckEditRoot, "CollectionGridHost");
            gridHost.anchorMin = new Vector2(0f, 0f);
            gridHost.anchorMax = new Vector2(0.64f, 1f);
            gridHost.offsetMin = new Vector2(0f, 0f);
            gridHost.offsetMax = new Vector2(0f, -88f);
            _deckCollectionGrid = CardGridView.Create(gridHost, _catalog);

            // Current-deck contents (right column).
            var deckPanel = new GameObject("DeckContentsPanel", typeof(RectTransform), typeof(Image));
            var dpRt = (RectTransform)deckPanel.transform;
            dpRt.SetParent(_deckEditRoot, false);
            dpRt.anchorMin = new Vector2(0.66f, 0f);
            dpRt.anchorMax = new Vector2(1f, 1f);
            dpRt.offsetMin = new Vector2(0f, 0f);
            dpRt.offsetMax = new Vector2(0f, -88f);
            var dpImg = deckPanel.GetComponent<Image>();
            dpImg.color = new Color(0f, 0f, 0f, 0.22f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(dpImg);

            _deckCountText = MakeText(dpRt, "DeckCount", "0 / 30 cards", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_deckCountText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(-16f, 56f));
            DeckBuilderTextScale.Apply(_deckCountText, DeckBuilderTextScale.Role.Label);

            _deckValidationText = MakeText(dpRt, "DeckValidation", string.Empty, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_deckValidationText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(-16f, 60f));
            DeckBuilderTextScale.ApplyAutoSize(_deckValidationText, DeckBuilderTextScale.Role.Status);

            var dcScroll = new GameObject("DeckContentsScroll", typeof(RectTransform), typeof(ScrollRect));
            var dcsRt = (RectTransform)dcScroll.transform;
            dcsRt.SetParent(dpRt, false);
            dcsRt.anchorMin = new Vector2(0f, 0f);
            dcsRt.anchorMax = new Vector2(1f, 1f);
            dcsRt.offsetMin = new Vector2(8f, 76f);
            dcsRt.offsetMax = new Vector2(-8f, -68f);
            var dcVp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var dcVpRt = (RectTransform)dcVp.transform;
            dcVpRt.SetParent(dcsRt, false);
            KenneyUiSkin.Fill(dcVpRt);
            dcVp.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            dcVp.GetComponent<Mask>().showMaskGraphic = false;
            var dcContent = new GameObject("Content", typeof(RectTransform));
            var dcContentRt = (RectTransform)dcContent.transform;
            dcContentRt.SetParent(dcVpRt, false);
            dcContentRt.anchorMin = new Vector2(0f, 1f);
            dcContentRt.anchorMax = new Vector2(1f, 1f);
            dcContentRt.pivot = new Vector2(0.5f, 1f);
            dcContentRt.offsetMin = Vector2.zero;
            dcContentRt.offsetMax = Vector2.zero;
            var dcSr = dcScroll.GetComponent<ScrollRect>();
            dcSr.content = dcContentRt; dcSr.viewport = dcVpRt; dcSr.horizontal = false; dcSr.vertical = true;
            dcSr.movementType = ScrollRect.MovementType.Clamped;
            _deckContentsList = dcContentRt;
        }

        private RectTransform _deckListContent;

        private void ShowDeckList()
        {
            if (_deckEditRoot != null) _deckEditRoot.gameObject.SetActive(false);
            if (_deckListRoot != null) _deckListRoot.gameObject.SetActive(true);
            LoadDeckListAsync();
        }

        private async void LoadDeckListAsync()
        {
            ResolveServices();
            if (_deckService == null) { ShowStatus("Deck service unavailable."); return; }
            SetLoading(true);
            try
            {
                var decks = await _deckService.GetPlayerDecksAsync();
                RebuildDeckList(decks);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] LoadDeckList: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void RebuildDeckList(List<DeckDto> decks)
        {
            if (_deckListContent == null) return;
            KenneyUiSkin.EnsureVerticalList(_deckListContent, spacing: 12f, padding: 12);
            foreach (Transform child in _deckListContent) Destroy(child.gameObject);

            if (decks == null || decks.Count == 0)
            {
                ShowStatus("No decks yet. Tap New Deck to build one.");
                return;
            }
            ShowStatus(string.Empty);
            foreach (var deck in decks)
            {
                var rowGo = new GameObject("DeckRow", typeof(RectTransform), typeof(Image), typeof(Button));
                var rowRt = (RectTransform)rowGo.transform;
                rowRt.SetParent(_deckListContent, false);
                KenneyUiSkin.EnsureRowHeight(rowRt, 230f);
                var rowImg = rowGo.GetComponent<Image>();
                rowImg.color = new Color(0.18f, 0.19f, 0.24f, 0.9f);
                if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(rowGo.GetComponent<Button>(), KenneyUiSkin.ButtonStyle.Nav);

                var label = MakeText(rowRt, "Name", $"{deck.Name}", TextAlignmentOptions.MidlineLeft);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f); labelRt.anchorMax = new Vector2(0.7f, 1f);
                labelRt.pivot = new Vector2(0f, 0.5f);
                labelRt.offsetMin = new Vector2(24f, 0f); labelRt.offsetMax = new Vector2(0f, 0f);
                DeckBuilderTextScale.ApplyAutoSize(label, DeckBuilderTextScale.Role.CardName);

                var count = MakeText(rowRt, "Count", $"{deck.CardCount} cards", TextAlignmentOptions.MidlineRight);
                var countRt = count.rectTransform;
                countRt.anchorMin = new Vector2(0.7f, 0f); countRt.anchorMax = new Vector2(1f, 1f);
                countRt.pivot = new Vector2(1f, 0.5f);
                countRt.offsetMin = new Vector2(0f, 0f); countRt.offsetMax = new Vector2(-24f, 0f);
                DeckBuilderTextScale.Apply(count, DeckBuilderTextScale.Role.Label);

                var captured = deck;
                rowGo.GetComponent<Button>().onClick.AddListener(() => OpenDeckEditor(captured));
            }
        }

        private async void OpenDeckEditor(DeckDto deck)
        {
            _isNewDeck = deck == null;
            _editingDeck = deck;
            _deckCounts.Clear();

            if (_deckEditTitle != null) _deckEditTitle.text = _isNewDeck ? "New Deck" : "Edit Deck";
            if (_deckNameInput != null) _deckNameInput.SetTextWithoutNotify(deck?.Name ?? string.Empty);

            if (deck?.cardIds != null)
                foreach (var id in deck.cardIds)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    _deckCounts.TryGetValue(id, out var n);
                    _deckCounts[id] = n + 1;
                }

            if (_deckListRoot != null) _deckListRoot.gameObject.SetActive(false);
            if (_deckEditRoot != null) _deckEditRoot.gameObject.SetActive(true);

            await LoadOwnedCountsAsync();
            PopulateDeckCollectionGrid();
            RebuildDeckContents();
        }

        private async Task LoadOwnedCountsAsync()
        {
            ResolveServices();
            if (_collectionService == null) return;
            try
            {
                var summary = await _collectionService.GetSummaryAsync();
                _ownedCounts.Clear();
                _ownedLevels.Clear();
                _cardNames.Clear();
                if (summary?.cards != null)
                    foreach (var e in summary.cards)
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.cardId)) continue;
                        _ownedCounts[e.cardId] = Mathf.Max(0, e.ownedCopies);
                        _cardNames[e.cardId] = e.displayName ?? e.cardId;

                        // Highest upgrade level across the owned copies → the cell's "★N" badge.
                        int maxLevel = 1;
                        if (e.ownedInstances != null)
                            foreach (var inst in e.ownedInstances)
                                maxLevel = Mathf.Max(maxLevel, PlayerCardLevel.Resolve(inst));
                        _ownedLevels[e.cardId] = maxLevel;
                    }
            }
            catch (Exception ex) { Debug.LogWarning($"[DeckBuilderShell] owned counts: {ex.Message}"); }
        }

        // The deck-edit collection grid: every OWNED card; clicking ADDS a copy to the deck.
        private void PopulateDeckCollectionGrid()
        {
            if (_deckCollectionGrid == null) return;
            var entries = new List<CardGridView.CardEntry>();
            foreach (var kvp in _ownedCounts)
            {
                if (kvp.Value <= 0) continue;
                var cardId = kvp.Key;
                int rarity = 0, faction = 0, cardType = 0, manaCost = 0;
                if (_catalog != null && _catalog.TryGetCard(cardId, out var d) && d != null)
                { rarity = d.cardRarity; faction = d.cardFaction; cardType = d.cardType; manaCost = d.manaCost; }

                var opts = CardCellView.CardCellOptions.Plain;
                opts.showCopies = true;
                opts.copies = kvp.Value;
                opts.level = _ownedLevels.TryGetValue(cardId, out var lvl) ? lvl : 1;
                entries.Add(new CardGridView.CardEntry
                {
                    cardId = cardId,
                    displayName = _cardNames.TryGetValue(cardId, out var nm) ? nm : cardId,
                    rarity = rarity,
                    faction = faction,
                    cardType = cardType,
                    manaCost = manaCost,
                    options = opts,
                    onClicked = cell => OnDeckCollectionCardClicked(cell.CardId),
                });
            }
            _deckCollectionGrid.SetEntries(entries, "No owned cards. Craft some first!");
        }

        private void OnDeckCollectionCardClicked(string cardId)
        {
            _deckCounts.TryGetValue(cardId, out var current);
            _ownedCounts.TryGetValue(cardId, out var owned);

            if (owned <= 0) { ShowStatus("You don't own this card."); return; }
            if (current >= owned) { ShowStatus($"Only {owned} owned copies available."); return; }
            if (current >= DeckManagementService.MaxCopiesPerCard)
            { ShowStatus($"Max {DeckManagementService.MaxCopiesPerCard} copies per card."); return; }

            _deckCounts[cardId] = current + 1;
            ShowStatus(string.Empty);
            RebuildDeckContents();
        }

        private void RemoveDeckCard(string cardId)
        {
            if (!_deckCounts.ContainsKey(cardId)) return;
            _deckCounts[cardId]--;
            if (_deckCounts[cardId] <= 0) _deckCounts.Remove(cardId);
            RebuildDeckContents();
        }

        private void RebuildDeckContents()
        {
            if (_deckContentsList == null) return;
            KenneyUiSkin.EnsureVerticalList(_deckContentsList, spacing: 8f, padding: 8);
            foreach (Transform child in _deckContentsList) Destroy(child.gameObject);

            int total = 0;
            foreach (var kvp in _deckCounts)
            {
                total += kvp.Value;
                var rowGo = new GameObject("DeckCardRow", typeof(RectTransform), typeof(Image));
                var rowRt = (RectTransform)rowGo.transform;
                rowRt.SetParent(_deckContentsList, false);
                KenneyUiSkin.EnsureRowHeight(rowRt, 70f);
                rowGo.GetComponent<Image>().color = new Color(0.2f, 0.21f, 0.26f, 0.85f);

                var nameText = MakeText(rowRt, "Name",
                    _cardNames.TryGetValue(kvp.Key, out var nm) ? nm : kvp.Key, TextAlignmentOptions.MidlineLeft);
                var nameRt2 = nameText.rectTransform;
                nameRt2.anchorMin = new Vector2(0f, 0f); nameRt2.anchorMax = new Vector2(0.62f, 1f);
                nameRt2.pivot = new Vector2(0f, 0.5f);
                nameRt2.offsetMin = new Vector2(14f, 0f); nameRt2.offsetMax = new Vector2(0f, 0f);
                nameText.enableWordWrapping = false; nameText.overflowMode = TextOverflowModes.Ellipsis;
                DeckBuilderTextScale.ApplyAutoSize(nameText, DeckBuilderTextScale.Role.CardName);

                var cnt = MakeText(rowRt, "Count", $"×{kvp.Value}", TextAlignmentOptions.Midline);
                var cntRt = cnt.rectTransform;
                cntRt.anchorMin = new Vector2(0.62f, 0f); cntRt.anchorMax = new Vector2(0.82f, 1f);
                cntRt.pivot = new Vector2(0.5f, 0.5f);
                cntRt.offsetMin = Vector2.zero; cntRt.offsetMax = Vector2.zero;
                DeckBuilderTextScale.Apply(cnt, DeckBuilderTextScale.Role.Label);

                var rm = MakeButton(rowRt, "Remove", "−", KenneyUiSkin.ButtonStyle.Icon);
                var rmRt = rm.GetComponent<RectTransform>();
                rmRt.anchorMin = new Vector2(0.84f, 0.1f);
                rmRt.anchorMax = new Vector2(0.98f, 0.9f);
                rmRt.offsetMin = Vector2.zero;
                rmRt.offsetMax = Vector2.zero;
                var captured = kvp.Key;
                rm.onClick.AddListener(() => RemoveDeckCard(captured));
            }

            if (_deckCountText != null)
                _deckCountText.text = $"{total} / {DeckManagementService.MaxCards} cards";

            ValidateDeck(total);
        }

        private void ValidateDeck(int total)
        {
            bool valid = true;
            string msg = string.Empty;

            // Ownership check.
            foreach (var kvp in _deckCounts)
            {
                _ownedCounts.TryGetValue(kvp.Key, out var owned);
                if (kvp.Value > owned)
                {
                    valid = false;
                    var label = _cardNames.TryGetValue(kvp.Key, out var nm) ? nm : kvp.Key;
                    msg = $"Need {kvp.Value} of '{label}', own {owned}.";
                    break;
                }
            }

            if (valid)
            {
                if (total < DeckManagementService.MinCards)
                { valid = false; msg = $"Need {DeckManagementService.MinCards - total} more cards."; }
                else if (total > DeckManagementService.MaxCards)
                { valid = false; msg = $"Remove {total - DeckManagementService.MaxCards} cards."; }
            }

            if (_deckSaveButton != null) _deckSaveButton.interactable = valid;
            if (_deckValidationText != null)
            {
                _deckValidationText.text = msg;
                _deckValidationText.color = valid ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
            }
        }

        private List<string> BuildDeckCardIdList()
        {
            var result = new List<string>();
            foreach (var kvp in _deckCounts)
                for (int i = 0; i < kvp.Value; i++) result.Add(kvp.Key);
            return result;
        }

        private async void OnSaveDeckClicked()
        {
            ResolveServices();
            if (_deckService == null) { ShowStatus("Deck service unavailable."); return; }

            var name = _deckNameInput != null ? _deckNameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) { ShowStatus("Deck name required."); return; }

            var cardList = BuildDeckCardIdList();
            if (!_deckService.ValidateCardList(cardList, out var validMsg)) { ShowStatus(validMsg); return; }

            SetLoading(true);
            ShowStatus(_isNewDeck ? "Creating deck..." : "Saving deck...");
            try
            {
                DeckDto result = _isNewDeck
                    ? await _deckService.CreateDeckAsync(name, cardList)
                    : await _deckService.UpdateDeckAsync(_editingDeck?.deckId ?? _editingDeck?.id, name, cardList);

                if (result != null)
                {
                    ShowStatus("Saved!");
                    _deckService.InvalidateCache();
                    ShowDeckList();
                }
                else ShowStatus("Save failed — server returned no response.");
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] SaveDeck: {ex}");
            }
            finally { SetLoading(false); }
        }

        // ====================================================================
        //  CREATE TAB
        // ====================================================================

        private void BuildCreateTab()
        {
            _createGrid = CardGridView.Create(_createTabRoot, _catalog, columns: 3, rows: 3);
        }

        private async void LoadCreateAsync()
        {
            ResolveServices();
            if (_craftingService == null || _inventoryService == null)
            { ShowStatus("Crafting service unavailable."); return; }

            SetLoading(true);
            try
            {
                var cardsTask = _craftingService.GetCraftableCardsAsync();
                var invTask = _inventoryService.GetInventoryAsync();
                await Task.WhenAll(cardsTask, invTask);
                _craftableCards = cardsTask.Result ?? new List<CraftingApiClient.CraftableCardDto>();
                _inventory = invTask.Result ?? new Dictionary<string, InventoryApiClient.PlayerItemDto>();
                RefreshDustFromInventory();
                PopulateCreateGrid();
                ShowStatus(string.Empty);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading craftable cards: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] LoadCreate: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void PopulateCreateGrid()
        {
            if (_createGrid == null) return;
            var entries = new List<CardGridView.CardEntry>();
            foreach (var card in _craftableCards)
            {
                int rarity = card.cardRarity, faction = 0, cardType = 0, manaCost = 0;
                if (_catalog != null && _catalog.TryGetCard(card.cardId, out var d) && d != null)
                { faction = d.cardFaction; cardType = d.cardType; manaCost = d.manaCost; }
                var captured = card;
                entries.Add(new CardGridView.CardEntry
                {
                    cardId = card.cardId,
                    displayName = card.displayName,
                    rarity = rarity,
                    faction = faction,
                    cardType = cardType,
                    manaCost = manaCost,
                    options = CardCellView.CardCellOptions.Plain,
                    onClicked = _ => OpenCraftOverview(captured.cardId),
                });
            }
            _createGrid.SetEntries(entries, "No craftable cards available.");
        }

        private void OpenCraftOverview(string cardId)
        {
            var card = _craftableCards.Find(c => c != null && c.cardId == cardId);
            if (card == null) return;
            EnsureCraftOverview();
            _craftOverview.Show(card, _inventory);
        }

        private void EnsureCraftOverview()
        {
            if (_craftOverview != null) return;
            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform : transform;
            var go = new GameObject("CardCraftOverviewPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            _craftOverview = go.AddComponent<CardCraftOverviewPanel>();
            _craftOverview.OnCraftConfirmed += OnCraftConfirmed;
            go.SetActive(false);
        }

        private async void OnCraftConfirmed(string cardId)
        {
            ResolveServices();
            if (_craftingService == null) return;
            SetLoading(true);
            _craftOverview?.SetLoading(true);
            ShowStatus("Crafting...");
            try
            {
                var result = await _craftingService.CraftCardAsync(cardId);
                if (result?.success == true)
                {
                    if (result.updatedInventory != null) _inventoryService?.ApplyPartialUpdate(result.updatedInventory);
                    _collectionService?.InvalidateSummaryCache();
                    ShowStatus($"Crafted {result.playerCard?.displayName ?? cardId}!");
                    _inventory = await _inventoryService.GetInventoryAsync();
                    RefreshDustFromInventory();
                    PopulateCreateGrid();
                    if (_craftOverview != null) { _craftOverview.RefreshAfterCraft(_inventory); _craftOverview.Hide(); }
                    // Owned counts changed — refresh for the deck tab.
                    await LoadOwnedCountsAsync();
                }
                else ShowStatus(result?.message ?? "Craft failed.");
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] Craft: {ex}");
            }
            finally { SetLoading(false); _craftOverview?.SetLoading(false); }
        }

        // ====================================================================
        //  SALVAGE TAB
        // ====================================================================

        private void BuildSalvageTab()
        {
            // Grid host leaves room at the bottom-right for the trash can.
            var gridHost = MakeChild(_salvageTabRoot, "SalvageGridHost");
            KenneyUiSkin.Fill(gridHost);
            _salvageGrid = CardGridView.Create(gridHost, _catalog, columns: 3, rows: 3);

            // Trash can (bottom-right, over the grid's pager region).
            var trashGo = new GameObject("TrashCan", typeof(RectTransform), typeof(Image), typeof(Button));
            _trashCanRect = (RectTransform)trashGo.transform;
            _trashCanRect.SetParent(_salvageTabRoot, false);
            _trashCanRect.anchorMin = new Vector2(1f, 0f);
            _trashCanRect.anchorMax = new Vector2(1f, 0f);
            _trashCanRect.pivot = new Vector2(1f, 0f);
            // Sit in the bottom strip's free right corner, INSIDE the pager band (height < the grid's
            // 160px bottom inset) so it never overlaps the card grid. The pager is shifted left to clear it.
            _trashCanRect.anchoredPosition = new Vector2(-8f, 6f);
            _trashCanRect.sizeDelta = new Vector2(176f, 150f);
            trashGo.GetComponent<Image>().color = new Color(0.55f, 0.18f, 0.18f, 0.95f);
            _trashBtn = trashGo.GetComponent<Button>();
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(_trashBtn, KenneyUiSkin.ButtonStyle.Primary);
            _trashBtn.onClick.AddListener(OnTrashClicked);

            var icon = MakeText(_trashCanRect, "TrashIcon", "\U0001F5D1", TextAlignmentOptions.Center); // wastebasket
            KenneyUiSkin.Fill(icon, 0f, 0f, 0f, 30f);
            icon.fontSize = 68f; icon.enableAutoSizing = false; icon.raycastTarget = false;
            _trashCountText = MakeText(_trashCanRect, "TrashCount", "0", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_trashCountText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(140f, 36f));
            _trashCountText.raycastTarget = false;
            DeckBuilderTextScale.Apply(_trashCountText, DeckBuilderTextScale.Role.Label);

            BuildSalvageConfirmModal();
        }

        private void BuildSalvageConfirmModal()
        {
            _salvageConfirmModal = new GameObject("SalvageConfirmModal", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)_salvageConfirmModal.transform;
            rt.SetParent(_root, false);
            KenneyUiSkin.Fill(rt);
            _salvageConfirmModal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dialog.transform;
            drt.SetParent(rt, false);
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(920f, 720f);
            var dimg = dialog.GetComponent<Image>();
            dimg.color = new Color(0.16f, 0.14f, 0.18f, 1f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(dimg);

            var title = MakeText(drt, "Title", "Salvage these cards?", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(-40f, 80f));
            DeckBuilderTextScale.Apply(title, DeckBuilderTextScale.Role.Header);

            _salvageConfirmSummary = MakeText(drt, "Summary", string.Empty, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_salvageConfirmSummary, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 45f), new Vector2(-80f, -290f));
            _salvageConfirmSummary.enableWordWrapping = true;
            _salvageConfirmSummary.richText = true;
            DeckBuilderTextScale.ApplyAutoSize(_salvageConfirmSummary, DeckBuilderTextScale.Role.Status);

            var yes = MakeButton(drt, "Yes", "Salvage", KenneyUiSkin.ButtonStyle.Primary);
            KenneyUiSkin.SetRect(yes, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 40f), new Vector2(390f, 128f));
            yes.onClick.AddListener(OnConfirmSalvage);

            var no = MakeButton(drt, "No", "Cancel", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(no, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 40f), new Vector2(390f, 128f));
            no.onClick.AddListener(() => _salvageConfirmModal.SetActive(false));

            _salvageConfirmModal.SetActive(false);
        }

        private async void LoadSalvageAsync()
        {
            ResolveServices();
            if (_salvageApi == null) { ShowStatus("Salvage service unavailable."); return; }
            SetLoading(true);
            try
            {
                var summary = await _salvageApi.FetchSalvageable();
                _salvageCards = summary?.cards != null
                    ? new List<SalvageApiClient.SalvageableCardDto>(summary.cards)
                    : new List<SalvageApiClient.SalvageableCardDto>();
                _salvageSelected.RemoveWhere(id => !_salvageCards.Exists(c => c.cardId == id));
                PopulateSalvageGrid();
                UpdateTrashCount();
                ShowStatus(string.Empty);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading salvageable cards: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] LoadSalvage: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void PopulateSalvageGrid()
        {
            if (_salvageGrid == null) return;
            var entries = new List<CardGridView.CardEntry>();
            foreach (var card in _salvageCards)
            {
                var captured = card;
                var opts = CardCellView.CardCellOptions.Plain;
                opts.showCopies = true;
                opts.copies = Mathf.Max(1, card.ownedCopies);
                opts.selectable = true;
                opts.selected = _salvageSelected.Contains(card.cardId);
                opts.showStepper = true;
                opts.stepperMax = Mathf.Max(1, card.ownedCopies);
                opts.showLock = card.lockedInDeck;
                opts.dim = card.lockedInDeck;
                int manaCost = 0;
                if (_catalog != null && _catalog.TryGetCard(card.cardId, out var sd) && sd != null)
                    manaCost = sd.manaCost;
                entries.Add(new CardGridView.CardEntry
                {
                    cardId = card.cardId,
                    displayName = card.displayName,
                    rarity = card.cardRarity,
                    faction = card.cardFaction,
                    cardType = card.cardType,
                    manaCost = manaCost,
                    options = opts,
                    onClicked = cell => OnSalvageCardClicked(captured, cell),
                    onQuantityChanged = (cell, q) => OnSalvageQuantityChanged(captured, q),
                });
            }
            _salvageGrid.SetEntries(entries, "No cards to salvage.");
        }

        private void OnSalvageCardClicked(SalvageApiClient.SalvageableCardDto card, CardCellView cell)
        {
            if (_salvageBusy) return;
            if (card.lockedInDeck) { ShowStatus("Locked in a deck — remove it there first."); return; }

            if (_salvageSelected.Contains(card.cardId))
            {
                _salvageSelected.Remove(card.cardId);
                _salvageQty.Remove(card.cardId);
                cell.SetSelected(false);
            }
            else
            {
                _salvageSelected.Add(card.cardId);
                _salvageQty[card.cardId] = Mathf.Clamp(cell.Quantity, 1, Mathf.Max(1, card.ownedCopies));
                cell.SetSelected(true);
            }
            UpdateTrashCount();
        }

        private void OnSalvageQuantityChanged(SalvageApiClient.SalvageableCardDto card, int qty)
        {
            if (!_salvageSelected.Contains(card.cardId)) return;
            _salvageQty[card.cardId] = Mathf.Clamp(qty, 1, Mathf.Max(1, card.ownedCopies));
            UpdateTrashCount();
        }

        private int QtyFor(SalvageApiClient.SalvageableCardDto card)
        {
            int max = Mathf.Max(1, card.ownedCopies);
            int q = _salvageQty.TryGetValue(card.cardId, out var v) ? v : 1;
            return Mathf.Clamp(q, 1, max);
        }

        private int TotalSelectedCopies()
        {
            int total = 0;
            foreach (var id in _salvageSelected)
            {
                var card = _salvageCards.Find(c => c.cardId == id);
                total += card != null ? QtyFor(card) : 1;
            }
            return total;
        }

        private void UpdateTrashCount()
        {
            if (_trashCountText != null) _trashCountText.text = TotalSelectedCopies().ToString();
            if (_trashBtn != null) _trashBtn.interactable = _salvageSelected.Count > 0 && !_salvageBusy;
        }

        private void OnTrashClicked()
        {
            if (_salvageBusy || _salvageSelected.Count == 0) return;
            StartCoroutine(SalvageFlowRoutine());
        }

        // Float the selected cells up then shrink into the trash, then show the confirm dialog.
        private IEnumerator SalvageFlowRoutine()
        {
            _salvageBusy = true;
            UpdateTrashCount();

            var animating = new List<CardCellView>();
            foreach (var id in _salvageSelected)
            {
                var cell = _salvageGrid != null ? _salvageGrid.GetCell(id) : null;
                if (cell != null) animating.Add(cell);
            }
            Vector3 target = _trashCanRect != null ? _trashCanRect.position : transform.position;

            float t = 0f; const float upDur = 0.2f;
            var startPos = animating.Select(c => c.RectTransform.position).ToList();
            while (t < upDur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / upDur);
                for (int i = 0; i < animating.Count; i++)
                {
                    if (animating[i] == null) continue;
                    var p = startPos[i]; p.y += 40f * k;
                    animating[i].RectTransform.position = p;
                }
                yield return null;
            }

            t = 0f; const float dropDur = 0.4f;
            var fromPos = animating.Select(c => c != null ? c.RectTransform.position : target).ToList();
            while (t < dropDur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dropDur));
                for (int i = 0; i < animating.Count; i++)
                {
                    if (animating[i] == null) continue;
                    animating[i].RectTransform.position = Vector3.Lerp(fromPos[i], target, k);
                    animating[i].RectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, k);
                }
                yield return null;
            }
            foreach (var c in animating) if (c != null) c.gameObject.SetActive(false);

            ShowSalvageConfirm();
            _salvageBusy = false;
            UpdateTrashCount();
        }

        private void ShowSalvageConfirm()
        {
            if (_salvageConfirmModal == null) return;
            _salvageConfirmSummary.text = BuildMaterialsSummary();
            _salvageConfirmModal.SetActive(true);
            _salvageConfirmModal.transform.SetAsLastSibling();
        }

        private string BuildMaterialsSummary()
        {
            var totals = new Dictionary<string, (string name, int rarity, int qty)>();
            foreach (var id in _salvageSelected)
            {
                var card = _salvageCards.Find(c => c.cardId == id);
                if (card?.yields == null) continue;
                int copies = QtyFor(card);
                foreach (var y in card.yields)
                {
                    var key = y.itemTypeKey ?? y.itemTypeDisplayName ?? "?";
                    totals.TryGetValue(key, out var cur);
                    var name = cur.name ?? (y.itemTypeDisplayName ?? y.itemTypeKey);
                    totals[key] = (name, y.itemTypeRarity, cur.qty + y.quantity * copies);
                }
            }
            var sb = new StringBuilder();
            sb.Append($"Salvaging <b>{TotalSelectedCopies()}</b> copy(ies) will grant:\n\n");
            if (totals.Count == 0) { sb.Append("No materials."); return sb.ToString(); }
            foreach (var kvp in totals)
            {
                var (name, rarity, qty) = kvp.Value;
                var col = RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];
                sb.Append($"  • <color=#{ColorUtility.ToHtmlStringRGB(col)}><b>{qty}x {name}</b></color>\n");
            }
            return sb.ToString();
        }

        private async void OnConfirmSalvage()
        {
            if (_salvageBusy) return;
            _salvageBusy = true;
            if (_salvageConfirmModal != null) _salvageConfirmModal.SetActive(false);
            SetLoading(true);
            ShowStatus("Salvaging...");
            ResolveServices();

            var toSalvage = new List<(string cardId, int qty)>();
            foreach (var id in _salvageSelected)
            {
                var card = _salvageCards.Find(c => c.cardId == id);
                toSalvage.Add((id, card != null ? QtyFor(card) : 1));
            }

            int ok = 0;
            int blocked = 0;
            InventoryApiClient.PlayerItemDto[] latestInventory = null;

            try
            {
                foreach (var (cardId, qty) in toSalvage)
                {
                    int salvagedForCard = 0;
                    for (int copy = 0; copy < qty; copy++)
                    {
                        SalvageApiClient.SalvageCardResponse res;
                        try { res = await _salvageApi.SalvageCard(cardId); }
                        catch (Exception ex) { Debug.LogError($"[DeckBuilderShell] SalvageCard({cardId}): {ex}"); break; }

                        if (res != null && res.success)
                        {
                            ok++; salvagedForCard++;
                            if (res.updatedInventory != null) latestInventory = res.updatedInventory;
                        }
                        else if (res != null && res.errorCode == "card_in_deck") { blocked++; break; }
                        else break;
                    }
                    if (salvagedForCard >= qty) { _salvageSelected.Remove(cardId); _salvageQty.Remove(cardId); }
                }

                if (latestInventory != null) _inventoryService?.ApplyPartialUpdate(latestInventory);
                _collectionService?.InvalidateSummaryCache();
                _inventoryService?.InvalidateCache();

                // Reload list + owned counts.
                await LoadSalvageReload();
                await LoadOwnedCountsAsync();
                if (_inventoryService != null) RefreshDust(await _inventoryService.GetCardDustAsync());

                var msg = new StringBuilder();
                if (ok > 0) msg.Append($"Salvaged {ok} cop{(ok == 1 ? "y" : "ies")}. ");
                if (blocked > 0) msg.Append($"{blocked} card(s) locked in a deck were skipped.");
                ShowStatus(msg.Length > 0 ? msg.ToString() : "Nothing was salvaged.");
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] Salvage flow: {ex}");
            }
            finally { SetLoading(false); _salvageBusy = false; UpdateTrashCount(); }
        }

        private async Task LoadSalvageReload()
        {
            try
            {
                var summary = await _salvageApi.FetchSalvageable();
                _salvageCards = summary?.cards != null
                    ? new List<SalvageApiClient.SalvageableCardDto>(summary.cards)
                    : new List<SalvageApiClient.SalvageableCardDto>();
                _salvageSelected.RemoveWhere(id => !_salvageCards.Exists(c => c.cardId == id));
                PopulateSalvageGrid();
            }
            catch (Exception ex) { Debug.LogError($"[DeckBuilderShell] salvage reload: {ex}"); }
        }

        // ====================================================================
        //  MY CARDS TAB  (owned cards → leveling view)
        // ====================================================================

        private void BuildMyCardsTab()
        {
            var gridHost = MakeChild(_myCardsTabRoot, "MyCardsGridHost");
            KenneyUiSkin.Fill(gridHost);
            _myCardsGrid = CardGridView.Create(gridHost, _catalog, columns: 3, rows: 3);
        }

        // Loads the owned-card summary and shows a grid of OWNED cards (★N badge). Reuses the same
        // CardGridView + filters as the other tabs; clicking a card opens the LEVELING view (does NOT
        // add to a deck). The player-card INSTANCE id we level comes from the summary's ownedInstances
        // (the highest-level copy) — apply-level needs that instance id, not the cardId.
        private async void LoadMyCardsAsync()
        {
            ResolveServices();
            if (_collectionService == null) { ShowStatus("Collection service unavailable."); return; }
            SetLoading(true);
            try
            {
                var summary = await _collectionService.GetSummaryAsync();
                _myCardsInstanceId.Clear();
                _myCardsLevel.Clear();

                var entries = new List<CardGridView.CardEntry>();
                if (summary?.cards != null)
                {
                    foreach (var e in summary.cards)
                    {
                        if (e == null || string.IsNullOrWhiteSpace(e.cardId)) continue;
                        if (e.ownedCopies <= 0 && (e.ownedInstances == null || e.ownedInstances.Length == 0)) continue;

                        // Pick the highest-level owned instance as the one we level (its ★N is shown).
                        string bestInstanceId = null;
                        int bestLevel = 1;
                        if (e.ownedInstances != null)
                        {
                            foreach (var inst in e.ownedInstances)
                            {
                                if (inst == null || string.IsNullOrWhiteSpace(inst.id)) continue;
                                int lvl = PlayerCardLevel.Resolve(inst);
                                if (bestInstanceId == null || lvl > bestLevel)
                                {
                                    bestInstanceId = inst.id;
                                    bestLevel = lvl;
                                }
                            }
                        }
                        if (bestInstanceId == null) continue; // no instance id → can't open the leveling view

                        _myCardsInstanceId[e.cardId] = bestInstanceId;
                        _myCardsLevel[e.cardId] = bestLevel;

                        int rarity = 0, faction = 0, cardType = 0, manaCost = 0;
                        if (_catalog != null && _catalog.TryGetCard(e.cardId, out var d) && d != null)
                        { rarity = d.cardRarity; faction = d.cardFaction; cardType = d.cardType; manaCost = d.manaCost; }

                        var opts = CardCellView.CardCellOptions.Plain;
                        opts.showCopies = e.ownedCopies > 1;
                        opts.copies = Mathf.Max(1, e.ownedCopies);
                        opts.level = bestLevel; // drives the ★N badge

                        var capturedCardId = e.cardId;
                        entries.Add(new CardGridView.CardEntry
                        {
                            cardId = e.cardId,
                            displayName = e.displayName ?? e.cardId,
                            rarity = rarity,
                            faction = faction,
                            cardType = cardType,
                            manaCost = manaCost,
                            options = opts,
                            onClicked = _ => OpenLevelingView(capturedCardId),
                        });
                    }
                }
                _myCardsGrid.SetEntries(entries, "No owned cards. Craft some first!");
                ShowStatus(string.Empty);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading your cards: {ex.Message}");
                Debug.LogError($"[DeckBuilderShell] LoadMyCards: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void OpenLevelingView(string cardId)
        {
            if (!_myCardsInstanceId.TryGetValue(cardId, out var playerCardId) || string.IsNullOrWhiteSpace(playerCardId))
            { ShowStatus("Can't level this card (no instance id)."); return; }
            EnsureUpgradeView();
            _upgradeView.Show(playerCardId);
        }

        private void EnsureUpgradeView()
        {
            if (_upgradeView != null) return;
            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform : transform;
            var go = new GameObject("CardUpgradeView", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            _upgradeView = go.AddComponent<CardUpgradeView>();
            _upgradeView.OnLeveledUp += OnCardLeveledUp;
            go.SetActive(false);
        }

        // A successful level-up consumed materials and (maybe) changed the card's level. Refresh the
        // dust HUD + caches so the grid's ★N badge and other tabs stay in sync.
        private async void OnCardLeveledUp(string playerCardId, CardUpgradeApiClient.PlayerCardUpgradeStateDto state)
        {
            ResolveServices();
            _collectionService?.InvalidateSummaryCache();
            _inventoryService?.InvalidateCache();
            try
            {
                if (_inventoryService != null) RefreshDust(await _inventoryService.GetCardDustAsync());
            }
            catch (Exception ex) { Debug.LogWarning($"[DeckBuilderShell] dust refresh after level-up: {ex.Message}"); }

            // Refresh owned-level cache (deck tab ★N) and the My Cards grid if it's the active tab.
            await LoadOwnedCountsAsync();
            if (_activeTab == Tab.MyCards) LoadMyCardsAsync();
        }

        // ====================================================================
        //  Helpers
        // ====================================================================

        // Builds the LEVEL badge + XP progress bar group, pinned top-left just under the title. Built in
        // code with the Kenney theme; values are filled by RefreshProgressAsync.
        private void BuildProgressHud(RectTransform root)
        {
            const float barW = 460f, barH = 34f, rowH = 90f;

            var hud = MakeChild(root, "ProgressHud");
            hud.anchorMin = new Vector2(0f, 1f);
            hud.anchorMax = new Vector2(0f, 1f);
            hud.pivot = new Vector2(0f, 1f);
            hud.anchoredPosition = new Vector2(60f, -150f); // below the title row (title at y=-56, h=90)
            hud.sizeDelta = new Vector2(barW + 16f, rowH);

            // "Lv N" badge on the left.
            _levelText = MakeText(hud, "LevelText", "Lv —", TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.SetRect(_levelText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(160f, 44f));
            _levelText.color = new Color(1f, 0.85f, 0.25f); // gold
            DeckBuilderTextScale.Apply(_levelText, DeckBuilderTextScale.Role.Label);

            // XP bar track (dark inset) under the level badge.
            var trackGo = new GameObject("XpBarTrack", typeof(RectTransform), typeof(Image));
            var trackRt = (RectTransform)trackGo.transform;
            trackRt.SetParent(hud, false);
            trackRt.anchorMin = new Vector2(0f, 0f);
            trackRt.anchorMax = new Vector2(0f, 0f);
            trackRt.pivot = new Vector2(0f, 0f);
            trackRt.anchoredPosition = new Vector2(0f, 4f);
            trackRt.sizeDelta = new Vector2(barW, barH);
            var trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0f, 0f, 0f, 0.55f);
            trackImg.raycastTarget = false;
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(trackImg);

            // Fill (left-anchored; width driven by fillAmount via a horizontally-stretched child).
            var fillGo = new GameObject("XpBarFill", typeof(RectTransform), typeof(Image));
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.SetParent(trackRt, false);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            _xpBarFill = fillGo.GetComponent<Image>();
            _xpBarFill.color = new Color(0.30f, 0.78f, 1.00f, 1f); // cyan progress
            _xpBarFill.raycastTarget = false;
            _xpBarFill.type = Image.Type.Filled;
            _xpBarFill.fillMethod = Image.FillMethod.Horizontal;
            _xpBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _xpBarFill.fillAmount = 0f;

            // XP numbers centred over the bar.
            _xpText = MakeText(trackRt, "XpText", string.Empty, TextAlignmentOptions.Center);
            KenneyUiSkin.Fill(_xpText);
            _xpText.raycastTarget = false;
            DeckBuilderTextScale.Apply(_xpText, DeckBuilderTextScale.Role.Status);
        }

        // Pulls the player's level + XP from the server and fills the HUD. Null-safe / non-fatal.
        private async void RefreshProgressAsync()
        {
            ResolveServices();
            if (_progressApi == null || _levelText == null) return;
            var playerId = _authService?.CurrentPlayerId;
            if (string.IsNullOrWhiteSpace(playerId)) return;

            try
            {
                var p = await _progressApi.GetProgress(playerId);
                if (p == null) return;

                if (_levelText != null) _levelText.text = $"Lv {p.level}";
                float pct = p.xpForNextLevel > 0
                    ? Mathf.Clamp01((float)p.xpIntoLevel / p.xpForNextLevel)
                    : 1f; // at the level cap (xpForNextLevel == 0) the bar reads full
                if (_xpBarFill != null) _xpBarFill.fillAmount = pct;
                if (_xpText != null)
                    _xpText.text = p.xpForNextLevel > 0
                        ? $"{p.xpIntoLevel:N0} / {p.xpForNextLevel:N0} XP"
                        : "MAX";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeckBuilderShell] progress load failed: {ex.Message}");
            }
        }

        private void RefreshDust(int amount)
        {
            if (_dustText != null) _dustText.text = $"{amount:N0} ◆";
        }

        private void RefreshDustFromInventory()
        {
            if (_dustText == null) return;
            _inventory.TryGetValue(InventoryService.CardDustKey, out var dust);
            _dustText.text = $"{(dust?.quantity ?? 0):N0} ◆";
        }

        private void OnBack() => SceneBootstrap.LoadScene(SceneBootstrap.MenuSceneName);

        private void ShowStatus(string msg) { if (_statusText != null) _statusText.text = msg; }
        private void SetLoading(bool on) { if (_loadingOverlay != null) _loadingOverlay.SetActive(on); }

        private RectTransform MakeChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.alignment = align;
            t.color = Color.white;
            return t;
        }

        private static Button MakeButton(Transform parent, string name, string label, KenneyUiSkin.ButtonStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.32f, 0.40f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var lt = labelGo.AddComponent<TextMeshProUGUI>();
            labelGo.transform.SetParent(go.transform, false);
            KenneyUiSkin.Fill(lt);
            lt.text = label;
            lt.alignment = TextAlignmentOptions.Center;
            lt.color = Color.white;
            lt.raycastTarget = false;
            DeckBuilderTextScale.ApplyAutoSize(lt, DeckBuilderTextScale.Role.Button);

            var btn = go.GetComponent<Button>();
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButtonWithLabel(btn, style, label);
            return btn;
        }

        private void OnDestroy()
        {
            if (_craftOverview != null) _craftOverview.OnCraftConfirmed -= OnCraftConfirmed;
            if (_upgradeView != null) _upgradeView.OnLeveledUp -= OnCardLeveledUp;
        }
    }
}
