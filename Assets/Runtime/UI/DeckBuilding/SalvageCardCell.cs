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
    /// A single salvageable-card cell in the <see cref="SalvageScreen"/> grid. Built entirely in CODE
    /// (no prefab) so the screen needs zero serialized wiring. Mirrors the in-game card visual:
    /// framed composite art fills the (2:3) cell, <see cref="CardStatBadges"/> draws cost/attack/health/
    /// rarity over it, and the name + copies sit at the bottom. A selection outline highlights the cell
    /// when selected; a lock icon appears when the card is locked in a deck.
    ///
    /// GOTCHA (same as CraftingRecipeItem): the stat badges must be applied AFTER the grid lays the cell
    /// out — at <see cref="Bind"/> time the overlay rect is still 0 and the badges would collapse. The
    /// panel calls <see cref="RefreshBadges"/> after <c>Canvas.ForceUpdateCanvases()</c>.
    /// </summary>
    public sealed class SalvageCardCell : MonoBehaviour
    {
        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };

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
        private Image _artImage;
        private RectTransform _overlay;
        private Image _selectionOutline;
        private GameObject _lockBadge;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _copiesText;
        private Button _button;

        // Quantity stepper (− / count / +) — only shown for cards with ownedCopies > 1 while selected.
        private GameObject _stepper;
        private Button _minusBtn;
        private Button _plusBtn;
        private TextMeshProUGUI _qtyText;

        private SalvageApiClient.SalvageableCardDto _card;
        private Action<SalvageApiClient.SalvageableCardDto> _onTapped;
        private Action<SalvageApiClient.SalvageableCardDto, int> _onQuantityChanged;
        private int _quantity = 1;

        public RectTransform RectTransform => (RectTransform)transform;

        public static SalvageCardCell Create(Transform parent, CardCatalogCache catalog)
        {
            var go = new GameObject("SalvageCardCell", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var cell = go.AddComponent<SalvageCardCell>();
            cell._catalog = catalog;
            cell.BuildLayout();
            return cell;
        }

        private void BuildLayout()
        {
            // Cell background (used as the selection-tint base; mostly hidden behind the art).
            var bg = GetComponent<Image>();
            bg.color = new Color(0.16f, 0.17f, 0.20f, 0.6f);
            bg.raycastTarget = false;

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

            // Selection outline (a thin bright frame), hidden until selected.
            var selGo = new GameObject("SelectionOutline", typeof(RectTransform), typeof(Image));
            _selectionOutline = selGo.GetComponent<Image>();
            selGo.transform.SetParent(transform, false);
            KenneyUiSkin.Fill(_selectionOutline);
            _selectionOutline.color = SelectedOutline;
            _selectionOutline.raycastTarget = false;
            // Draw as a hollow frame via a sliced sprite if available; else a translucent tint.
            _selectionOutline.color = new Color(SelectedOutline.r, SelectedOutline.g, SelectedOutline.b, 0.28f);
            selGo.SetActive(false);

            // Lock badge (top-left), shown when locked in a deck.
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
            lockText.text = "🔒";
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

            // Copies badge (top-right) — how many copies are salvageable.
            _copiesText = MakeText("CopiesText", TextAlignmentOptions.TopRight);
            KenneyUiSkin.SetRect(_copiesText, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(110f, 44f));
            DeckBuilderTextScale.Apply(_copiesText, DeckBuilderTextScale.Role.Label);

            // Full-cell tap target. Built BEFORE the stepper so the stepper's own buttons sit on top
            // of it and consume their taps (otherwise the cell would toggle selection underneath).
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
            _button.onClick.AddListener(() => _onTapped?.Invoke(_card));

            BuildStepper();
        }

        // Compact − / count / + stepper pinned bottom-centre, just above the name. Hidden by default;
        // shown only for multi-copy cards while selected (see UpdateStepper).
        private void BuildStepper()
        {
            _stepper = new GameObject("QtyStepper", typeof(RectTransform), typeof(Image));
            var srt = (RectTransform)_stepper.transform;
            srt.SetParent(transform, false);
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 56f); // sits above the name row (name is at y=8, h=46)
            srt.sizeDelta = new Vector2(176f, 56f);
            var bg = _stepper.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            _minusBtn = MakeStepperButton("MinusBtn", "−");
            KenneyUiSkin.SetRect(_minusBtn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, 0f), new Vector2(52f, 52f));
            _minusBtn.onClick.AddListener(() => ChangeQuantity(-1));

            _plusBtn = MakeStepperButton("PlusBtn", "+");
            KenneyUiSkin.SetRect(_plusBtn, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f), new Vector2(52f, 52f));
            _plusBtn.onClick.AddListener(() => ChangeQuantity(+1));

            _qtyText = MakeText("QtyText", TextAlignmentOptions.Center);
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

        public void Bind(
            SalvageApiClient.SalvageableCardDto card,
            bool selected,
            int quantity,
            Action<SalvageApiClient.SalvageableCardDto> onTapped,
            Action<SalvageApiClient.SalvageableCardDto, int> onQuantityChanged)
        {
            _card = card;
            _onTapped = onTapped;
            _onQuantityChanged = onQuantityChanged;
            _quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, card.ownedCopies));

            if (_nameText != null) _nameText.text = card.displayName ?? card.cardId;
            if (_copiesText != null) _copiesText.text = $"×{Mathf.Max(1, card.ownedCopies)}";

            ApplyThumbnail(card);
            SetSelected(selected);
            SetLocked(card.lockedInDeck);

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        public void SetSelected(bool selected)
        {
            if (_selectionOutline != null) _selectionOutline.gameObject.SetActive(selected);
            UpdateStepper(selected);
        }

        /// <summary>Quantity this cell is currently showing (clamped to [1, ownedCopies]).</summary>
        public int Quantity => _quantity;

        // Shows the stepper only when the card is selected AND has more than one owned copy.
        private void UpdateStepper(bool selected)
        {
            bool show = selected && _card != null && _card.ownedCopies > 1;
            if (_stepper != null) _stepper.SetActive(show);
            if (!show) return;
            RefreshQtyUi();
            // Keep the stepper above the badges/tap target so its buttons receive taps.
            _stepper.transform.SetAsLastSibling();
        }

        private void ChangeQuantity(int delta)
        {
            if (_card == null) return;
            int max = Mathf.Max(1, _card.ownedCopies);
            int next = Mathf.Clamp(_quantity + delta, 1, max);
            if (next == _quantity) return;
            _quantity = next;
            RefreshQtyUi();
            _onQuantityChanged?.Invoke(_card, _quantity);
        }

        private void RefreshQtyUi()
        {
            int max = _card != null ? Mathf.Max(1, _card.ownedCopies) : 1;
            if (_qtyText != null) _qtyText.text = _quantity.ToString();
            if (_minusBtn != null) _minusBtn.interactable = _quantity > 1;
            if (_plusBtn != null) _plusBtn.interactable = _quantity < max;
        }

        private void SetLocked(bool locked)
        {
            if (_lockBadge != null) _lockBadge.SetActive(locked);
            // Dim locked cards so they read as not-salvageable.
            if (_artImage != null && _artImage.sprite != null)
                _artImage.color = locked ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
        }

        /// <summary>Apply the stat badges once the cell has been laid out (overlay rect is valid).</summary>
        public void RefreshBadges()
        {
            if (_card == null || _overlay == null) return;

            if (_catalog == null) Core.ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
            ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(_card.cardId, out d);
            if (d == null) return;

            var dto = new BoardCardDto
            {
                cardId = _card.cardId,
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
            if (_copiesText != null) _copiesText.transform.SetAsLastSibling();
            // Tap target above the badges...
            if (_button != null && _button.targetGraphic != null)
                _button.targetGraphic.transform.SetAsLastSibling();
            // ...and the stepper above the tap target (when shown) so its buttons receive taps.
            if (_stepper != null && _stepper.activeSelf) _stepper.transform.SetAsLastSibling();
        }

        private void ApplyThumbnail(SalvageApiClient.SalvageableCardDto card)
        {
            if (_artImage == null) return;

            if (_catalog == null) Core.ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
            ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(card.cardId, out d);

            Sprite spr = null;
            if (d != null)
                spr = CardArtLibrary.GetCardComposite(card.cardId, d.cardType, d.cardRarity, d.cardFaction, d.unitType, d.armor > 0, "hand");
            if (spr == null || spr == CardArtLibrary.Missing)
                spr = CardArtLibrary.GetCardArt(card.cardId);

            if (spr != null && spr != CardArtLibrary.Missing)
            {
                _artImage.sprite = spr;
                _artImage.preserveAspect = false;
                _artImage.color = Color.white;
            }
            else
            {
                _artImage.sprite = null;
                _artImage.color = FactionColors[Mathf.Clamp(card.cardFaction, 0, FactionColors.Length - 1)];
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
    }
}
