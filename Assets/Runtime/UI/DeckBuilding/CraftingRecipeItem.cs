using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI;
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

            if (cardNameText != null)
                cardNameText.text = card.displayName ?? card.cardId;

            EnsureVisualRenderer();
            visualRenderer?.ApplyCard(card.cardId, visualSurface);

            if (rarityText != null)
            {
                var idx = Mathf.Clamp(card.cardRarity, 0, RarityNames.Length - 1);
                rarityText.text = RarityNames[idx];
            }

            BuildCostChips(card.requirements);

            if (craftButton != null)
            {
                craftButton.interactable = canAfford;
                craftButton.onClick.RemoveAllListeners();
                craftButton.onClick.AddListener(OnCraftClicked);
            }

            if (craftButtonText != null)
                craftButtonText.text = canAfford ? "Craft" : "Can't Afford";

            if (affordabilityText != null)
            {
                affordabilityText.color = canAfford ? canAffordColor : cannotAffordColor;
                affordabilityText.text = canAfford
                    ? "Available"
                    : BuildDeficitText(card.requirements, inventory);
            }
        }

        private void BuildCostChips(CraftingApiClient.CraftingRequirementDto[] requirements)
        {
            if (costContainer == null) return;
            foreach (Transform child in costContainer) Destroy(child.gameObject);
            if (requirements == null || costChipPrefab == null) return;

            foreach (var req in requirements)
            {
                var chip = Instantiate(costChipPrefab, costContainer);
                var label = chip.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{req.quantityRequired} {req.itemTypeDisplayName ?? req.itemTypeKey}";
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

        private void OnCraftClicked() => _onCraft?.Invoke(_card.cardId);
    }
}
