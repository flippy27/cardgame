using System;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.UI;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// REUSABLE single-card cell — the single source of truth for how a card is drawn in the
    /// deck-builder grids (Decks / Create / Salvage all use this via <see cref="CardGridView"/>).
    /// Built entirely in code (no prefab wiring); call <see cref="Create"/> then <see cref="Bind"/>.
    ///
    /// Renders the card the IN-GAME way: the framed composite (<see cref="CardArtLibrary.GetCardComposite"/>)
    /// on a 2:3 full-cover Image (preserveAspect = false) with <see cref="CardStatBadges"/> drawn over a
    /// full-cover StatsOverlay. Stats/meta come from the <see cref="CardCatalogCache"/>.
    ///
    /// BADGE GOTCHA (same as CraftingRecipeItem / SalvageCardCell): the overlay rect is 0 at instantiate,
    /// so the badges must be applied AFTER the grid lays the cell out. <see cref="Bind"/> only stores
    /// state and draws the art; the grid calls <see cref="RefreshBadges"/> after
    /// <c>Canvas.ForceUpdateCanvases()</c>.
    ///
    /// Configurable extras live in <see cref="CardCellOptions"/>: copies "×N" badge, a quantity stepper
    /// (− / N / +, clamped to [1, max]), a lock badge, and a selection outline.
    /// </summary>
    public sealed class CardCellView : MonoBehaviour
    {
        /// <summary>Per-cell display configuration. Defaults to a plain card (no extras).</summary>
        public struct CardCellOptions
        {
            public bool showCopies;       // draw the "×N" badge (top-right)
            public int copies;            // N for the copies badge
            public bool showStepper;      // draw the − / N / + quantity stepper
            public int stepperMax;        // upper bound for the stepper (lower bound is always 1)
            public bool showLock;         // draw the lock badge (top-left)
            public bool selectable;       // allow the selection outline to show when selected
            public bool selected;         // initial selected state
            public bool dim;              // dim the art (e.g. locked / unavailable)
            public int level;             // effective upgrade level; draws a "★N" badge when > 1

            public static CardCellOptions Plain => new CardCellOptions { stepperMax = 1, copies = 1 };
        }

        private static readonly Color[] FactionColors =
        {
            new Color(1.00f, 0.45f, 0.10f), // Ember
            new Color(0.10f, 0.75f, 0.95f), // Tidal
            new Color(0.20f, 0.75f, 0.30f), // Grove
            new Color(0.70f, 0.70f, 0.75f), // Alloy
            new Color(0.45f, 0.10f, 0.70f), // Void
        };

        private static readonly Color SelectedOutline = new Color(0.20f, 0.85f, 1.00f, 1f);

        private CardCatalogCache _catalog;

        // ---- Built widgets ----
        private Image _bg;
        private Image _artImage;
        private RectTransform _overlay;
        private Image _selectionOutline;
        private GameObject _lockBadge;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _copiesText;
        private GameObject _levelBadge;
        private TextMeshProUGUI _levelText;
        private Button _button;

        private GameObject _stepper;
        private Button _minusBtn;
        private Button _plusBtn;
        private TextMeshProUGUI _qtyText;

        // ---- State ----
        private string _cardId;
        private CardCellOptions _options;
        private Action<CardCellView> _onClicked;
        private Action<CardCellView, int> _onQuantityChanged;
        private int _quantity = 1;

        public string CardId => _cardId;
        public bool Selected { get; private set; }
        public int Quantity => _quantity;
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>Creates an unbound cell under <paramref name="parent"/>. Pass the catalog (or null to
        /// resolve lazily).</summary>
        public static CardCellView Create(Transform parent, CardCatalogCache catalog)
        {
            var go = new GameObject("CardCellView", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var cell = go.AddComponent<CardCellView>();
            cell._catalog = catalog;
            cell.BuildLayout();
            return cell;
        }

        private void BuildLayout()
        {
            // Cell background (mostly hidden behind the art; used as a faction-tint fallback base).
            _bg = GetComponent<Image>();
            _bg.color = new Color(0.16f, 0.17f, 0.20f, 0.6f);
            _bg.raycastTarget = false;

            // Framed card art fills the 2:3 cell (no preserveAspect so badges line up with the frame).
            var artGo = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
            _artImage = artGo.GetComponent<Image>();
            artGo.transform.SetParent(transform, false);
            KenneyUiSkin.Fill(_artImage);
            _artImage.preserveAspect = false;
            _artImage.raycastTarget = false;

            // Stat-badge overlay (covers the art); badges applied later in RefreshBadges().
            var ovGo = new GameObject("StatsOverlay", typeof(RectTransform));
            _overlay = (RectTransform)ovGo.transform;
            _overlay.SetParent(transform, false);
            KenneyUiSkin.Fill(_overlay);

            // Selection outline (hidden until selected).
            var selGo = new GameObject("SelectionOutline", typeof(RectTransform), typeof(Image));
            _selectionOutline = selGo.GetComponent<Image>();
            selGo.transform.SetParent(transform, false);
            KenneyUiSkin.Fill(_selectionOutline);
            _selectionOutline.color = new Color(SelectedOutline.r, SelectedOutline.g, SelectedOutline.b, 0.28f);
            _selectionOutline.raycastTarget = false;
            selGo.SetActive(false);

            // Lock badge (top-left).
            _lockBadge = new GameObject("LockBadge", typeof(RectTransform), typeof(Image));
            var lockRt = (RectTransform)_lockBadge.transform;
            lockRt.SetParent(transform, false);
            lockRt.anchorMin = new Vector2(0f, 1f);
            lockRt.anchorMax = new Vector2(0f, 1f);
            lockRt.pivot = new Vector2(0f, 1f);
            lockRt.anchoredPosition = new Vector2(8f, -8f);
            lockRt.sizeDelta = new Vector2(56f, 56f);
            _lockBadge.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            _lockBadge.GetComponent<Image>().raycastTarget = false;
            var lockIcon = new GameObject("Icon", typeof(RectTransform));
            var lockText = lockIcon.AddComponent<TextMeshProUGUI>();
            lockIcon.transform.SetParent(lockRt, false);
            KenneyUiSkin.Fill(lockText);
            lockText.text = "\U0001F512"; // lock
            lockText.alignment = TextAlignmentOptions.Center;
            lockText.fontSize = 34f;
            lockText.enableAutoSizing = false;
            lockText.raycastTarget = false;
            _lockBadge.SetActive(false);

            // Name (bottom).
            _nameText = MakeText("CardNameText", TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_nameText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(-8f, 46f));
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;
            DeckBuilderTextScale.ApplyAutoSize(_nameText, DeckBuilderTextScale.Role.CardName);

            // Copies badge (top-right).
            _copiesText = MakeText("CopiesText", TextAlignmentOptions.TopRight);
            KenneyUiSkin.SetRect(_copiesText, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(120f, 44f));
            DeckBuilderTextScale.Apply(_copiesText, DeckBuilderTextScale.Role.Label);
            _copiesText.gameObject.SetActive(false);

            // Level badge ("★N") pinned bottom-left, just above the name row. Hidden unless level > 1.
            _levelBadge = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
            var lvRt = (RectTransform)_levelBadge.transform;
            lvRt.SetParent(transform, false);
            lvRt.anchorMin = new Vector2(0f, 0f);
            lvRt.anchorMax = new Vector2(0f, 0f);
            lvRt.pivot = new Vector2(0f, 0f);
            lvRt.anchoredPosition = new Vector2(8f, 58f); // sit just above the name row (name at y=8, h=46)
            lvRt.sizeDelta = new Vector2(96f, 44f);
            var lvBg = _levelBadge.GetComponent<Image>();
            lvBg.color = new Color(0f, 0f, 0f, 0.7f);
            lvBg.raycastTarget = false;
            _levelText = MakeText("LevelText", TextAlignmentOptions.Center);
            _levelText.transform.SetParent(lvRt, false);
            KenneyUiSkin.Fill(_levelText, 6f, 6f, 2f, 2f);
            _levelText.color = new Color(1f, 0.85f, 0.25f); // gold star/level
            _levelText.enableWordWrapping = false;
            DeckBuilderTextScale.Apply(_levelText, DeckBuilderTextScale.Role.Label);
            _levelBadge.SetActive(false);

            // Full-cell tap target (built before the stepper so the stepper buttons sit on top of it).
            _button = GetComponent<Button>();
            _button.transition = Selectable.Transition.None;
            var tapGo = new GameObject("TapTarget", typeof(RectTransform), typeof(Image));
            var tapRt = (RectTransform)tapGo.transform;
            tapRt.SetParent(transform, false);
            KenneyUiSkin.Fill(tapRt);
            var tapImg = tapGo.GetComponent<Image>();
            tapImg.color = new Color(1f, 1f, 1f, 0f);
            tapImg.raycastTarget = true;
            _button.targetGraphic = tapImg;
            tapGo.transform.SetAsLastSibling();
            _button.onClick.AddListener(() => _onClicked?.Invoke(this));

            BuildStepper();
        }

        // Compact − / count / + stepper pinned bottom-centre, just above the name. Hidden by default.
        private void BuildStepper()
        {
            _stepper = new GameObject("QtyStepper", typeof(RectTransform), typeof(Image));
            var srt = (RectTransform)_stepper.transform;
            srt.SetParent(transform, false);
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 56f); // above the name row (name at y=8, h=46)
            srt.sizeDelta = new Vector2(176f, 56f);
            var bg = _stepper.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            _minusBtn = MakeStepperButton("MinusBtn", "−"); // minus sign
            KenneyUiSkin.SetRect(_minusBtn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, 0f), new Vector2(52f, 52f));
            _minusBtn.onClick.AddListener(() => ChangeQuantity(-1));

            _plusBtn = MakeStepperButton("PlusBtn", "+");
            KenneyUiSkin.SetRect(_plusBtn, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f), new Vector2(52f, 52f));
            _plusBtn.onClick.AddListener(() => ChangeQuantity(+1));

            _qtyText = MakeText("QtyText", TextAlignmentOptions.Center);
            _qtyText.transform.SetParent(_stepper.transform, false);
            KenneyUiSkin.SetRect(_qtyText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(64f, 52f));
            DeckBuilderTextScale.Apply(_qtyText, DeckBuilderTextScale.Role.Label);

            _stepper.SetActive(false);
        }

        private Button MakeStepperButton(string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_stepper.transform, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.32f, 0.40f, 1f);
            img.raycastTarget = true;

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
            btn.targetGraphic = img;
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(btn, KenneyUiSkin.ButtonStyle.Icon);
            return btn;
        }

        /// <summary>
        /// Bind a card to this cell. <paramref name="onClicked"/> fires when the cell body is tapped
        /// (the grid/tab decides what that means). <paramref name="onQuantityChanged"/> fires when the
        /// stepper changes (only relevant when <see cref="CardCellOptions.showStepper"/> is set).
        /// </summary>
        public void Bind(
            string cardId,
            string displayName,
            CardCellOptions options,
            Action<CardCellView> onClicked,
            Action<CardCellView, int> onQuantityChanged = null)
        {
            _cardId = cardId;
            _options = options;
            _onClicked = onClicked;
            _onQuantityChanged = onQuantityChanged;
            _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, options.stepperMax));

            if (_nameText != null) _nameText.text = displayName ?? cardId;

            if (_copiesText != null)
            {
                _copiesText.gameObject.SetActive(options.showCopies);
                if (options.showCopies) _copiesText.text = $"×{Mathf.Max(1, options.copies)}";
            }

            if (_levelBadge != null)
            {
                bool showLevel = options.level > 1; // ★1 / unupgraded is hidden to keep the grid clean
                _levelBadge.SetActive(showLevel);
                if (showLevel && _levelText != null) _levelText.text = $"★{options.level}"; // "★N"
            }

            ApplyThumbnail(cardId, options.dim);
            SetSelected(options.selectable && options.selected);
            SetLocked(options.showLock);

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        /// <summary>Toggle the selection outline + stepper visibility.</summary>
        public void SetSelected(bool selected)
        {
            Selected = selected;
            if (_selectionOutline != null) _selectionOutline.gameObject.SetActive(selected);
            UpdateStepper(selected);
        }

        private void SetLocked(bool locked)
        {
            if (_lockBadge != null) _lockBadge.SetActive(locked);
        }

        private void UpdateStepper(bool selected)
        {
            bool show = selected && _options.showStepper && _options.stepperMax > 1;
            if (_stepper != null) _stepper.SetActive(show);
            if (!show) return;
            RefreshQtyUi();
            _stepper.transform.SetAsLastSibling();
        }

        private void ChangeQuantity(int delta)
        {
            int max = Mathf.Max(1, _options.stepperMax);
            int next = Mathf.Clamp(_quantity + delta, 1, max);
            if (next == _quantity) return;
            _quantity = next;
            RefreshQtyUi();
            _onQuantityChanged?.Invoke(this, _quantity);
        }

        private void RefreshQtyUi()
        {
            int max = Mathf.Max(1, _options.stepperMax);
            if (_qtyText != null) _qtyText.text = _quantity.ToString();
            if (_minusBtn != null) _minusBtn.interactable = _quantity > 1;
            if (_plusBtn != null) _plusBtn.interactable = _quantity < max;
        }

        /// <summary>Apply the stat badges once the cell has been laid out (overlay rect is valid).
        /// Called by the grid after <c>Canvas.ForceUpdateCanvases()</c>.</summary>
        public void RefreshBadges()
        {
            if (string.IsNullOrEmpty(_cardId) || _overlay == null) return;

            if (_catalog == null) Core.ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
            ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(_cardId, out d);
            if (d == null) return;

            var dto = new BoardCardDto
            {
                cardId = _cardId,
                manaCost = d.manaCost,
                attack = d.attack,
                currentHealth = d.health,
                maxHealth = d.health,
                armor = d.armor,
                unitType = d.unitType
            };

            CardStatBadges.Apply(_overlay, dto, isBoard: false);

            // Keep chrome above the badges.
            if (_selectionOutline != null) _selectionOutline.transform.SetAsLastSibling();
            if (_lockBadge != null) _lockBadge.transform.SetAsLastSibling();
            if (_nameText != null) _nameText.transform.SetAsLastSibling();
            if (_copiesText != null && _copiesText.gameObject.activeSelf) _copiesText.transform.SetAsLastSibling();
            if (_levelBadge != null && _levelBadge.activeSelf) _levelBadge.transform.SetAsLastSibling();
            if (_button != null && _button.targetGraphic != null)
                _button.targetGraphic.transform.SetAsLastSibling();
            if (_stepper != null && _stepper.activeSelf) _stepper.transform.SetAsLastSibling();
        }

        private void ApplyThumbnail(string cardId, bool dim)
        {
            if (_artImage == null) return;

            if (_catalog == null) Core.ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
            ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(cardId, out d);

            // SAME compositor the in-game card uses (art + ornamental frame) — one source of truth.
            Sprite spr = null;
            if (d != null)
                spr = CardArtLibrary.GetCardComposite(cardId, d.cardType, d.cardRarity, d.cardFaction, d.unitType, d.armor > 0, "hand");
            if (spr == null || spr == CardArtLibrary.Missing)
                spr = CardArtLibrary.GetCardArt(cardId);

            if (spr != null && spr != CardArtLibrary.Missing)
            {
                _artImage.sprite = spr;
                _artImage.preserveAspect = false;
                _artImage.color = dim ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
            }
            else
            {
                _artImage.sprite = null;
                var c = FactionColors[Mathf.Clamp(d?.cardFaction ?? 0, 0, FactionColors.Length - 1)];
                _artImage.color = dim ? new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 1f) : c;
            }
        }

        private TextMeshProUGUI MakeText(string name, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private void OnDestroy()
        {
            if (_overlay != null) CardStatBadges.Release(_overlay);
        }
    }
}
