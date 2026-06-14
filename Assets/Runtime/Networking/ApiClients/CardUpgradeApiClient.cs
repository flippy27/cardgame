using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    /// <summary>
    /// Per-card upgrade-tree / leveling endpoints (server-authoritative). Cards level 1→5; each level
    /// offers 0, 1 or 2 options the player picks from (stat change / add skill / upgrade skill) and has
    /// incremental material requirements. Applying a level consumes materials and bumps the owned card.
    ///
    ///   GET  /api/v1/card-upgrades/cards/{cardId}/tree                       → CardUpgradeTreeDto
    ///   GET  /api/v1/card-upgrades/player-cards/{playerCardId}/state         → PlayerCardUpgradeStateDto
    ///   POST /api/v1/card-upgrades/player-cards/{playerCardId}/apply-level   → ApplyCardLevelResponse
    ///
    /// Mirrors CardUpgradeDtos.cs on the server: camelCase JSON, enum-ish fields are plain ints
    /// (kind: 0=fixed, 1=stat, 2=add_skill, 3=upgrade_skill). JsonUtility-safe [Serializable] classes,
    /// public camelCase fields — NO nullable types (JsonUtility ignores them; a missing string is null,
    /// a missing int is 0). The server's nullable int (nextLevel) arrives as a number or is absent → 0;
    /// IsMaxLevel is the authoritative "no next level" flag, so we never key off nextLevel == 0.
    /// </summary>
    public sealed class CardUpgradeApiClient
    {
        private readonly string _baseUrl;

        public CardUpgradeApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/card-upgrades/cards/{cardId}/tree
        public async Task<CardUpgradeTreeDto> FetchTree(string cardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/card-upgrades/cards/{cardId}/tree");
            return JsonUtility.FromJson<CardUpgradeTreeDto>(json);
        }

        // GET /api/v1/card-upgrades/player-cards/{playerCardId}/state
        public async Task<PlayerCardUpgradeStateDto> FetchState(string playerCardId)
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/card-upgrades/player-cards/{playerCardId}/state");
            return JsonUtility.FromJson<PlayerCardUpgradeStateDto>(json);
        }

        // POST /api/v1/card-upgrades/player-cards/{playerCardId}/apply-level
        // chosenOptionId may be null for a no-choice (skipped) level. JsonUtility serializes a null
        // string field as "chosenOptionId":null, which the server's nullable string accepts.
        public async Task<ApplyCardLevelResponse> ApplyLevel(string playerCardId, string chosenOptionId)
        {
            var body = JsonUtility.ToJson(new ApplyCardLevelRequest { chosenOptionId = chosenOptionId });
            var json = await HttpClientHelper.PostAsync(
                $"{_baseUrl}/api/v1/card-upgrades/player-cards/{playerCardId}/apply-level", body);
            return JsonUtility.FromJson<ApplyCardLevelResponse>(json);
        }

        // ---- DTOs (mirror CardUpgradeDtos.cs; camelCase, int enums) ----

        [Serializable]
        public sealed class CardUpgradeOptionDto
        {
            public string id;
            public int level;
            public int optionIndex;
            public int kind;              // 0=fixed, 1=stat, 2=add_skill, 3=upgrade_skill
            public string displayName;
            public string description;
            public int attackDelta;       // may be negative
            public int healthDelta;
            public int armorDelta;
            public string addAbilityId;
            public string upgradeAbilityId;
        }

        [Serializable]
        public sealed class CardUpgradeRequirementDto
        {
            public string id;
            public int level;
            public int itemTypeId;
            public string itemTypeKey;            // "card_dust" — used for the Resources/Art/items/{key} icon
            public string itemTypeDisplayName;
            public int itemTypeRarity;
            public string itemTypeCategory;
            public string itemTypeIconAssetRef;
            public int quantityRequired;
        }

        [Serializable]
        public sealed class CardUpgradeLevelDto
        {
            public int level;
            public CardUpgradeOptionDto[] options;
            public CardUpgradeRequirementDto[] requirements;
        }

        [Serializable]
        public sealed class CardUpgradeTreeDto
        {
            public string cardId;
            public string displayName;
            public int cardRarity;
            public int cardFaction;
            public int cardType;
            public int maxLevel;
            public CardUpgradeLevelDto[] levels;
        }

        [Serializable]
        public sealed class PlayerCardChoiceDto
        {
            public string id;
            public int level;
            public string chosenOptionId;
            public int kind;
            public string displayName;
        }

        [Serializable]
        public sealed class DerivedSkillLevelDto
        {
            public string abilityId;
            public int level;
            public bool added;
        }

        [Serializable]
        public sealed class PlayerCardUpgradeStateDto
        {
            public string playerCardId;
            public string cardId;
            public string displayName;
            public int cardRarity;
            public int currentLevel;
            public int maxLevel;
            public bool isMaxLevel;
            public int baseAttack;
            public int baseHealth;
            public int baseArmor;
            public int appliedAttackDelta;
            public int appliedHealthDelta;
            public int appliedArmorDelta;
            public int effectiveAttack;
            public int effectiveHealth;
            public int effectiveArmor;
            public PlayerCardChoiceDto[] choices;
            public DerivedSkillLevelDto[] skillLevels;
            public int nextLevel;                 // 0/absent when maxed — trust isMaxLevel instead
            public bool nextLevelAffordable;
            public CardUpgradeOptionDto[] nextLevelOptions;
            public CardUpgradeRequirementDto[] nextLevelRequirements;
        }

        [Serializable]
        public sealed class ApplyCardLevelRequest
        {
            public string chosenOptionId;         // null only when the next level has no options
        }

        [Serializable]
        public sealed class ApplyCardLevelResponse
        {
            public bool success;
            public string message;
            public string errorCode;              // null on success
            public PlayerCardUpgradeStateDto state;
            // Embedded from InventoryApiClient — same namespace, no extra using needed.
            public InventoryApiClient.PlayerItemDto[] updatedInventory;
            public long newXpTotal;
        }
    }
}
