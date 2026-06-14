using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI;
using Flippy.CardDuelMobile.Battle;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Single craftable card row inside the CraftingPanel scroll list.
    ///
    /// Prefab structure (CraftingRecipeItem_Prefab):
    ///   CraftingRecipeItem  (root — this component)
    ///   ├── CardNameText        (Text) — craftable card display name
    ///   ├── RarityText          (Text) — "Common" / "Rare" etc. (optional)
    ///   ├── CostContainer       (HorizontalLayoutGroup)
    ///   │   └── [CostChip_Prefab × N — one per requirement]
    ///   │       └── CostLabel   (Text) — "200 Card Dust"
    ///   ├── AffordabilityText   (Text) — "Available" / "Need 50 more Card Dust"
    ///   └── CraftButton         (Button)
    ///       └── CraftButtonText (Text)
    /// </summary>
    public sealed class CraftingRecipeItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [SerializeField] private Transform costContainer;
        [SerializeField] private GameObject costChipPrefab;
        [SerializeField] private TextMeshProUGUI affordabilityText;
        [SerializeField] private Button craftButton;
        [SerializeField] private TextMeshProUGUI craftButtonText;
        [SerializeField] private Image cardArtImage;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;
        [SerializeField] private string visualSurface = "collection";

        [SerializeField] private Color canAffordColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color cannotAffordColor = new Color(0.9f, 0.3f, 0.3f);

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };

        private CraftingApiClient.CraftableCardDto _card;
        private Action<string> _onCraft; // cardId

        /// <summary>
        /// Bind a craftable card entry to this row.
        /// </summary>
        public void Bind(
            CraftingApiClient.CraftableCardDto card,
            Dictionary<string, InventoryApiClient.PlayerItemDto> inventory,
            bool canAfford,
            Action<string> onCraft)
        {
            _card = card;
            _onCraft = onCraft;

            EnsureCellLayout();

            DeckBuilderTextScale.ApplyAutoSize(cardNameText, DeckBuilderTextScale.Role.CardName);
            DeckBuilderTextScale.Apply(rarityText, DeckBuilderTextScale.Role.Label);
            DeckBuilderTextScale.ApplyAutoSize(affordabilityText, DeckBuilderTextScale.Role.Status);
            DeckBuilderTextScale.Apply(craftButtonText, DeckBuilderTextScale.Role.Button);

            if (cardNameText != null)
                cardNameText.text = card.displayName ?? card.cardId;

            ApplyThumbnail(card.cardId);
            // Badges are applied via RefreshBadges() AFTER the grid lays out the cell — at Bind time the
            // overlay rect is still 0-height, which would collapse all badges to the vertical centre.

            if (rarityText != null)
            {
                var idx = Mathf.Clamp(card.cardRarity, 0, RarityNames.Length - 1);
                rarityText.text = RarityNames[idx];
            }

            BuildCostChips(card.requirements);

            // The cell no longer crafts directly — tapping it OPENS THE PER-CARD OVERVIEW (where the
            // requirements live and crafting is confirmed). So the button is ALWAYS interactable
            // regardless of affordability; the overview disables its own Craft button when too poor.
            if (craftButton != null)
            {
                craftButton.interactable = true;
                craftButton.onClick.RemoveAllListeners();
                craftButton.onClick.AddListener(OnCraftClicked);
                if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(craftButton, KenneyUiSkin.ButtonStyle.Nav);
            }

            if (craftButtonText != null)
                craftButtonText.text = "View";

            EnsureCellTapTarget();

            if (affordabilityText != null)
            {
                affordabilityText.color = canAfford ? canAffordColor : cannotAffordColor;
                affordabilityText.text = canAfford
                    ? "Available"
                    : BuildDeficitText(card.requirements, inventory);
            }

            // Spawned rows (incl. the cost chips just built) aren't reached by the panel-level
            // font pass, so apply the Kenney theme font here.
            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        // OVERHAUL: each craftable card is a GRID CELL. Art fills the top; rarity sits top-left and
        // affordability top-right; cost chips + name + a Craft button stack at the bottom. (The prefab
        // parked every element at the centre, so we re-anchor them into a card cell.)
        private void EnsureCellLayout()
        {
            // Remove any layout group from a previous build so absolute anchors take effect.
            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Destroy(hlg);
            var le = GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false; // grid drives the cell size

            // Clean card cell that mirrors the in-game card: the framed art FILLS the cell (cells are
            // authored 2:3 so the 2:3 composite fits with no distortion), CardStatBadges draw the
            // cost/attack/health/rarity over it, and the name + Craft sit at the very bottom. Crafting
            // REQUIREMENTS are NOT shown here (they live in the per-card overview).
            if (costContainer != null) costContainer.gameObject.SetActive(false);
            if (affordabilityText != null) affordabilityText.gameObject.SetActive(false);
            if (rarityText != null) rarityText.gameObject.SetActive(false); // rarity is shown by the badge

            // Card art fills the (2:3) cell — no preserveAspect so it lines up with the badge overlay.
            KenneyUiSkin.Fill(cardArtImage);
            if (cardArtImage != null) { cardArtImage.preserveAspect = false; cardArtImage.raycastTarget = false; }

            if (cardNameText != null)
            {
                KenneyUiSkin.SetRect(cardNameText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 50f), new Vector2(-8f, 44f));
                cardNameText.alignment = TextAlignmentOptions.Center;
                cardNameText.raycastTarget = false;
                cardNameText.enableWordWrapping = false;
                cardNameText.overflowMode = TextOverflowModes.Ellipsis;
                cardNameText.transform.SetAsLastSibling();
            }

            if (craftButton != null)
            {
                KenneyUiSkin.SetRect(craftButton, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 6f), new Vector2(-8f, 42f));
                craftButton.transform.SetAsLastSibling();
            }

            // Hide stray direct children (e.g. an unused default "Text (TMP)").
            var known = new HashSet<Transform>();
            foreach (var c in new Component[] { cardArtImage, cardNameText, rarityText, costContainer, affordabilityText, craftButton })
                if (c != null) known.Add(c.transform);
            foreach (Transform child in transform)
                if (!known.Contains(child) && child.name != "StatsOverlay" && child.name != "CellTapTarget") child.gameObject.SetActive(false);

            // Create the badge overlay NOW (covering the card) so it's part of the layout pass and has a
            // valid rect when RefreshBadges() applies the badges. Drawn under the name/craft chrome.
            var overlay = transform.Find("StatsOverlay") as RectTransform;
            if (overlay == null)
            {
                var go = new GameObject("StatsOverlay", typeof(RectTransform));
                overlay = (RectTransform)go.transform;
                overlay.SetParent(transform, false);
            }
            KenneyUiSkin.Fill(overlay);
            if (cardArtImage != null) overlay.SetSiblingIndex(cardArtImage.transform.GetSiblingIndex() + 1);
        }

        private void BuildCostChips(CraftingApiClient.CraftingRequirementDto[] requirements)
        {
            if (costContainer == null) return;
            // Lay cost chips out in a row with gaps (no overlap) even if the prefab lacks a layout group.
            KenneyUiSkin.EnsureHorizontalStrip(costContainer, spacing: 8f);
            foreach (Transform child in costContainer) Destroy(child.gameObject);
            if (requirements == null || costChipPrefab == null) return;

            foreach (var req in requirements)
            {
                var chip = Instantiate(costChipPrefab, costContainer);
                KenneyUiSkin.SetLayoutElement(chip.transform, preferredWidth: 84f, preferredHeight: 40f);
                // Dark pill so the white number reads (the prefab's default fill was a loud teal).
                var chipImg = chip.GetComponent<Image>();
                if (chipImg != null) chipImg.color = new Color(0.12f, 0.13f, 0.16f, 0.92f);
                // CostChip_Prefab's label is a TextMeshProUGUI — the old GetComponentInChildren<Text>()
                // looked for legacy uGUI Text and returned null, so chips showed no number.
                var label = chip.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    KenneyUiSkin.Fill(label, 6f, 6f, 2f, 2f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = Color.white;
                    label.enableWordWrapping = false;
                    DeckBuilderTextScale.ApplyAutoSize(label, DeckBuilderTextScale.Role.Status);
                    // Compact: quantity + a diamond for dust (the usual currency); other items keep a short tag.
                    bool isDust = (req.itemTypeKey ?? string.Empty).ToLowerInvariant().Contains("dust");
                    label.text = isDust ? $"{req.quantityRequired}◆" : $"{req.quantityRequired}";
                }
            }
        }

        private string BuildDeficitText(
            CraftingApiClient.CraftingRequirementDto[] requirements,
            Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            if (requirements == null) return "No requirements defined";

            foreach (var req in requirements)
            {
                inventory.TryGetValue(req.itemTypeKey, out var bal);
                int deficit = req.quantityRequired - (bal?.quantity ?? 0);
                if (deficit > 0)
                    return $"Need {deficit} more {req.itemTypeDisplayName ?? req.itemTypeKey}";
            }
            return "Missing items";
        }

        private void EnsureVisualRenderer()
        {
            if (visualRenderer == null)
            {
                visualRenderer = GetComponent<CardSurfaceVisualRenderer>() ?? GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            }

            if (visualRenderer == null && cardArtImage != null)
            {
                visualRenderer = gameObject.AddComponent<CardSurfaceVisualRenderer>();
                visualRenderer.EnsureDefaultImageBinding(cardArtImage, visualSurface);
            }
        }

        /// <summary>Apply the stat badges now that the cell has been laid out (overlay rect is valid).
        /// Called by the panel after Canvas.ForceUpdateCanvases() following a rebuild.</summary>
        public void RefreshBadges()
        {
            if (_card != null) ApplyStatBadges(_card.cardId);
        }

        // Card stat circles/icons/numbers (cost/attack/health/rarity), reusing the SAME in-game system
        // (CardStatBadges) over an overlay that covers the framed card, so they line up with the frame
        // sockets. The card data is built from the catalog (CraftableCardDto has no stats).
        private void ApplyStatBadges(string cardId)
        {
            Core.ServiceLocator.TryResolve<CardCatalogCache>(out var cat);
            ServerCardDefinition d = null;
            if (cat != null) cat.TryGetCard(cardId, out d);
            if (d == null) return;

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

            var overlay = transform.Find("StatsOverlay") as RectTransform;
            if (overlay == null)
            {
                var go = new GameObject("StatsOverlay", typeof(RectTransform));
                overlay = (RectTransform)go.transform;
                overlay.SetParent(transform, false);
            }
            KenneyUiSkin.Fill(overlay);
            // Draw badges UNDER the name/craft chrome.
            if (cardArtImage != null) overlay.SetSiblingIndex(cardArtImage.transform.GetSiblingIndex() + 1);
            CardStatBadges.Apply(overlay, dto, isBoard: false);
            if (cardNameText != null) cardNameText.transform.SetAsLastSibling();
            if (craftButton != null) craftButton.transform.SetAsLastSibling();
        }

        // Raw per-card illustration if present, else a neutral block (never white/magenta "missing").
        private void ApplyThumbnail(string cardId)
        {
            if (cardArtImage == null) return;
            cardArtImage.preserveAspect = true;
            Core.ServiceLocator.TryResolve<CardCatalogCache>(out var cat);
            ServerCardDefinition d = null;
            if (cat != null) cat.TryGetCard(cardId, out d);
            // Same compositor as the in-game card (art + ornamental frame).
            Sprite spr = null;
            if (d != null)
                spr = CardArtLibrary.GetCardComposite(cardId, d.cardType, d.cardRarity, d.cardFaction, d.unitType, d.armor > 0, "hand");
            if (spr == null || spr == CardArtLibrary.Missing)
                spr = CardArtLibrary.GetCardArt(cardId);
            if (spr != null && spr != CardArtLibrary.Missing)
            {
                cardArtImage.sprite = spr;
                cardArtImage.color = Color.white;
            }
            else
            {
                cardArtImage.sprite = null;
                cardArtImage.color = new Color(0.30f, 0.32f, 0.38f);
            }
        }

        // Transparent full-cell tap target so tapping ANYWHERE on the card (not just the button) opens
        // the overview. Sits just under the name/button chrome. Idempotent.
        private void EnsureCellTapTarget()
        {
            var existing = transform.Find("CellTapTarget");
            Button btn;
            if (existing != null)
            {
                btn = existing.GetComponent<Button>();
            }
            else
            {
                var go = new GameObject("CellTapTarget", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f); // invisible but raycastable
                img.raycastTarget = true;
                btn = go.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            KenneyUiSkin.Fill(btn);
            // Draw above the art/badges but BELOW the name + View button so those stay tappable/visible.
            int below = transform.childCount;
            if (cardNameText != null) below = Mathf.Min(below, cardNameText.transform.GetSiblingIndex());
            if (craftButton != null) below = Mathf.Min(below, craftButton.transform.GetSiblingIndex());
            btn.transform.SetSiblingIndex(Mathf.Max(0, below));
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnCraftClicked);
        }

        // Routes to the panel's OpenOverview (NOT a direct craft) — see CraftingPanel.Bind wiring.
        private void OnCraftClicked()
        {
            if (_card != null) _onCraft?.Invoke(_card.cardId);
        }
    }
}
