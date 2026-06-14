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
    /// Crafting overlay panel. Shows craftable cards, their item requirements, and the craft button.
    ///
    /// Hierarchy (CraftingPanel — starts inactive):
    ///   CraftingPanel
    ///   ├── Header
    ///   │   ├── TitleText           "Crafting Workshop"
    ///   │   ├── DustBadge
    ///   │   │   ├── DustIcon        (Image)
    ///   │   │   └── DustAmountText  (Text) — card_dust balance
    ///   │   └── CloseButton         (Button)
    ///   ├── FilterRow
    ///   │   └── AffordableOnlyToggle (Toggle)
    ///   ├── RecipeScrollView
    ///   │   └── Viewport → Content  (VerticalLayoutGroup + ContentSizeFitter)
    ///   │       └── [CraftingRecipeItem × N — spawned at runtime]
    ///   ├── StatusText              (Text)
    ///   └── LoadingOverlay          (GameObject)
    /// </summary>
    public sealed class CraftingPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI dustAmountText;
        [SerializeField] private Button closeButton;

        [Header("Filters")]
        [SerializeField] private Toggle affordableOnlyToggle;

        [Header("Recipe List")]
        [SerializeField] private Transform recipeListContainer;
        [SerializeField] private GameObject recipeItemPrefab;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        private CraftingService _craftingService;
        private InventoryService _inventoryService;
        private PlayerCardCollectionService _collectionService;

        private List<CraftingApiClient.CraftableCardDto> _craftableCards = new();
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _inventory = new();
        private bool _affordableOnly;

        // Per-card CRAFT OVERVIEW (full-screen): tapping a recipe cell opens this instead of
        // direct-crafting. Crafting is confirmed FROM the overview. Created lazily in code.
        private CardCraftOverviewPanel _overview;

        // Pagination + layout (built in code — the scene has no pager and the scroll box is tiny).
        private const int PageSize = 9; // 3x3 grid
        private int _page;
        private bool _workshopLaidOut;
        private Button _prevBtn, _nextBtn;
        private TextMeshProUGUI _pageLabel;

        /// <summary>Fired after a successful craft — CardCollectionScreen subscribes to refresh.</summary>
        public event Action OnCraftSuccess;

        private void Awake()
        {
            DeckBuilderTextScale.Apply(dustAmountText, DeckBuilderTextScale.Role.Label);
            DeckBuilderTextScale.ApplyAutoSize(statusText, DeckBuilderTextScale.Role.Status);

            ApplyKenneySkin();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (affordableOnlyToggle != null)
                affordableOnlyToggle.onValueChanged.AddListener(v => { _affordableOnly = v; RebuildList(); });
        }

        /// <summary>Temporary Kenney art skin (code-side, no prefab wiring). Null-safe.</summary>
        private void ApplyKenneySkin()
        {
            if (!KenneyUiSkin.Available) return;

            // Brown window panel behind the overlay (skins the panel's own Image, or creates a
            // stretched backdrop child if it has none).
            KenneyUiSkin.EnsureWindowBackdrop(this);

            var viewport = recipeListContainer != null ? recipeListContainer.parent : null;
            if (viewport != null)
            {
                var vimg = viewport.GetComponent<Image>();
                if (vimg != null) KenneyUiSkin.SkinInsetImage(vimg);
                else KenneyUiSkin.EnsureInsetBackdrop(viewport);
                KenneyUiSkin.SkinScrollbarsUnder(viewport.parent != null ? viewport.parent : viewport);
            }

            KenneyUiSkin.SkinToggle(affordableOnlyToggle);
            if (affordableOnlyToggle != null)
            {
                var tlabel = affordableOnlyToggle.GetComponentInChildren<TMP_Text>(true);
                if (tlabel != null)
                {
                    tlabel.text = "Affordable only";
                    DeckBuilderTextScale.Apply(tlabel, DeckBuilderTextScale.Role.Label);
                }
            }
            KenneyUiSkin.SkinButtonWithLabel(closeButton, KenneyUiSkin.ButtonStyle.Icon, "X");

            // Unify typography on the Kenney theme font.
            KenneyUiSkin.ApplyFontUnder(this);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureWorkshopLayout();
            LoadDataAsync();
        }

        // Make the workshop fill the screen: stretch the recipe scroll-view, enlarge the affordability
        // toggle, and build a pagination bar in code (the scene has none). Idempotent.
        private void EnsureWorkshopLayout()
        {
            if (_workshopLaidOut) return;
            _workshopLaidOut = true;

            var panel = (RectTransform)transform;

            // 0) FULL-SCREEN: size the panel to the whole canvas (it's anchored to a tiny 100x100 root via
            // sizeDelta, leaving screen margins). Then re-anchor the chrome to the panel edges so the
            // header/toggle/close don't drift off when the panel grows.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var crt = (RectTransform)canvas.rootCanvas.transform;
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = crt.rect.size;

                if (closeButton != null)
                    KenneyUiSkin.SetRect(closeButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-40f, -40f), new Vector2(150f, 90f));
                if (dustAmountText != null)
                    KenneyUiSkin.SetRect(dustAmountText, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-220f, -56f), new Vector2(240f, 70f));
                if (affordableOnlyToggle != null)
                    KenneyUiSkin.SetRect(affordableOnlyToggle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(60f, -160f), new Vector2(520f, 64f));
                // Title (no serialized ref): the panel's TMP that reads "Crafting...".
                foreach (var t in GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t != null && t.text != null && t.text.StartsWith("Crafting"))
                    {
                        KenneyUiSkin.SetRect(t, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(60f, -56f), new Vector2(700f, 90f));
                        break;
                    }
                }
            }

            // 1) Stretch the recipe scroll-view to fill the panel. The scene anchors RecipeScrollView to
            // CENTER with a fixed 1257x650 size (and a zero-size Viewport), so re-anchor the RectTransforms
            // directly (don't rely on a ScrollRect component — there may be none).
            if (recipeListContainer != null && recipeListContainer.parent != null && recipeListContainer.parent.parent != null)
            {
                var scrollRT = recipeListContainer.parent.parent; // RecipeScrollView
                var viewport = recipeListContainer.parent;        // Viewport (was 0-size)
                KenneyUiSkin.Fill(scrollRT, left: 24f, right: 24f, top: 250f, bottom: 280f);
                KenneyUiSkin.Fill(viewport);
            }

            // 2) Enlarge the affordability toggle (it was tiny) and set a real caption.
            if (affordableOnlyToggle != null)
            {
                var tr = (RectTransform)affordableOnlyToggle.transform;
                tr.sizeDelta = new Vector2(Mathf.Max(tr.sizeDelta.x, 460f), 64f);
                // Enlarge the checkbox graphic (Background) + its checkmark.
                var bg = affordableOnlyToggle.targetGraphic as Image;
                if (bg != null)
                {
                    var brt = bg.rectTransform;
                    brt.sizeDelta = new Vector2(56f, 56f);
                }
                // Label: search children AND siblings for a TMP/Text showing the GameObject name.
                var lbl = affordableOnlyToggle.GetComponentInChildren<TMP_Text>(true);
                if (lbl == null && affordableOnlyToggle.transform.parent != null)
                    lbl = affordableOnlyToggle.transform.parent.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null)
                {
                    lbl.text = "Affordable only";
                    DeckBuilderTextScale.Apply(lbl, DeckBuilderTextScale.Role.Label);
                }
            }

            // 3) Pagination bar pinned near the bottom of the panel.
            var pager = new GameObject("CraftPager", typeof(RectTransform));
            var prt = (RectTransform)pager.transform;
            prt.SetParent(panel, false);
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 150f);
            prt.sizeDelta = new Vector2(560f, 90f);

            _prevBtn = MakePagerButton(prt, "‹ Prev", new Vector2(0f, 0.5f), new Vector2(-200f, 0f));
            _nextBtn = MakePagerButton(prt, "Next ›", new Vector2(1f, 0.5f), new Vector2(200f, 0f));
            _prevBtn.onClick.AddListener(() => { if (_page > 0) { _page--; RebuildList(); } });
            _nextBtn.onClick.AddListener(() => { _page++; RebuildList(); });

            var lblGo = new GameObject("PageLabel", typeof(RectTransform));
            _pageLabel = lblGo.AddComponent<TextMeshProUGUI>();
            var lrt = (RectTransform)lblGo.transform;
            lrt.SetParent(prt, false);
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(180f, 70f);
            _pageLabel.alignment = TextAlignmentOptions.Center;
            _pageLabel.raycastTarget = false;
            DeckBuilderTextScale.Apply(_pageLabel, DeckBuilderTextScale.Role.Label);
        }

        private Button MakePagerButton(RectTransform parent, string label, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(label + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(170f, 76f);
            var lblGo = new GameObject("Label", typeof(RectTransform));
            var t = lblGo.AddComponent<TextMeshProUGUI>();
            var lrt = (RectTransform)lblGo.transform;
            lrt.SetParent(rt, false);
            KenneyUiSkin.Fill(t);
            t.alignment = TextAlignmentOptions.Center;
            var btn = go.GetComponent<Button>();
            KenneyUiSkin.SkinButtonWithLabel(btn, KenneyUiSkin.ButtonStyle.Nav, label);
            return btn;
        }

        public void Hide() => gameObject.SetActive(false);

        private async void LoadDataAsync()
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ServiceLocator.TryResolve<CraftingService>(out _craftingService);
            ServiceLocator.TryResolve<InventoryService>(out _inventoryService);
            ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);

            if (_craftingService == null || _inventoryService == null)
            {
                ShowStatus("Services unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var cardsTask = _craftingService.GetCraftableCardsAsync();
                var invTask = _inventoryService.GetInventoryAsync();
                await Task.WhenAll(cardsTask, invTask);

                _craftableCards = cardsTask.Result;
                _inventory = invTask.Result;

                RefreshDustDisplay();
                RebuildList();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading crafting data: {ex.Message}");
                Debug.LogError($"[CraftingPanel] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void RebuildList()
        {
            if (recipeListContainer == null || recipeItemPrefab == null) return;

            foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

            var toShow = _affordableOnly
                ? _craftableCards.FindAll(c => _inventoryService?.CanAffordCraft(c.requirements, _inventory) == true)
                : _craftableCards;

            // OVERHAUL: paginated 3x3 grid of craftable-card cells. Drop any VerticalLayoutGroup so the
            // GridLayoutGroup drives the cells, and force exactly 3 columns.
            var vlg = recipeListContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Destroy(vlg);
            KenneyUiSkin.EnsureGrid(recipeListContainer, cellSize: new Vector2(190f, 300f), spacing: new Vector2(16f, 18f), padding: 12);
            var grid = recipeListContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                // Size cells to fill the real width so 3 columns of BIG cards fill the screen.
                Canvas.ForceUpdateCanvases();
                float availW = ((RectTransform)recipeListContainer).rect.width;
                if (availW < 50f) availW = Mathf.Max(600f, ((RectTransform)transform).rect.width - 140f);
                float pad = grid.padding.left + grid.padding.right;
                float sp = grid.spacing.x * 2f;
                float cw = Mathf.Max(240f, (availW - pad - sp) / 3f);
                grid.cellSize = new Vector2(cw, cw * 1.5f); // 2:3 card so the composite fits + badges align
            }

            // Pagination over the filtered set.
            int total = Mathf.Max(1, Mathf.CeilToInt(toShow.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, total - 1);
            int start = _page * PageSize;
            int end = Mathf.Min(start + PageSize, toShow.Count);

            for (int i = start; i < end; i++)
            {
                var card = toShow[i];
                var go = Instantiate(recipeItemPrefab, recipeListContainer);
                var item = go.GetComponent<CraftingRecipeItem>();
                if (item == null) continue;
                bool canAfford = _inventoryService?.CanAffordCraft(card.requirements, _inventory) == true;
                // Tapping a cell opens the per-card OVERVIEW (crafting is confirmed there), it no
                // longer crafts directly from the grid.
                item.Bind(card, _inventory, canAfford, OpenOverview);
            }

            // The grid sizes the cells now; apply the stat badges once the overlay rects are valid
            // (at Bind time the cell rect is still 0, which would collapse the badges).
            Canvas.ForceUpdateCanvases();
            foreach (Transform ch in recipeListContainer)
            {
                var it = ch.GetComponent<CraftingRecipeItem>();
                if (it != null) it.RefreshBadges();
            }

            if (_pageLabel != null) _pageLabel.text = $"{_page + 1} / {total}";
            if (_prevBtn != null) _prevBtn.interactable = _page > 0;
            if (_nextBtn != null) _nextBtn.interactable = _page < total - 1;

            if (toShow.Count == 0)
                ShowStatus(_affordableOnly ? "No affordable recipes." : "No craftable cards available.");
            else
                ShowStatus(string.Empty);
        }

        // Opens the per-card craft OVERVIEW for the tapped recipe cell. Crafting is then confirmed
        // from the overview's Craft button (wired to OnCraftRequested via OnCraftConfirmed).
        private void OpenOverview(string cardId)
        {
            var card = _craftableCards.Find(c => c != null && c.cardId == cardId);
            if (card == null) return;
            EnsureOverview();
            _overview.Show(card, _inventory);
        }

        // Lazily build the overview as a full-screen child of the same canvas as this panel, so it
        // draws on top. No prefab wiring — the panel builds its own UI in code.
        private void EnsureOverview()
        {
            if (_overview != null) return;

            var canvas = GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform : transform;

            var go = new GameObject("CardCraftOverviewPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling(); // draw above the crafting grid
            _overview = go.AddComponent<CardCraftOverviewPanel>();
            _overview.OnCraftConfirmed += OnCraftRequested;
            go.SetActive(false);
        }

        private async void OnCraftRequested(string cardId)
        {
            if (_craftingService == null) return;
            SetLoading(true);
            _overview?.SetLoading(true);
            ShowStatus("Crafting...");

            try
            {
                var result = await _craftingService.CraftCardAsync(cardId);
                if (result?.success == true)
                {
                    // Update cached inventory from response
                    if (result.updatedInventory != null)
                        _inventoryService?.ApplyPartialUpdate(result.updatedInventory);

                    // Invalidate collection cache — new card was added
                    _collectionService?.InvalidateSummaryCache();

                    ShowStatus($"Crafted {result.playerCard?.displayName ?? cardId}!");
                    _inventory = await _inventoryService.GetInventoryAsync();
                    RefreshDustDisplay();
                    RebuildList();
                    OnCraftSuccess?.Invoke();

                    // Crafting was confirmed from the overview — refresh affordability there, then close.
                    if (_overview != null)
                    {
                        _overview.RefreshAfterCraft(_inventory);
                        _overview.Hide();
                    }
                }
                else
                {
                    // 409 or other failure — message from server
                    ShowStatus(result?.message ?? "Craft failed.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CraftingPanel] Craft failed: {ex}");
            }
            finally { SetLoading(false); _overview?.SetLoading(false); }
        }

        private void RefreshDustDisplay()
        {
            if (dustAmountText == null) return;
            _inventory.TryGetValue(InventoryService.CardDustKey, out var dust);
            dustAmountText.text = (dust?.quantity ?? 0).ToString("N0");
        }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
