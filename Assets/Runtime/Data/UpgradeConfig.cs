using System;
using UnityEngine;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// ScriptableObject defining available upgrade types and their item costs.
    /// Since the API has no /crafting/upgrades endpoint (per current contract),
    /// upgrade options are defined client-side here.
    ///
    /// Create: Assets → Create → CardDuel → UpgradeConfig
    /// Place at: Assets/Resources/UpgradeConfig.asset  (or assign via serialized field)
    ///
    /// Known item type keys (from contract §1):
    ///   card_dust, arcane_shard, essence_of_void,
    ///   faction_ember, faction_tidal, faction_grove, faction_alloy, faction_void,
    ///   upgrade_stone, ability_tome
    ///
    /// Known upgrade kinds (from contract §4):
    ///   attack_bonus, health_bonus, armor_bonus, level_up, added_ability, custom_tag
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeConfig", menuName = "CardDuel/UpgradeConfig")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        [Serializable]
        public sealed class UpgradeOption
        {
            public string upgradeKind;      // "attack_bonus"
            public string displayName;      // "+2 ATK"
            public string description;      // Shown in UI tooltip
            public int intValue;            // e.g. 2 for attack_bonus
            public string stringValue;      // ability_id for added_ability, empty otherwise
            public UpgradeCostEntry[] costs;
        }

        [Serializable]
        public sealed class UpgradeCostEntry
        {
            public string itemTypeKey;      // "upgrade_stone"
            public int quantity;            // 1
            public string displayName;      // "Upgrade Stone" — shown in UI
        }

        public UpgradeOption[] upgradeOptions;

        /// <summary>Returns all options valid for a given card rarity (0=Common..3=Legendary).</summary>
        public UpgradeOption[] GetOptionsForRarity(int cardRarity)
        {
            // Extend this filter logic as needed (e.g. hide essence_of_void for Commons).
            return upgradeOptions;
        }

        /// <summary>Converts UpgradeCostEntry[] to the tuple format InventoryService.CanAfford expects.</summary>
        public static (string itemTypeKey, int quantity)[] ToCostTuples(UpgradeCostEntry[] costs)
        {
            if (costs == null) return Array.Empty<(string, int)>();
            var result = new (string, int)[costs.Length];
            for (int i = 0; i < costs.Length; i++)
                result[i] = (costs[i].itemTypeKey, costs[i].quantity);
            return result;
        }
    }
}
