using System;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Single upgrade option row inside CardDetailPanel.
    ///
    /// Prefab structure (UpgradeOptionItem_Prefab):
    ///   UpgradeOptionItem   (root — this component)
    ///   ├── UpgradeNameText     (Text)  — "+2 ATK"
    ///   ├── DescriptionText     (Text)  — description (optional)
    ///   ├── CostContainer       (HorizontalLayoutGroup)
    ///   │   └── [CostChip_Prefab × N per cost entry]
    ///   │       └── CostLabel   (Text)  — "1 Upgrade Stone"
    ///   ├── AffordabilityText   (Text)  — "Available" / "Need X more ..."
    ///   └── ApplyButton         (Button)
    ///       └── ApplyButtonText (Text)
    /// </summary>
    public sealed class UpgradeOptionItem : MonoBehaviour
    {
        [SerializeField] private Text upgradeNameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Transform costContainer;
        [SerializeField] private GameObject costChipPrefab;
        [SerializeField] private Text affordabilityText;
        [SerializeField] private Button applyButton;
        [SerializeField] private Text applyButtonText;

        [SerializeField] private Color canAffordColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color cannotAffordColor = new Color(0.9f, 0.3f, 0.3f);

        private UpgradeConfig.UpgradeOption _option;
        private Action<UpgradeConfig.UpgradeOption> _onApply;

        public void Bind(
            UpgradeConfig.UpgradeOption option,
            System.Collections.Generic.Dictionary<string, InventoryApiClient.PlayerItemDto> inventory,
            bool canAfford,
            Action<UpgradeConfig.UpgradeOption> onApply)
        {
            _option = option;
            _onApply = onApply;

            if (upgradeNameText != null) upgradeNameText.text = option.displayName;
            if (descriptionText != null) descriptionText.text = option.description;

            BuildCostChips(option.costs);

            if (applyButton != null)
            {
                applyButton.interactable = canAfford;
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (applyButtonText != null)
                applyButtonText.text = canAfford ? "Apply" : "Can't Afford";

            if (affordabilityText != null)
            {
                affordabilityText.color = canAfford ? canAffordColor : cannotAffordColor;
                affordabilityText.text = canAfford ? "Available" : BuildDeficitText(option.costs, inventory);
            }
        }

        private void BuildCostChips(UpgradeConfig.UpgradeCostEntry[] costs)
        {
            if (costContainer == null) return;
            foreach (Transform child in costContainer) Destroy(child.gameObject);
            if (costs == null || costChipPrefab == null) return;

            foreach (var cost in costs)
            {
                var chip = Instantiate(costChipPrefab, costContainer);
                var label = chip.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{cost.quantity} {cost.displayName ?? cost.itemTypeKey}";
            }
        }

        private string BuildDeficitText(
            UpgradeConfig.UpgradeCostEntry[] costs,
            System.Collections.Generic.Dictionary<string, InventoryApiClient.PlayerItemDto> inventory)
        {
            if (costs == null) return string.Empty;
            foreach (var cost in costs)
            {
                inventory.TryGetValue(cost.itemTypeKey, out var bal);
                int deficit = cost.quantity - (bal?.quantity ?? 0);
                if (deficit > 0)
                    return $"Need {deficit} more {cost.displayName ?? cost.itemTypeKey}";
            }
            return "Missing items";
        }

        private void OnApplyClicked() => _onApply?.Invoke(_option);
    }
}
