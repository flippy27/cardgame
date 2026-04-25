using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Card detail and upgrade panel. Opened when the player taps a card in the collection.
    ///
    /// Shows:
    ///   - Card stats (base vs effective after upgrades)
    ///   - Applied upgrades history
    ///   - Available upgrade options (from UpgradeConfig) with costs + affordability
    ///   - Apply upgrade button (consumes items server-side note: non-atomic, see PlayerCardCollectionService)
    ///
    /// Hierarchy (CardDetailPanel — starts inactive):
    ///   CardDetailPanel
    ///   ├── Header
    ///   │   ├── CardNameText        (Text)
    ///   │   ├── LevelText           (Text) — "Level 2"
    ///   │   └── CloseButton         (Button)
    ///   ├── StatsSection
    ///   │   ├── AttackText          (Text) — "ATK: 3 → 5"
    ///   │   ├── HealthText          (Text) — "HP: 4"
    ///   │   ├── ArmorText           (Text) — "ARM: 0 → 1"
    ///   │   ├── RarityText          (Text)
    ///   │   └── FactionText         (Text)
    ///   ├── UpgradesAppliedSection
    ///   │   ├── SectionTitle        (Text) — "Upgrades Applied"
    ///   │   └── UpgradeHistoryContainer (VerticalLayoutGroup — spawned Text rows)
    ///   ├── UpgradeOptionsSection
    ///   │   ├── SectionTitle        (Text) — "Available Upgrades"
    ///   │   └── UpgradeOptionsContainer (VerticalLayoutGroup)
    ///   │       └── [UpgradeOptionItem × N — from UpgradeConfig]
    ///   ├── StatusText              (Text)
    ///   └── LoadingOverlay          (GameObject)
    /// </summary>
    public sealed class CardDetailPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private Text cardNameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Button closeButton;

        [Header("Stats")]
        [SerializeField] private Text attackText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text armorText;
        [SerializeField] private Text rarityText;
        [SerializeField] private Text factionText;

        [Header("Upgrade History")]
        [SerializeField] private Transform upgradeHistoryContainer;
        [SerializeField] private GameObject upgradeHistoryRowPrefab; // simple Text prefab

        [Header("Upgrade Options")]
        [SerializeField] private Transform upgradeOptionsContainer;
        [SerializeField] private GameObject upgradeOptionItemPrefab;
        [SerializeField] private UpgradeConfig upgradeConfig;

        [Header("Feedback")]
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject loadingOverlay;

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };
        private static readonly string[] FactionNames = { "Ember", "Tidal", "Grove", "Alloy", "Void" };

        private PlayerCardCollectionService _collectionService;
        private InventoryService _inventoryService;

        private PlayerCardsApiClient.PlayerCardDetailDto _currentCard;
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _inventory = new();

        /// <summary>Fired after successful upgrade — CardCollectionScreen subscribes.</summary>
        public event Action OnUpgradeSuccess;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        /// <summary>Opens the panel for a specific owned card instance by UUID.</summary>
        public void Show(string playerCardId)
        {
            gameObject.SetActive(true);
            LoadDataAsync(playerCardId);
        }

        public void Hide() => gameObject.SetActive(false);

        private async void LoadDataAsync(string playerCardId)
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);
            ServiceLocator.TryResolve<InventoryService>(out _inventoryService);

            if (_collectionService == null || _inventoryService == null)
            {
                ShowStatus("Services unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var detailTask = _collectionService.GetCardDetailAsync(playerCardId);
                var invTask = _inventoryService.GetInventoryAsync();
                await Task.WhenAll(detailTask, invTask);

                _currentCard = detailTask.Result;
                _inventory = invTask.Result;

                if (_currentCard == null)
                {
                    ShowStatus("Card not found.");
                    SetLoading(false);
                    return;
                }

                BindCard(_currentCard);
                BuildUpgradeHistory(_currentCard.upgrades);
                BuildUpgradeOptions(_currentCard);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CardDetail] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void BindCard(PlayerCardsApiClient.PlayerCardDetailDto card)
        {
            if (cardNameText != null) cardNameText.text = card.displayName ?? card.cardId;
            if (levelText != null) levelText.text = $"Level {card.level}";

            if (attackText != null)
                attackText.text = card.effectiveAttack != card.baseAttack
                    ? $"ATK: {card.baseAttack} → {card.effectiveAttack}"
                    : $"ATK: {card.baseAttack}";

            if (healthText != null)
                healthText.text = card.effectiveHealth != card.baseHealth
                    ? $"HP: {card.baseHealth} → {card.effectiveHealth}"
                    : $"HP: {card.baseHealth}";

            if (armorText != null)
                armorText.text = card.effectiveArmor != card.baseArmor
                    ? $"ARM: {card.baseArmor} → {card.effectiveArmor}"
                    : $"ARM: {card.baseArmor}";

            if (rarityText != null)
            {
                var idx = Mathf.Clamp(card.cardRarity, 0, RarityNames.Length - 1);
                rarityText.text = RarityNames[idx];
            }

            if (factionText != null)
            {
                var idx = Mathf.Clamp(card.cardFaction, 0, FactionNames.Length - 1);
                factionText.text = FactionNames[idx];
            }
        }

        private void BuildUpgradeHistory(PlayerCardsApiClient.PlayerCardUpgradeDto[] upgrades)
        {
            if (upgradeHistoryContainer == null) return;
            foreach (Transform child in upgradeHistoryContainer) Destroy(child.gameObject);

            if (upgrades == null || upgrades.Length == 0)
            {
                SpawnHistoryRow("No upgrades applied yet.");
                return;
            }

            foreach (var u in upgrades)
            {
                string label = FormatUpgradeLabel(u);
                SpawnHistoryRow(label);
            }
        }

        private void SpawnHistoryRow(string text)
        {
            if (upgradeHistoryRowPrefab == null) return;
            var go = Instantiate(upgradeHistoryRowPrefab, upgradeHistoryContainer);
            var t = go.GetComponent<Text>() ?? go.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        private string FormatUpgradeLabel(PlayerCardsApiClient.PlayerCardUpgradeDto u)
        {
            return u.upgradeKind switch
            {
                "attack_bonus" => $"+{u.intValue} ATK",
                "health_bonus" => $"+{u.intValue} HP",
                "armor_bonus"  => $"+{u.intValue} ARM",
                "level_up"     => "Level Up",
                "added_ability" => $"Ability: {u.stringValue}",
                _ => $"{u.upgradeKind}" + (u.intValue != 0 ? $" +{u.intValue}" : string.Empty)
            };
        }

        private void BuildUpgradeOptions(PlayerCardsApiClient.PlayerCardDetailDto card)
        {
            if (upgradeOptionsContainer == null || upgradeOptionItemPrefab == null) return;
            foreach (Transform child in upgradeOptionsContainer) Destroy(child.gameObject);

            if (upgradeConfig == null)
            {
                ShowStatus("UpgradeConfig not assigned.");
                return;
            }

            var options = upgradeConfig.GetOptionsForRarity(card.cardRarity);
            foreach (var option in options)
            {
                var go = Instantiate(upgradeOptionItemPrefab, upgradeOptionsContainer);
                var item = go.GetComponent<UpgradeOptionItem>();
                if (item == null) continue;

                var costs = UpgradeConfig.ToCostTuples(option.costs);
                bool canAfford = _inventoryService.CanAfford(costs, _inventory);
                item.Bind(option, _inventory, canAfford, OnUpgradeRequested);
            }
        }

        private async void OnUpgradeRequested(UpgradeConfig.UpgradeOption option)
        {
            if (_collectionService == null || _currentCard == null) return;

            SetLoading(true);
            ShowStatus("Applying upgrade...");

            var upgradeRequest = new PlayerCardsApiClient.ApplyUpgradeRequestDto
            {
                upgradeKind = option.upgradeKind,
                intValue = option.intValue,
                stringValue = option.stringValue,
                appliedBy = "upgrade_system",
                note = option.displayName
            };

            var costs = UpgradeConfig.ToCostTuples(option.costs);

            var (success, message, updatedCard) = await _collectionService.ApplyUpgradeAsync(
                _currentCard.id,
                upgradeRequest,
                _inventoryService,
                costs);

            if (success && updatedCard != null)
            {
                _currentCard = updatedCard;
                _inventory = await _inventoryService.GetInventoryAsync();
                BindCard(_currentCard);
                BuildUpgradeHistory(_currentCard.upgrades);
                BuildUpgradeOptions(_currentCard);
                ShowStatus("Upgrade applied!");
                OnUpgradeSuccess?.Invoke();
            }
            else
            {
                ShowStatus(message);
            }

            SetLoading(false);
        }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
