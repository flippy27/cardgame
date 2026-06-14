using System;
using System.Collections.Generic;
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
    /// Full-screen CARD CRAFT OVERVIEW. Opened when the player taps a craftable card in the
    /// crafting grid. Shows the big framed card (composite + stat badges, reusing the in-game
    /// system), its name + rarity, and the crafting REQUIREMENTS (each material: icon + name +
    /// have/need quantity, tinted by the material's rarity), plus a Craft button (enabled only
    /// when affordable) and a Close button. Crafting happens HERE, not from the grid cell.
    ///
    /// Built entirely in code (no prefab wiring) following the KenneyUiSkin "no prefab wiring"
    /// pattern used by the other deck-builder panels. The host (<see cref="CraftingPanel"/>)
    /// creates one instance and calls <see cref="Show"/> / wires <see cref="OnCraftConfirmed"/>.
    /// </summary>
    public sealed class CardCraftOverviewPanel : MonoBehaviour
    {
        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };

        // Same rarity palette as CardCollectionItem (Common grey, Rare blue, Epic purple, Legendary gold).
        private static readonly Color[] RarityColors =
        {
            new Color(0.65f, 0.65f, 0.65f), // Common    — grey
            new Color(0.20f, 0.50f, 1.00f), // Rare      — blue
            new Color(0.60f, 0.10f, 0.90f), // Epic      — purple
            new Color(1.00f, 0.78f, 0.10f), // Legendary — gold
        };

        // ---- UI (built in code) ----
        private RectTransform _root;        // full-screen overlay root
        private Image _cardArtImage;        // big framed composite
        private RectTransform _statsOverlay; // badge overlay covering the card
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _rarityText;
        private Transform _reqListContainer; // requirement rows live here
        private Button _craftButton;
        private TextMeshProUGUI _craftButtonText;
        private Button _closeButton;
        private TextMeshProUGUI _statusText;
        private GameObject _loadingOverlay;
        private bool _built;

        // ---- Data ----
        private CraftingApiClient.CraftableCardDto _card;
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _inventory = new();
        private CardCatalogCache _catalog;

        /// <summary>Fired when the player confirms a craft in the overview. The host (CraftingPanel)
        /// runs the actual craft path (the existing OnCraftRequested) and then calls
        /// <see cref="RefreshAfterCraft"/> or <see cref="Hide"/>.</summary>
        public event Action<string> OnCraftConfirmed; // cardId

        /// <summary>
        /// Show the overview for a craftable card. <paramref name="card"/> carries the requirements;
        /// <paramref name="inventory"/> is the owned-item map used to compute have/need + affordability.
        /// </summary>
        public void Show(
            CraftingApiClient.CraftableCardDto card,
            Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            _card = card;
            _inventory = inventory ?? new Dictionary<string, InventoryApiClient.PlayerItemDto>();

            gameObject.SetActive(true);
            EnsureBuilt();
            Rebuild();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Update the affordability/have-need display after the inventory changed (e.g. a
        /// successful craft that left the overview open). Pass the refreshed inventory.</summary>
        public void RefreshAfterCraft(Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            _inventory = inventory ?? _inventory;
            if (isActiveAndEnabled) Rebuild();
        }

        // ---- Build the UI once, in code ----
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var canvas = GetComponentInParent<Canvas>();
            var canvasRT = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;

            // Root full-screen overlay (this component's own RectTransform).
            _root = transform as RectTransform;
            if (_root == null)
            {
                // Defensive: if somehow attached to a non-UI transform, nothing to lay out.
                Debug.LogError("[CardCraftOverview] Root is not a RectTransform.");
                return;
            }
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            if (canvasRT != null)
            {
                // If the root is anchored to a tiny logical container, size to the canvas instead.
                _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
                _root.pivot = new Vector2(0.5f, 0.5f);
                _root.anchoredPosition = Vector2.zero;
                _root.sizeDelta = canvasRT.rect.size;
            }

            // Dimmer behind everything (blocks taps to the crafting grid underneath).
            var dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            var dimRT = (RectTransform)dim.transform;
            dimRT.SetParent(_root, false);
            KenneyUiSkin.Fill(dimRT);
            var dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            dimImg.raycastTarget = true;

            // Brown window panel backdrop.
            if (KenneyUiSkin.Available) KenneyUiSkin.EnsureWindowBackdrop(this);

            // ---- Big framed card (left/top region) ----
            var cardGo = new GameObject("BigCard", typeof(RectTransform), typeof(Image));
            var cardRT = (RectTransform)cardGo.transform;
            cardRT.SetParent(_root, false);
            // Left half, vertically centred, 2:3 card (~520x780). Anchored so it scales with the panel.
            cardRT.anchorMin = new Vector2(0f, 0.5f);
            cardRT.anchorMax = new Vector2(0f, 0.5f);
            cardRT.pivot = new Vector2(0f, 0.5f);
            cardRT.anchoredPosition = new Vector2(70f, 30f);
            cardRT.sizeDelta = new Vector2(520f, 780f);
            _cardArtImage = cardGo.GetComponent<Image>();
            _cardArtImage.preserveAspect = false; // 2:3 so badges land on the frame sockets
            _cardArtImage.raycastTarget = false;

            // Badge overlay exactly covering the card (laid out before Apply — see RefreshBadges gotcha).
            var overlayGo = new GameObject("StatsOverlay", typeof(RectTransform));
            _statsOverlay = (RectTransform)overlayGo.transform;
            _statsOverlay.SetParent(cardRT, false);
            KenneyUiSkin.Fill(_statsOverlay);

            // ---- Name + rarity (above the requirements, right column) ----
            _nameText = MakeText("NameText", _root, DeckBuilderTextScale.Role.Header, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_nameText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -70f), new Vector2(-720f, 90f));
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;

            _rarityText = MakeText("RarityText", _root, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_rarityText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -160f), new Vector2(-720f, 60f));

            // ---- "Requirements" header ----
            var reqHeader = MakeText("RequirementsHeader", _root, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.TopLeft);
            reqHeader.text = "Crafting Requirements";
            KenneyUiSkin.SetRect(reqHeader, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -240f), new Vector2(-720f, 56f));

            // ---- Requirements list (right column, scrollable-ish vertical list) ----
            var listGo = new GameObject("RequirementsList", typeof(RectTransform));
            var listRT = (RectTransform)listGo.transform;
            listRT.SetParent(_root, false);
            // Right column under the header, down to the action bar.
            listRT.anchorMin = new Vector2(0f, 0f);
            listRT.anchorMax = new Vector2(1f, 1f);
            listRT.pivot = new Vector2(0.5f, 1f);
            listRT.offsetMin = new Vector2(660f, 240f);   // left, bottom (clear of the taller action bar)
            listRT.offsetMax = new Vector2(-60f, -310f);  // right, top (under the header)
            _reqListContainer = listRT;
            KenneyUiSkin.EnsureVerticalList(_reqListContainer, spacing: 12f, padding: 8);

            // ---- Action bar: Craft (primary) + Close ----
            _craftButton = MakeButton("CraftButton", _root, KenneyUiSkin.ButtonStyle.Primary, "Craft", out _craftButtonText);
            KenneyUiSkin.SetRect(_craftButton, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(250f, 70f), new Vector2(440f, 132f));
            _craftButton.onClick.AddListener(OnCraftClicked);

            _closeButton = MakeButton("CloseButton", _root, KenneyUiSkin.ButtonStyle.Nav, "Close", out _);
            KenneyUiSkin.SetRect(_closeButton, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-250f, 70f), new Vector2(400f, 132f));
            _closeButton.onClick.AddListener(Hide);

            // Top-right X close as well (matches the other panels' chrome).
            var xClose = MakeButton("CloseX", _root, KenneyUiSkin.ButtonStyle.Icon, "X", out _);
            KenneyUiSkin.SetRect(xClose, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), new Vector2(140f, 120f));
            xClose.onClick.AddListener(Hide);

            // ---- Status + loading ----
            _statusText = MakeText("StatusText", _root, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_statusText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 224f), new Vector2(900f, 50f));

            _loadingOverlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(Image));
            var loRT = (RectTransform)_loadingOverlay.transform;
            loRT.SetParent(_root, false);
            KenneyUiSkin.Fill(loRT);
            _loadingOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            _loadingOverlay.SetActive(false);

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        private void Rebuild()
        {
            if (_card == null) return;

            // Name + rarity.
            if (_nameText != null) _nameText.text = _card.displayName ?? _card.cardId;
            if (_rarityText != null)
            {
                int idx = Mathf.Clamp(_card.cardRarity, 0, RarityNames.Length - 1);
                _rarityText.text = RarityNames[idx];
                _rarityText.color = RarityColors[Mathf.Clamp(_card.cardRarity, 0, RarityColors.Length - 1)];
            }

            ApplyBigCard(_card.cardId);

            // Requirements rows.
            BuildRequirementRows();

            // Affordability.
            ServiceLocator.TryResolve<InventoryService>(out var inv);
            bool canAfford = inv != null && inv.CanAffordCraft(_card.requirements, _inventory);
            if (_craftButton != null) _craftButton.interactable = canAfford;
            if (_craftButtonText != null) _craftButtonText.text = canAfford ? "Craft" : "Can't Afford";

            ShowStatus(string.Empty);
        }

        // Big framed composite + stat badges, reusing the in-game system. Mirrors
        // CraftingRecipeItem.ApplyThumbnail + ApplyStatBadges but at full overview size.
        private void ApplyBigCard(string cardId)
        {
            if (_catalog == null) ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
            ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(cardId, out d);

            // Composite art + ornamental frame (same compositor as the in-game card).
            if (_cardArtImage != null)
            {
                Sprite spr = null;
                if (d != null)
                    spr = CardArtLibrary.GetCardComposite(cardId, d.cardType, d.cardRarity, d.cardFaction, d.unitType, d.armor > 0, "hand");
                if (spr == null || spr == CardArtLibrary.Missing)
                    spr = CardArtLibrary.GetCardArt(cardId);
                if (spr != null && spr != CardArtLibrary.Missing)
                {
                    _cardArtImage.sprite = spr;
                    _cardArtImage.color = Color.white;
                }
                else
                {
                    _cardArtImage.sprite = null;
                    _cardArtImage.color = new Color(0.30f, 0.32f, 0.38f);
                }
            }

            // Stat badges over the overlay. The overlay rect must be valid before Apply, so force a
            // layout pass first (GOTCHA: otherwise the badges collapse to centre).
            if (_statsOverlay != null && d != null)
            {
                var dto = new BoardCardDto
                {
                    cardId = cardId,
                    manaCost = d.manaCost,
                    attack = d.attack,
                    currentHealth = d.health,
                    maxHealth = d.health,
                    armor = d.armor,
                    unitType = d.unitType
                };
                Canvas.ForceUpdateCanvases();
                CardStatBadges.Apply(_statsOverlay, dto, isBoard: false);
            }
        }

        private void BuildRequirementRows()
        {
            if (_reqListContainer == null) return;
            foreach (Transform child in _reqListContainer) Destroy(child.gameObject);

            var reqs = _card.requirements;
            if (reqs == null || reqs.Length == 0)
            {
                var row = MakeText("NoReqs", _reqListContainer, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.MidlineLeft);
                row.text = "No requirements.";
                KenneyUiSkin.EnsureRowHeight(row, 64f);
                return;
            }

            foreach (var req in reqs)
            {
                BuildOneRequirementRow(req);
            }

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        // One material row: [rarity-coloured icon block] [name] ........ [have/need], the whole row
        // tinted by the material's rarity.
        private void BuildOneRequirementRow(CraftingApiClient.CraftingRequirementDto req)
        {
            int have = 0;
            if (_inventory.TryGetValue(req.itemTypeKey, out var bal)) have = bal?.quantity ?? 0;
            int need = req.quantityRequired;
            bool met = have >= need;

            int rarity = ResolveMaterialRarity(req);
            Color rarityColor = RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];

            var rowGo = new GameObject("Req_" + (req.itemTypeKey ?? "?"), typeof(RectTransform), typeof(Image));
            var rowRT = (RectTransform)rowGo.transform;
            rowRT.SetParent(_reqListContainer, false);
            KenneyUiSkin.EnsureRowHeight(rowRT, 84f);
            var rowBg = rowGo.GetComponent<Image>();
            // Subtle rarity-tinted row background.
            rowBg.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.18f);
            rowBg.raycastTarget = false;

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 16, 8, 8);
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Material icon (best-effort sprite, else a rarity-coloured chip).
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rowRT, false);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSprite = ResolveMaterialIcon(req);
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = rarityColor;
            }
            KenneyUiSkin.SetLayoutElement(icon, preferredWidth: 64f, preferredHeight: 64f);

            // Material name, tinted by rarity.
            var nameText = MakeText("MatName", rowRT, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.MidlineLeft);
            nameText.text = req.itemTypeDisplayName ?? req.itemTypeKey ?? "Material";
            nameText.color = rarityColor;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            KenneyUiSkin.SetLayoutElement(nameText, preferredWidth: 280f, preferredHeight: 64f, flexibleWidth: 1f);

            // have/need quantity (green when met, red when short).
            var qtyText = MakeText("MatQty", rowRT, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.MidlineRight);
            qtyText.text = $"{have}/{need}";
            qtyText.color = met ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.95f, 0.35f, 0.35f);
            qtyText.enableWordWrapping = false;
            KenneyUiSkin.SetLayoutElement(qtyText, preferredWidth: 150f, preferredHeight: 64f);
        }

        // Material rarity: card_dust (and other generic crafting items) read as Common; if the material
        // key/id resolves to a card in the catalog (shard-style materials), use that card's rarity.
        // No DTO change — best-effort, defaults to Common (grey).
        private int ResolveMaterialRarity(CraftingApiClient.CraftingRequirementDto req)
        {
            if (req == null) return 0;
            if (_catalog == null) ServiceLocator.TryResolve<CardCatalogCache>(out _catalog);
            if (_catalog != null)
            {
                // Some materials are keyed by a cardId/cardDefinitionId (e.g. card shards).
                if (!string.IsNullOrEmpty(req.cardDefinitionId) &&
                    _catalog.TryGetCard(req.cardDefinitionId, out var byDef) && byDef != null)
                    return byDef.cardRarity;
                if (!string.IsNullOrEmpty(req.itemTypeKey) &&
                    _catalog.TryGetCard(req.itemTypeKey, out var byKey) && byKey != null)
                    return byKey.cardRarity;
            }
            return 0; // Common
        }

        // Best-effort material icon: per-item art under Resources/Art/items/{key}, else the card-dust
        // stat icon for dust, else null (caller draws a rarity chip).
        private Sprite ResolveMaterialIcon(CraftingApiClient.CraftingRequirementDto req)
        {
            if (req == null) return null;
            var key = req.itemTypeKey ?? string.Empty;
            var spr = Resources.Load<Sprite>($"Art/items/{key}");
            if (spr != null) return spr;
            if (key.ToLowerInvariant().Contains("dust"))
            {
                var dust = CardArtLibrary.GetStatIconSprite("stat_mana");
                if (dust != null) return dust;
            }
            return null;
        }

        private void OnCraftClicked()
        {
            if (_card == null) return;
            OnCraftConfirmed?.Invoke(_card.cardId);
        }

        // ---- Small UI factory helpers ----
        private TextMeshProUGUI MakeText(string goName, Transform parent, DeckBuilderTextScale.Role role, TextAlignmentOptions align)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            var t = go.AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.alignment = align;
            t.raycastTarget = false;
            DeckBuilderTextScale.Apply(t, role);
            return t;
        }

        private Button MakeButton(string goName, Transform parent, KenneyUiSkin.ButtonStyle style, string label, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var lblGo = new GameObject("Label", typeof(RectTransform));
            labelText = lblGo.AddComponent<TextMeshProUGUI>();
            var lrt = (RectTransform)lblGo.transform;
            lrt.SetParent(rt, false);
            KenneyUiSkin.Fill(labelText);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;
            DeckBuilderTextScale.Apply(labelText, DeckBuilderTextScale.Role.Button);

            var btn = go.GetComponent<Button>();
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButtonWithLabel(btn, style, label);
            else labelText.text = label;
            return btn;
        }

        private void ShowStatus(string msg) { if (_statusText != null) _statusText.text = msg; }
        public void SetLoading(bool on) { if (_loadingOverlay != null) _loadingOverlay.SetActive(on); }
    }
}
