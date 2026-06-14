using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// REUSABLE paginated card grid (configurable columns/rows) + filter row (search + Filter popup +
    /// Clear) + pagination (Prev / page / Next). The Filter button opens the chip-based
    /// <see cref="CardFilterPanel"/> (faction / rarity / type / mana). Builds <see cref="CardCellView"/>s
    /// into a width-filling grid and
    /// is shared by all three deck-builder tabs (Decks / Create / Salvage) — the ONLY per-tab difference
    /// is the per-cell click callback + the cell options supplied via <see cref="CardEntry"/>.
    ///
    /// Built entirely in code (no prefab wiring); call <see cref="Create"/>, then <see cref="SetEntries"/>.
    /// This consolidates the duplicated grid/pagination/filter logic previously living in CraftingPanel,
    /// SalvageScreen and CardCollectionScreen.
    /// </summary>
    public sealed class CardGridView : MonoBehaviour
    {
        /// <summary>One grid entry: a card plus the flags the cell needs and a per-card click handler.</summary>
        public sealed class CardEntry
        {
            public string cardId;
            public string displayName;
            public int rarity;    // for filtering
            public int faction;   // for filtering
            public int cardType;  // for filtering (Unit=0, Utility=1, Equipment=2, Spell=3)
            public int manaCost;  // for filtering (mana-bucket)
            public CardCellView.CardCellOptions options;
            public Action<CardCellView> onClicked;
            public Action<CardCellView, int> onQuantityChanged;
        }

        private CardCatalogCache _catalog;

        // ---- Grid geometry (configurable per host; see Create) ----
        private int _columns = 3;     // cards per row
        private int _rows = 3;        // visible rows per page
        private int PageSize => Mathf.Max(1, _columns * _rows);

        // ---- Built UI ----
        private RectTransform _gridContent;
        private RectTransform _gridViewport;
        private TMP_InputField _searchField;
        private Button _filterBtn, _clearBtn;
        private TextMeshProUGUI _filterLabel;
        private Button _prevBtn, _nextBtn;
        private TextMeshProUGUI _pageLabel, _emptyLabel;
        private CardFilterPanel _filterPanel;

        // ---- State ----
        private readonly List<CardEntry> _all = new();
        private List<CardEntry> _filtered = new();
        private readonly Dictionary<string, CardCellView> _cells = new();
        private int _page;
        private string _search = string.Empty;
        private string _emptyText = "No cards.";

        /// <summary>The visible cell for a cardId on the current page, or null.</summary>
        public CardCellView GetCell(string cardId) =>
            _cells.TryGetValue(cardId, out var c) ? c : null;

        /// <summary>
        /// Creates a CardGridView filling its parent (the host typically sizes the parent region). Pass
        /// the catalog (or null to resolve lazily). The grid builds a filter row at the top, the scroll
        /// grid in the middle, and a pager at the bottom.
        ///
        /// <paramref name="columns"/> / <paramref name="rows"/> set the grid density: the default 3x3
        /// stays for the Decks tab, while a denser grid (e.g. 4 columns) makes cards read smaller so
        /// more fit at once (Create / Salvage tabs).
        /// </summary>
        public static CardGridView Create(Transform parent, CardCatalogCache catalog, int columns = 3, int rows = 3)
        {
            var go = new GameObject("CardGridView", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            KenneyUiSkin.Fill(rt);
            var grid = go.AddComponent<CardGridView>();
            grid._catalog = catalog;
            grid._columns = Mathf.Max(1, columns);
            grid._rows = Mathf.Max(1, rows);
            grid.Build(rt);
            return grid;
        }

        private void Build(RectTransform root)
        {
            if (_catalog == null) ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);

            BuildFilterRow(root);
            BuildGrid(root);
            BuildPager(root);

            // Chip-based filter popup (overlay, hidden until the Filter button is tapped). Built last so
            // it can be raised above the grid; rebuilds the grid whenever a chip/Clear toggles.
            // Parent the filter popup to the ROOT CANVAS so its dim backdrop covers the WHOLE screen and
            // draws above everything (a child-of-grid backdrop only dimmed the grid area / sat behind cards).
            var fpCanvas = root.GetComponentInParent<Canvas>();
            var fpParent = fpCanvas != null ? fpCanvas.rootCanvas.transform : (Transform)root;
            _filterPanel = CardFilterPanel.Create(fpParent);
            _filterPanel.OnChanged += () => { _page = 0; ApplyFilters(); UpdateFilterButtonState(); };
            // Drop the panel down from the Filter button (built above) instead of centring it.
            if (_filterBtn != null) _filterPanel.SetAnchorButton((RectTransform)_filterBtn.transform);

            _emptyLabel = MakeText(root, "EmptyLabel", string.Empty, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_emptyLabel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(700f, 80f));
            _emptyLabel.raycastTarget = false;
            DeckBuilderTextScale.ApplyAutoSize(_emptyLabel, DeckBuilderTextScale.Role.Status);
            _emptyLabel.gameObject.SetActive(false);
        }

        // Filter row pinned to the top: [search ............] [Filter] [Clear]. The Filter button opens
        // the chip-based CardFilterPanel popup; Clear resets the search + every chip.
        private void BuildFilterRow(RectTransform root)
        {
            var rowGo = new GameObject("FilterRow", typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(root, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(8f, -148f);
            row.offsetMax = new Vector2(-8f, -8f);

            // Search input.
            var sfGo = new GameObject("SearchField", typeof(RectTransform), typeof(Image));
            var sfRt = (RectTransform)sfGo.transform;
            sfRt.SetParent(row, false);
            // Stretch the search field from the left edge up to a fixed gap reserved for the Filter +
            // Clear cluster on the right (see SetRowRectRight offsets below). Stretching (rather than a
            // fixed width) keeps it from overlapping the buttons on narrower / wider devices.
            // GOTCHA: with anchorMax.x = 1, do NOT set sizeDelta.x afterwards — it would override offsetMax.
            sfRt.anchorMin = new Vector2(0f, 0f);
            sfRt.anchorMax = new Vector2(1f, 1f);
            sfRt.pivot = new Vector2(0.5f, 0.5f);
            sfRt.offsetMin = new Vector2(0f, 0f);
            sfRt.offsetMax = new Vector2(-680f, 0f); // room for Filter (380) + gap + Clear (240) + gaps
            var sfImg = sfGo.GetComponent<Image>();
            sfImg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInputImage(sfImg);
            _searchField = sfGo.AddComponent<TMP_InputField>();
            var textArea = new GameObject("Text", typeof(RectTransform));
            var taRt = (RectTransform)textArea.transform;
            taRt.SetParent(sfRt, false);
            KenneyUiSkin.Fill(taRt, 16f, 16f, 6f, 6f);
            var tComp = textArea.AddComponent<TextMeshProUGUI>();
            tComp.alignment = TextAlignmentOptions.MidlineLeft;
            tComp.color = Color.white;
            tComp.enableWordWrapping = false;
            DeckBuilderTextScale.Apply(tComp, DeckBuilderTextScale.Role.Label);
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            var phRt = (RectTransform)phGo.transform;
            phRt.SetParent(sfRt, false);
            KenneyUiSkin.Fill(phRt, 16f, 16f, 6f, 6f);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = "Search...";
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.color = new Color(1f, 1f, 1f, 0.45f);
            ph.enableWordWrapping = false;
            DeckBuilderTextScale.Apply(ph, DeckBuilderTextScale.Role.Label);
            _searchField.textViewport = sfRt;
            _searchField.textComponent = tComp;
            _searchField.placeholder = ph;
            _searchField.onValueChanged.AddListener(v => { _search = v ?? string.Empty; _page = 0; ApplyFilters(); });

            // Filter button — opens the chip-based filter panel (FACCIÓN / RAREZA / TIPO / MANA).
            _filterBtn = MakeButton(row, "FilterButton", "Filter", KenneyUiSkin.ButtonStyle.Nav);
            SetRowRectRight(_filterBtn, 264f, 380f);
            _filterLabel = _filterBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            _filterBtn.onClick.AddListener(() => { if (_filterPanel != null) _filterPanel.Toggle(); });

            // Clear button.
            _clearBtn = MakeButton(row, "ClearFilters", "Clear", KenneyUiSkin.ButtonStyle.Nav);
            SetRowRectRight(_clearBtn, 0f, 240f);
            _clearBtn.onClick.AddListener(ClearFilters);
        }

        // Anchors a control to the RIGHT edge of the filter row at the given right-offset and width.
        private void SetRowRectRight(Component c, float rightOffset, float width)
        {
            if (c == null || !(c.transform is RectTransform rt)) return;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = new Vector2(-rightOffset, 0f);
        }

        private void BuildGrid(RectTransform root)
        {
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(root, false);
            // Fill between the filter row (top) and the pager (bottom).
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(8f, 160f);    // leave room for the (taller) pager
            scrollRt.offsetMax = new Vector2(-8f, -158f);  // below the (taller) filter row
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.18f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(scrollImg);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.SetParent(scrollRt, false);
            KenneyUiSkin.Fill(viewportRt);
            _gridViewport = viewportRt;
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
            // No vertical scroll: the page is a FIXED columns×rows grid sized to fit the viewport
            // (see RebuildGrid — cell size is clamped by both viewport width AND height). Pagination
            // moves between pages instead of scrolling within one.
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private void BuildPager(RectTransform root)
        {
            var pagerGo = new GameObject("Pager", typeof(RectTransform));
            var prt = (RectTransform)pagerGo.transform;
            prt.SetParent(root, false);
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            // Shift the pager slightly left of centre so the bottom strip's right corner stays free for
            // the Salvage trash can (which lives in that corner and must not overlap the card grid).
            prt.anchoredPosition = new Vector2(-94f, 18f);
            prt.sizeDelta = new Vector2(840f, 132f);

            _prevBtn = MakeButton(prt, "PrevBtn", "‹ Prev", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(_prevBtn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(300f, 124f));
            _prevBtn.onClick.AddListener(() => { if (_page > 0) { _page--; RebuildGrid(); } });

            _nextBtn = MakeButton(prt, "NextBtn", "Next ›", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(_nextBtn, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0f), new Vector2(300f, 124f));
            _nextBtn.onClick.AddListener(() => { _page++; RebuildGrid(); });

            _pageLabel = MakeText(prt, "PageLabel", "1 / 1", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_pageLabel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(240f, 100f));
            _pageLabel.raycastTarget = false;
            DeckBuilderTextScale.Apply(_pageLabel, DeckBuilderTextScale.Role.Label);
        }

        // ---- Public API ----

        /// <summary>Replace the grid's data. Resets to page 0 and rebuilds. <paramref name="emptyText"/>
        /// shows when no entries match.</summary>
        public void SetEntries(IEnumerable<CardEntry> entries, string emptyText = "No cards.")
        {
            _all.Clear();
            if (entries != null) _all.AddRange(entries);
            _emptyText = emptyText ?? "No cards.";
            _page = 0;
            ApplyFilters();
        }

        /// <summary>Re-applies the current filters and rebuilds the page (e.g. after a selection toggle
        /// that changed an entry's options). Keeps the current page.</summary>
        public void Refresh() => RebuildGrid();

        private void ClearFilters()
        {
            _search = string.Empty;
            if (_searchField != null) _searchField.SetTextWithoutNotify(string.Empty);
            _filterPanel?.ClearAll();
            UpdateFilterButtonState();
            _page = 0;
            ApplyFilters();
        }

        // Show "Filter •" (active dot) when any chip is selected so the collapsed button hints state.
        private void UpdateFilterButtonState()
        {
            if (_filterLabel == null) return;
            bool active = _filterPanel != null && _filterPanel.AnySelected;
            _filterLabel.text = active ? "Filter •" : "Filter";
        }

        private void ApplyFilters()
        {
            string s = _search?.Trim().ToLowerInvariant() ?? string.Empty;

            // Per-category selected sets (within a category = OR; across categories = AND).
            var factions = _filterPanel?.SelectedFactions;
            var rarities = _filterPanel?.SelectedRarities;
            var types = _filterPanel?.SelectedTypes;
            var manaBuckets = _filterPanel?.SelectedManaBuckets;

            _filtered = _all.Where(e =>
                (string.IsNullOrEmpty(s) ||
                 (e.displayName ?? e.cardId ?? string.Empty).ToLowerInvariant().Contains(s) ||
                 (e.cardId ?? string.Empty).ToLowerInvariant().Contains(s)) &&
                (factions == null || factions.Count == 0 || factions.Contains(e.faction)) &&
                (rarities == null || rarities.Count == 0 || rarities.Contains(e.rarity)) &&
                (types == null || types.Count == 0 || types.Contains(e.cardType)) &&
                (manaBuckets == null || manaBuckets.Count == 0 ||
                 manaBuckets.Contains(CardFilterPanel.ManaBucketFor(e.manaCost)))).ToList();
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (_gridContent == null) return;

            foreach (Transform child in _gridContent) Destroy(child.gameObject);
            _cells.Clear();

            // N-column grid sized to fill the viewport (2:3 cells so the card composite fits). N is
            // _columns (default 3 for Decks; denser for Create / Salvage → smaller cards, more visible).
            KenneyUiSkin.EnsureGrid(_gridContent, cellSize: new Vector2(240f, 360f), spacing: new Vector2(16f, 18f), padding: 12);
            var grid = _gridContent.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = _columns;
                Canvas.ForceUpdateCanvases();

                float availW = _gridContent.rect.width;
                if (availW < 50f)
                {
                    var pr = transform as RectTransform;
                    availW = Mathf.Max(600f, (pr != null ? pr.rect.width : 1000f) - 60f);
                }
                float padX = grid.padding.left + grid.padding.right;
                float spX = grid.spacing.x * (_columns - 1);
                float cwByWidth = (availW - padX - spX) / _columns;

                // Also clamp by viewport HEIGHT so the full _columns×_rows page fits without scrolling
                // (cells are 2:3, so a cell of width cw is cw*1.5 tall).
                float availH = _gridViewport != null ? _gridViewport.rect.height : 0f;
                float cwByHeight = float.MaxValue;
                if (availH > 50f)
                {
                    float padY = grid.padding.top + grid.padding.bottom;
                    float spY = grid.spacing.y * (_rows - 1);
                    cwByHeight = (availH - padY - spY) / _rows / 1.5f;
                }

                float cw = Mathf.Max(120f, Mathf.Min(cwByWidth, cwByHeight));
                grid.cellSize = new Vector2(cw, cw * 1.5f);
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(_filtered.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, totalPages - 1);
            int start = _page * PageSize;
            int end = Mathf.Min(start + PageSize, _filtered.Count);

            for (int i = start; i < end; i++)
            {
                var entry = _filtered[i];
                var cell = CardCellView.Create(_gridContent, _catalog);
                cell.Bind(entry.cardId, entry.displayName, entry.options, entry.onClicked, entry.onQuantityChanged);
                _cells[entry.cardId] = cell;
            }

            // Apply stat badges only after the grid lays the cells out (rect is 0 at instantiate).
            Canvas.ForceUpdateCanvases();
            foreach (var kvp in _cells) kvp.Value.RefreshBadges();

            if (_pageLabel != null) _pageLabel.text = $"{_page + 1} / {totalPages}";
            if (_prevBtn != null) _prevBtn.interactable = _page > 0;
            if (_nextBtn != null) _nextBtn.interactable = _page < totalPages - 1;

            if (_emptyLabel != null)
            {
                bool empty = _filtered.Count == 0;
                _emptyLabel.gameObject.SetActive(empty);
                if (empty) _emptyLabel.text = _emptyText;
            }
        }

        // ---- Small builders ----

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
    }
}
