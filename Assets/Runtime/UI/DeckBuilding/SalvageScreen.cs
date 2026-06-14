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
using Flippy.CardDuelMobile.Battle;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Full-screen SALVAGE workshop (an inverse crafting workshop). The player multi-selects
    /// salvageable cards, drops them into the trash can, confirms the total materials gained, and
    /// the screen salvages each selected card (one server call per card) then refreshes.
    ///
    /// Everything is built in CODE (no prefab wiring) following the same "no prefab wiring" pattern as
    /// <see cref="CraftingPanel"/>: backdrop + header + filter row + paginated grid + trash button +
    /// in-code confirmation modal + the float-up-shrink-into-trash animation coroutine.
    ///
    /// Wiring: <see cref="CardCollectionScreen"/> creates ONE instance (mirroring how craftingPanel is
    /// shown), wires its <see cref="OnSalvaged"/> + <see cref="OnOpenDeckRequested"/> events, and calls
    /// <see cref="Show"/>. The SalvageApiClient is resolved from the <see cref="ServiceLocator"/> (it is
    /// registered in GameBootstrap; a transient client is created as a fallback).
    ///
    /// No serialized fields — call <see cref="Create"/> to instantiate at runtime.
    /// </summary>
    public sealed class SalvageScreen : MonoBehaviour
    {
        private const int PageSize = 9; // 3x3 grid, same density as the crafting workshop.

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };

        // Material/rarity palette — matches CardCollectionItem.RarityColors.
        private static readonly Color[] RarityColors =
        {
            new Color(0.65f, 0.65f, 0.65f), // Common   — grey
            new Color(0.20f, 0.50f, 1.00f), // Rare     — blue
            new Color(0.60f, 0.10f, 0.90f), // Epic     — purple
            new Color(1.00f, 0.78f, 0.10f), // Legendary — gold
        };

        // ---- Services ----
        private SalvageApiClient _salvageApi;
        private InventoryService _inventoryService;
        private PlayerCardCollectionService _collectionService;
        private DeckManagementService _deckService;
        private CardCatalogCache _catalog;

        // ---- Data / state ----
        private List<SalvageApiClient.SalvageableCardDto> _allCards = new();
        private List<SalvageApiClient.SalvageableCardDto> _filtered = new();
        private readonly HashSet<string> _selected = new(); // selected cardIds
        private readonly Dictionary<string, int> _quantities = new(); // cardId -> chosen copies, [1, ownedCopies]
        private readonly Dictionary<string, SalvageCardCell> _cells = new(); // cardId -> visible cell
        private int _page;
        private int? _rarityFilter; // null = all, else 0..3
        private int? _factionFilter; // null = all, else 0..4

        // ---- Built UI refs ----
        private RectTransform _gridContent;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _pageLabel;
        private TextMeshProUGUI _selectionCountText;
        private Button _prevBtn, _nextBtn, _trashBtn;
        private RectTransform _trashCanRect;
        private GameObject _loadingOverlay;
        private GameObject _lockToast;
        private TextMeshProUGUI _lockToastText;
        private Button _lockToastDeckBtn;
        private TextMeshProUGUI _lockToastDeckBtnLabel;
        private GameObject _confirmModal;
        private TextMeshProUGUI _confirmSummary;
        private Button _confirmYesBtn, _confirmNoBtn;
        private bool _built;
        private bool _busy;

        // Pending deep-link target for the lock toast button.
        private string _pendingDeckId;

        /// <summary>Fired after one or more cards were salvaged — the host refreshes its collection/dust.</summary>
        public event Action OnSalvaged;

        /// <summary>Requests the host open the deck editor for the given deckId (deep-link from a locked card).</summary>
        public event Action<string> OnOpenDeckRequested;

        // ---- Factory ----

        /// <summary>Creates a SalvageScreen under the given canvas/root (full-screen, starts hidden).</summary>
        public static SalvageScreen Create(Transform parent)
        {
            var go = new GameObject("SalvageScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<SalvageScreen>();
            screen.gameObject.SetActive(false);
            return screen;
        }

        // ---- Public API ----

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBuilt();
            LoadDataAsync();
        }

        public void Hide()
        {
            HideConfirm();
            HideLockToast();
            gameObject.SetActive(false);
        }

        // ---- Build (code-side UI) ----

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var rt = (RectTransform)transform;
            // Fill the whole canvas.
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Opaque dark base so the screen reads as a separate view even without the Kenney art.
            var baseImg = gameObject.AddComponent<Image>();
            baseImg.color = new Color(0.10f, 0.09f, 0.12f, 1f);
            baseImg.raycastTarget = true; // block taps to the screen behind

            // Brown Kenney window backdrop on top of the base (no-op if assets absent).
            if (KenneyUiSkin.Available) KenneyUiSkin.EnsureWindowBackdrop(this);

            BuildHeader(rt);
            BuildFilterRow(rt);
            BuildGrid(rt);
            BuildPager(rt);
            BuildTrashBar(rt);
            BuildStatus(rt);
            BuildLoadingOverlay(rt);
            BuildLockToast(rt);
            BuildConfirmModal(rt);

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        private void BuildHeader(RectTransform panel)
        {
            var title = MakeText(panel, "TitleText", "Salvage Workshop", TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.SetRect(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(60f, -56f), new Vector2(700f, 90f));
            DeckBuilderTextScale.Apply(title, DeckBuilderTextScale.Role.Header);

            var closeBtn = MakeButton(panel, "CloseButton", "X", KenneyUiSkin.ButtonStyle.Icon);
            KenneyUiSkin.SetRect(closeBtn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), new Vector2(150f, 90f));
            closeBtn.onClick.AddListener(Hide);
        }

        private void BuildFilterRow(RectTransform panel)
        {
            // Rarity filter: cycle button (All -> Common -> Rare -> Epic -> Legendary -> All).
            var rarityBtn = MakeButton(panel, "RarityFilter", "Rarity: All", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(rarityBtn, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(60f, -160f), new Vector2(360f, 64f));
            var rarityLabel = rarityBtn.GetComponentInChildren<TMP_Text>(true);
            rarityBtn.onClick.AddListener(() =>
            {
                _rarityFilter = NextFilter(_rarityFilter, RarityNames.Length);
                if (rarityLabel != null)
                    rarityLabel.text = "Rarity: " + (_rarityFilter == null ? "All" : RarityNames[_rarityFilter.Value]);
                ApplyFilters();
            });

            // Faction filter: cycle button.
            var factionNames = new[] { "Ember", "Tidal", "Grove", "Alloy", "Void" };
            var factionBtn = MakeButton(panel, "FactionFilter", "Faction: All", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(factionBtn, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(440f, -160f), new Vector2(360f, 64f));
            var factionLabel = factionBtn.GetComponentInChildren<TMP_Text>(true);
            factionBtn.onClick.AddListener(() =>
            {
                _factionFilter = NextFilter(_factionFilter, factionNames.Length);
                if (factionLabel != null)
                    factionLabel.text = "Faction: " + (_factionFilter == null ? "All" : factionNames[_factionFilter.Value]);
                ApplyFilters();
            });

            // Clear filters.
            var clearBtn = MakeButton(panel, "ClearFilters", "Clear", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(clearBtn, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(820f, -160f), new Vector2(180f, 64f));
            clearBtn.onClick.AddListener(() =>
            {
                _rarityFilter = null;
                _factionFilter = null;
                if (rarityLabel != null) rarityLabel.text = "Rarity: All";
                if (factionLabel != null) factionLabel.text = "Faction: All";
                ApplyFilters();
            });
        }

        private static int? NextFilter(int? current, int count)
        {
            if (current == null) return 0;
            int next = current.Value + 1;
            return next >= count ? (int?)null : next;
        }

        private void BuildGrid(RectTransform panel)
        {
            // Scroll view (viewport masks the content; content uses a GridLayoutGroup driven below).
            var scrollGo = new GameObject("SalvageScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(panel, false);
            KenneyUiSkin.Fill(scrollRt, left: 24f, right: 24f, top: 250f, bottom: 280f);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.18f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(scrollImg);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.SetParent(scrollRt, false);
            KenneyUiSkin.Fill(viewportRt);
            var vpImg = viewportGo.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            _gridContent = (RectTransform)contentGo.transform;
            _gridContent.SetParent(viewportRt, false);
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.offsetMin = Vector2.zero;
            _gridContent.offsetMax = Vector2.zero;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = _gridContent;
            scroll.viewport = viewportRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private void BuildPager(RectTransform panel)
        {
            var pager = new GameObject("SalvagePager", typeof(RectTransform));
            var prt = (RectTransform)pager.transform;
            prt.SetParent(panel, false);
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 150f);
            prt.sizeDelta = new Vector2(560f, 90f);

            _prevBtn = MakeButton(prt, "PrevBtn", "‹ Prev", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(_prevBtn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(110f, 0f), new Vector2(170f, 76f));
            _prevBtn.onClick.AddListener(() => { if (_page > 0) { _page--; RebuildGrid(); } });

            _nextBtn = MakeButton(prt, "NextBtn", "Next ›", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(_nextBtn, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-110f, 0f), new Vector2(170f, 76f));
            _nextBtn.onClick.AddListener(() => { _page++; RebuildGrid(); });

            _pageLabel = MakeText(prt, "PageLabel", "1 / 1", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_pageLabel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(180f, 70f));
            _pageLabel.raycastTarget = false;
            DeckBuilderTextScale.Apply(_pageLabel, DeckBuilderTextScale.Role.Label);
        }

        private void BuildTrashBar(RectTransform panel)
        {
            // Trash can button pinned bottom-right.
            var trashGo = new GameObject("TrashCan", typeof(RectTransform), typeof(Image), typeof(Button));
            var trashRt = (RectTransform)trashGo.transform;
            trashRt.SetParent(panel, false);
            trashRt.anchorMin = new Vector2(1f, 0f);
            trashRt.anchorMax = new Vector2(1f, 0f);
            trashRt.pivot = new Vector2(1f, 0f);
            trashRt.anchoredPosition = new Vector2(-50f, 140f);
            trashRt.sizeDelta = new Vector2(170f, 170f);
            _trashCanRect = trashRt;

            var trashImg = trashGo.GetComponent<Image>();
            trashImg.color = new Color(0.55f, 0.18f, 0.18f, 0.95f);
            _trashBtn = trashGo.GetComponent<Button>();
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(_trashBtn, KenneyUiSkin.ButtonStyle.Primary);

            var icon = MakeText(trashRt, "TrashIcon", "🗑", TextAlignmentOptions.Center);
            KenneyUiSkin.Fill(icon, 0f, 0f, 0f, 36f);
            icon.fontSize = 60f;
            icon.enableAutoSizing = false;
            icon.raycastTarget = false;

            _selectionCountText = MakeText(trashRt, "SelCount", "0", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_selectionCountText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(160f, 40f));
            _selectionCountText.raycastTarget = false;
            DeckBuilderTextScale.Apply(_selectionCountText, DeckBuilderTextScale.Role.Label);

            _trashBtn.onClick.AddListener(OnTrashClicked);
        }

        private void BuildStatus(RectTransform panel)
        {
            _statusText = MakeText(panel, "StatusText", string.Empty, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_statusText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(900f, 64f));
            _statusText.raycastTarget = false;
            DeckBuilderTextScale.ApplyAutoSize(_statusText, DeckBuilderTextScale.Role.Status);
        }

        private void BuildLoadingOverlay(RectTransform panel)
        {
            _loadingOverlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)_loadingOverlay.transform;
            rt.SetParent(panel, false);
            KenneyUiSkin.Fill(rt);
            _loadingOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var label = MakeText(rt, "LoadingText", "Loading...", TextAlignmentOptions.Center);
            KenneyUiSkin.Fill(label);
            DeckBuilderTextScale.Apply(label, DeckBuilderTextScale.Role.Header);
            _loadingOverlay.SetActive(false);
        }

        private void BuildLockToast(RectTransform panel)
        {
            _lockToast = new GameObject("LockToast", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)_lockToast.transform;
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -260f);
            rt.sizeDelta = new Vector2(820f, 220f);
            var img = _lockToast.GetComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.16f, 0.97f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(img);

            _lockToastText = MakeText(rt, "LockText",
                "This card is locked in a deck.", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_lockToastText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(-40f, 100f));
            _lockToastText.enableWordWrapping = true;
            DeckBuilderTextScale.ApplyAutoSize(_lockToastText, DeckBuilderTextScale.Role.Status);

            _lockToastDeckBtn = MakeButton(rt, "OpenDeckBtn", "Open Deck", KenneyUiSkin.ButtonStyle.Primary);
            KenneyUiSkin.SetRect(_lockToastDeckBtn, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 26f), new Vector2(340f, 70f));
            _lockToastDeckBtnLabel = _lockToastDeckBtn.GetComponentInChildren<TMP_Text>(true) as TextMeshProUGUI;
            _lockToastDeckBtn.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(_pendingDeckId)) OnOpenDeckRequested?.Invoke(_pendingDeckId);
                HideLockToast();
            });

            var dismiss = MakeButton(rt, "DismissBtn", "Dismiss", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(dismiss, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 26f), new Vector2(280f, 70f));
            dismiss.onClick.AddListener(HideLockToast);

            _lockToast.SetActive(false);
        }

        private void BuildConfirmModal(RectTransform panel)
        {
            _confirmModal = new GameObject("ConfirmModal", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)_confirmModal.transform;
            rt.SetParent(panel, false);
            KenneyUiSkin.Fill(rt);
            // Dim backdrop that blocks taps.
            _confirmModal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dialog.transform;
            drt.SetParent(rt, false);
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(920f, 760f);
            var dimg = dialog.GetComponent<Image>();
            dimg.color = new Color(0.16f, 0.14f, 0.18f, 1f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(dimg);

            var title = MakeText(drt, "ConfirmTitle", "Salvage these cards?", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(-40f, 80f));
            DeckBuilderTextScale.Apply(title, DeckBuilderTextScale.Role.Header);

            _confirmSummary = MakeText(drt, "ConfirmSummary", string.Empty, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_confirmSummary, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(-80f, -240f));
            _confirmSummary.enableWordWrapping = true;
            _confirmSummary.richText = true;
            DeckBuilderTextScale.ApplyAutoSize(_confirmSummary, DeckBuilderTextScale.Role.Status);

            _confirmYesBtn = MakeButton(drt, "ConfirmYes", "Salvage", KenneyUiSkin.ButtonStyle.Primary);
            KenneyUiSkin.SetRect(_confirmYesBtn, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 36f), new Vector2(340f, 84f));
            _confirmYesBtn.onClick.AddListener(OnConfirmSalvage);

            _confirmNoBtn = MakeButton(drt, "ConfirmNo", "Cancel", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(_confirmNoBtn, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 36f), new Vector2(340f, 84f));
            _confirmNoBtn.onClick.AddListener(HideConfirm);

            _confirmModal.SetActive(false);
        }

        // ---- Data loading ----

        private async void LoadDataAsync()
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ResolveServices();

            if (_salvageApi == null)
            {
                ShowStatus("Salvage service unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var summary = await _salvageApi.FetchSalvageable();
                _allCards = summary?.cards != null
                    ? new List<SalvageApiClient.SalvageableCardDto>(summary.cards)
                    : new List<SalvageApiClient.SalvageableCardDto>();

                // Drop selections that no longer exist; reconcile remaining quantities.
                _selected.RemoveWhere(id => !_allCards.Exists(c => c.cardId == id));
                ReconcileQuantities();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading salvageable cards: {ex.Message}");
                Debug.LogError($"[SalvageScreen] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void ResolveServices()
        {
            if (_salvageApi == null)
            {
                if (!ServiceLocator.TryResolve<SalvageApiClient>(out _salvageApi) || _salvageApi == null)
                    _salvageApi = new SalvageApiClient(); // fallback: uses ApiConfig.BaseUrl
            }
            if (_inventoryService == null) ServiceLocator.TryResolve<InventoryService>(out _inventoryService);
            if (_collectionService == null) ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);
            if (_deckService == null) ServiceLocator.TryResolve<DeckManagementService>(out _deckService);
            if (_catalog == null) ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
        }

        // ---- Filters ----

        private void ApplyFilters()
        {
            _filtered = _allCards.Where(c =>
                (_rarityFilter == null || c.cardRarity == _rarityFilter.Value) &&
                (_factionFilter == null || c.cardFaction == _factionFilter.Value)).ToList();
            _page = 0;
            RebuildGrid();
        }

        // ---- Grid ----

        private void RebuildGrid()
        {
            if (_gridContent == null) return;

            foreach (Transform child in _gridContent) Destroy(child.gameObject);
            _cells.Clear();

            // 3-column grid sized to fill the viewport (2:3 cells so the card composite fits).
            KenneyUiSkin.EnsureGrid(_gridContent, cellSize: new Vector2(240f, 360f), spacing: new Vector2(16f, 18f), padding: 12);
            var grid = _gridContent.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                Canvas.ForceUpdateCanvases();
                float availW = _gridContent.rect.width;
                if (availW < 50f) availW = Mathf.Max(600f, ((RectTransform)transform).rect.width - 140f);
                float pad = grid.padding.left + grid.padding.right;
                float sp = grid.spacing.x * 2f;
                float cw = Mathf.Max(220f, (availW - pad - sp) / 3f);
                grid.cellSize = new Vector2(cw, cw * 1.5f);
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(_filtered.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            int start = _page * PageSize;
            int end = Mathf.Min(start + PageSize, _filtered.Count);

            for (int i = start; i < end; i++)
            {
                var card = _filtered[i];
                var cell = SalvageCardCell.Create(_gridContent, _catalog);
                cell.Bind(card, _selected.Contains(card.cardId), QuantityFor(card), OnCellTapped, OnCellQuantityChanged);
                _cells[card.cardId] = cell;
            }

            // Apply stat badges only after the grid lays the cells out (rect is 0 at instantiate).
            Canvas.ForceUpdateCanvases();
            foreach (var kvp in _cells) kvp.Value.RefreshBadges();

            if (_pageLabel != null) _pageLabel.text = $"{_page + 1} / {totalPages}";
            if (_prevBtn != null) _prevBtn.interactable = _page > 0;
            if (_nextBtn != null) _nextBtn.interactable = _page < totalPages - 1;

            UpdateSelectionCount();

            if (_filtered.Count == 0)
                ShowStatus(_allCards.Count == 0 ? "No cards to salvage." : "No cards match the filters.");
            else
                ShowStatus(string.Empty);
        }

        // ---- Selection ----

        private void OnCellTapped(SalvageApiClient.SalvageableCardDto card)
        {
            if (_busy) return;

            if (card.lockedInDeck)
            {
                ShowLockToastForCard(card);
                return;
            }

            if (_selected.Contains(card.cardId))
            {
                // Deselecting resets the chosen quantity.
                _selected.Remove(card.cardId);
                _quantities.Remove(card.cardId);
            }
            else
            {
                _selected.Add(card.cardId);
                // Default to 1 copy on (re)select; clamp any stale value to the owned range.
                _quantities[card.cardId] = QuantityFor(card);
            }

            if (_cells.TryGetValue(card.cardId, out var cell))
                cell.SetSelected(_selected.Contains(card.cardId));

            UpdateSelectionCount();
        }

        // The chosen quantity for a card, clamped to [1, ownedCopies]. Defaults to 1.
        private int QuantityFor(SalvageApiClient.SalvageableCardDto card)
        {
            int max = Mathf.Max(1, card.ownedCopies);
            int q = _quantities.TryGetValue(card.cardId, out var v) ? v : 1;
            return Mathf.Clamp(q, 1, max);
        }

        // Drop quantities for unselected/gone cards and re-clamp the rest to the latest ownedCopies.
        private void ReconcileQuantities()
        {
            var keys = new List<string>(_quantities.Keys);
            foreach (var id in keys)
            {
                var card = _allCards.Find(c => c.cardId == id);
                if (card == null || !_selected.Contains(id))
                {
                    _quantities.Remove(id);
                    continue;
                }
                _quantities[id] = Mathf.Clamp(_quantities[id], 1, Mathf.Max(1, card.ownedCopies));
            }
        }

        // Fired by a cell's +/- stepper; record the clamped quantity and refresh the trash count.
        private void OnCellQuantityChanged(SalvageApiClient.SalvageableCardDto card, int quantity)
        {
            if (!_selected.Contains(card.cardId)) return;
            int max = Mathf.Max(1, card.ownedCopies);
            _quantities[card.cardId] = Mathf.Clamp(quantity, 1, max);
            UpdateSelectionCount();
        }

        // Total COPIES across all selected cards (sum of chosen quantities).
        private int TotalSelectedCopies()
        {
            int total = 0;
            foreach (var id in _selected)
            {
                var card = _allCards.Find(c => c.cardId == id);
                total += card != null ? QuantityFor(card) : 1;
            }
            return total;
        }

        private void UpdateSelectionCount()
        {
            if (_selectionCountText != null) _selectionCountText.text = TotalSelectedCopies().ToString();
            if (_trashBtn != null) _trashBtn.interactable = _selected.Count > 0 && !_busy;
        }

        // ---- Trash / animation / confirm ----

        private void OnTrashClicked()
        {
            if (_busy || _selected.Count == 0) return;
            StartCoroutine(SalvageFlowRoutine());
        }

        // Animate selected cells (float up, then shrink while dropping into the trash can), then show
        // the confirmation dialog summarizing the total materials gained.
        private IEnumerator SalvageFlowRoutine()
        {
            _busy = true;
            UpdateSelectionCount();

            var animating = new List<SalvageCardCell>();
            foreach (var id in _selected)
                if (_cells.TryGetValue(id, out var cell) && cell != null) animating.Add(cell);

            // World-space target = the trash can centre.
            Vector3 target = _trashCanRect != null ? _trashCanRect.position : transform.position;

            // Phase 1: float up a little.
            float t = 0f;
            const float upDur = 0.22f;
            var startPos = animating.Select(c => c.RectTransform.position).ToList();
            while (t < upDur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / upDur);
                for (int i = 0; i < animating.Count; i++)
                {
                    if (animating[i] == null) continue;
                    var p = startPos[i];
                    p.y += 40f * k;
                    animating[i].RectTransform.position = p;
                }
                yield return null;
            }

            // Phase 2: shrink while dropping into the trash can.
            t = 0f;
            const float dropDur = 0.42f;
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

            foreach (var c in animating)
                if (c != null) c.gameObject.SetActive(false);

            ShowConfirm();
            _busy = false;
            UpdateSelectionCount();
        }

        private void ShowConfirm()
        {
            if (_confirmModal == null) return;
            _confirmSummary.text = BuildMaterialsSummary();
            _confirmModal.SetActive(true);
            _confirmModal.transform.SetAsLastSibling();
        }

        private void HideConfirm()
        {
            if (_confirmModal != null) _confirmModal.SetActive(false);
            // Restore any cells hidden by the animation (e.g. on cancel) by rebuilding.
            RebuildGrid();
        }

        // Aggregate yields across all selected cards, multiplied by each card's chosen quantity →
        // "12x Card Dust, 2x Ember Essence".
        private string BuildMaterialsSummary()
        {
            var totals = new Dictionary<string, (string name, int rarity, int qty)>();
            foreach (var id in _selected)
            {
                var card = _allCards.Find(c => c.cardId == id);
                if (card?.yields == null) continue;
                int copies = QuantityFor(card);
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
            if (totals.Count == 0)
            {
                sb.Append("No materials.");
                return sb.ToString();
            }
            foreach (var kvp in totals)
            {
                var (name, rarity, qty) = kvp.Value;
                var col = RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];
                string hex = ColorUtility.ToHtmlStringRGB(col);
                sb.Append($"  • <color=#{hex}><b>{qty}x {name}</b></color>\n");
            }
            return sb.ToString();
        }

        private async void OnConfirmSalvage()
        {
            if (_busy) return;
            _busy = true;
            if (_confirmModal != null) _confirmModal.SetActive(false);
            SetLoading(true);
            ShowStatus("Salvaging...");

            ResolveServices();

            // Snapshot the selected cards + chosen quantities up front (the dicts mutate as we go).
            var toSalvage = new List<(string cardId, int qty)>();
            foreach (var id in _selected)
            {
                var card = _allCards.Find(c => c.cardId == id);
                toSalvage.Add((id, card != null ? QuantityFor(card) : 1));
            }

            int ok = 0; // total COPIES successfully salvaged
            var blocked = new List<(string cardId, SalvageApiClient.DeckReferenceDto[] decks)>();
            InventoryApiClient.PlayerItemDto[] latestInventory = null;

            try
            {
                foreach (var (cardId, qty) in toSalvage)
                {
                    // Salvage one copy per server call; stop this card if it gets locked / owned-out.
                    int salvagedForCard = 0;
                    for (int copy = 0; copy < qty; copy++)
                    {
                        SalvageApiClient.SalvageCardResponse res;
                        try
                        {
                            res = await _salvageApi.SalvageCard(cardId);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[SalvageScreen] SalvageCard({cardId}) failed: {ex}");
                            break; // network/parse error — skip this card's remaining copies
                        }

                        if (res != null && res.success)
                        {
                            ok++;
                            salvagedForCard++;
                            if (res.updatedInventory != null) latestInventory = res.updatedInventory;
                        }
                        else if (res != null && res.errorCode == "card_in_deck")
                        {
                            blocked.Add((cardId, res.decks));
                            break; // locked — don't attempt the rest of this card's copies
                        }
                        else
                        {
                            // Other failure (e.g. owned-out): stop this card's remaining copies.
                            break;
                        }
                    }

                    // Fully consumed → drop from selection; otherwise leave it for the refresh to reconcile.
                    if (salvagedForCard >= qty)
                    {
                        _selected.Remove(cardId);
                        _quantities.Remove(cardId);
                    }
                }

                // Push the freshest inventory snapshot into the cache.
                if (latestInventory != null) _inventoryService?.ApplyPartialUpdate(latestInventory);

                // Invalidate caches so the host refresh shows the reduced collection + new materials.
                _collectionService?.InvalidateSummaryCache();
                _inventoryService?.InvalidateCache();

                // Reload the salvageable list (copies/locks changed server-side).
                await ReloadAfterSalvage();

                // Status line: success + any blocks.
                var msg = new StringBuilder();
                if (ok > 0) msg.Append($"Salvaged {ok} cop{(ok == 1 ? "y" : "ies")}. ");
                if (blocked.Count > 0)
                {
                    msg.Append($"{blocked.Count} card(s) are locked in a deck and were skipped.");
                    // Surface the first blocked card's deck so the player can deep-link.
                    var first = blocked[0];
                    if (first.decks != null && first.decks.Length > 0)
                        ShowLockToast(first.decks);
                }
                ShowStatus(msg.Length > 0 ? msg.ToString() : "Nothing was salvaged.");

                if (ok > 0) OnSalvaged?.Invoke();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[SalvageScreen] Salvage flow failed: {ex}");
            }
            finally
            {
                SetLoading(false);
                _busy = false;
                UpdateSelectionCount();
            }
        }

        private async Task ReloadAfterSalvage()
        {
            try
            {
                var summary = await _salvageApi.FetchSalvageable();
                _allCards = summary?.cards != null
                    ? new List<SalvageApiClient.SalvageableCardDto>(summary.cards)
                    : new List<SalvageApiClient.SalvageableCardDto>();
                _selected.RemoveWhere(id => !_allCards.Exists(c => c.cardId == id));
                ReconcileQuantities();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SalvageScreen] Reload failed: {ex}");
            }
        }

        // ---- Lock toast / deep-link ----

        private void ShowLockToastForCard(SalvageApiClient.SalvageableCardDto card)
        {
            // The summary tells us it's locked but not which deck — try the preview/decks via SalvageCard
            // is destructive, so we surface a generic message + a deck-browser fallback. If the player
            // has decks, deep-link to the first deck that contains this card.
            _pendingDeckId = null;
            if (_lockToastText != null)
                _lockToastText.text = $"\"{card.displayName ?? card.cardId}\" is locked in a deck and can't be salvaged. Remove it from the deck first.";

            ShowLockToastInternal();
            ResolveDeckForCardAsync(card.cardId);
        }

        // Resolve which deck contains the card (for the deep-link) without a destructive salvage call.
        private async void ResolveDeckForCardAsync(string cardId)
        {
            if (_deckService == null) ServiceLocator.TryResolve<DeckManagementService>(out _deckService);
            if (_deckService == null) return;
            try
            {
                var decks = await _deckService.GetPlayerDecksAsync();
                var match = decks?.FirstOrDefault(d => d.cardIds != null && d.cardIds.Contains(cardId));
                if (match != null)
                {
                    _pendingDeckId = match.deckId ?? match.id;
                    if (_lockToastDeckBtnLabel != null)
                        _lockToastDeckBtnLabel.text = $"Open \"{match.Name}\"";
                    if (_lockToastDeckBtn != null) _lockToastDeckBtn.interactable = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SalvageScreen] ResolveDeckForCard failed: {ex.Message}");
            }
        }

        private void ShowLockToast(SalvageApiClient.DeckReferenceDto[] decks)
        {
            var first = decks != null && decks.Length > 0 ? decks[0] : null;
            _pendingDeckId = first?.deckId;
            if (_lockToastText != null)
                _lockToastText.text = first != null
                    ? $"This card is locked in deck \"{first.displayName}\" ({first.copiesInDeck} cop{(first.copiesInDeck == 1 ? "y" : "ies")}). Remove it there first."
                    : "This card is locked in a deck.";
            if (_lockToastDeckBtnLabel != null && first != null)
                _lockToastDeckBtnLabel.text = $"Open \"{first.displayName}\"";
            ShowLockToastInternal();
        }

        private void ShowLockToastInternal()
        {
            if (_lockToast == null) return;
            if (_lockToastDeckBtn != null) _lockToastDeckBtn.interactable = !string.IsNullOrEmpty(_pendingDeckId);
            _lockToast.SetActive(true);
            _lockToast.transform.SetAsLastSibling();
        }

        private void HideLockToast()
        {
            if (_lockToast != null) _lockToast.SetActive(false);
        }

        // ---- Small UI builders ----

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

        // ---- Helpers ----

        private void ShowStatus(string msg) { if (_statusText != null) _statusText.text = msg; }
        private void SetLoading(bool on) { if (_loadingOverlay != null) _loadingOverlay.SetActive(on); }
    }
}
