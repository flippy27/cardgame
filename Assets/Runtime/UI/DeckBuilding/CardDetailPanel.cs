using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Shows the server-owned details for one player-card instance.
    /// Upgrade options are intentionally not computed on the client anymore.
    /// </summary>
    public sealed class CardDetailPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Button closeButton;

        [Header("Visuals")]
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;
        [SerializeField] private Image cardArtImage;
        [SerializeField] private string visualSurface = "detail";

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [SerializeField] private TextMeshProUGUI factionText;

        [Header("Upgrade History")]
        [SerializeField] private Transform upgradeHistoryContainer;
        [SerializeField] private GameObject upgradeHistoryRowPrefab;

        [Header("Upgrade Options")]
        [SerializeField] private Transform upgradeOptionsContainer;
        [SerializeField] private GameObject upgradeOptionItemPrefab;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };
        private static readonly string[] FactionNames = { "Ember", "Tidal", "Grove", "Alloy", "Void" };

        private PlayerCardCollectionService _collectionService;

        public event Action OnUpgradeSuccess;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

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

        public void Show(string playerCardId)
        {
            gameObject.SetActive(true);
            LoadDataAsync(playerCardId);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private async void LoadDataAsync(string playerCardId)
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ServiceLocator.TryResolve(out _collectionService);
            if (_collectionService == null)
            {
                ShowStatus("Collection service unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var card = await _collectionService.GetCardDetailAsync(playerCardId);
                if (card == null)
                {
                    ShowStatus("Card not found.");
                    return;
                }

                BindCard(card);
                BuildUpgradeHistory(card.upgrades);
                BuildUpgradeOptionsPlaceholder();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CardDetail] {ex}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void BindCard(PlayerCardsApiClient.PlayerCardDetailDto card)
        {
            if (cardNameText != null) cardNameText.text = card.displayName ?? card.cardId;
            if (levelText != null) levelText.text = $"Level {card.level}";

            if (attackText != null)
            {
                attackText.text = card.effectiveAttack != card.baseAttack
                    ? $"ATK: {card.baseAttack} -> {card.effectiveAttack}"
                    : $"ATK: {card.baseAttack}";
            }

            if (healthText != null)
            {
                healthText.text = card.effectiveHealth != card.baseHealth
                    ? $"HP: {card.baseHealth} -> {card.effectiveHealth}"
                    : $"HP: {card.baseHealth}";
            }

            if (armorText != null)
            {
                armorText.text = card.effectiveArmor != card.baseArmor
                    ? $"ARM: {card.baseArmor} -> {card.effectiveArmor}"
                    : $"ARM: {card.baseArmor}";
            }

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

            visualRenderer?.ApplyCard(card.cardId, visualSurface);
        }

        private void BuildUpgradeHistory(PlayerCardsApiClient.PlayerCardUpgradeDto[] upgrades)
        {
            ClearChildren(upgradeHistoryContainer);

            if (upgrades == null || upgrades.Length == 0)
            {
                SpawnTextRow(upgradeHistoryContainer, upgradeHistoryRowPrefab, "No upgrades applied yet.");
                return;
            }

            foreach (var upgrade in upgrades)
            {
                SpawnTextRow(upgradeHistoryContainer, upgradeHistoryRowPrefab, FormatUpgradeLabel(upgrade));
            }
        }

        private void BuildUpgradeOptionsPlaceholder()
        {
            ClearChildren(upgradeOptionsContainer);

            if (upgradeOptionItemPrefab != null && upgradeOptionsContainer != null)
            {
                var go = Instantiate(upgradeOptionItemPrefab, upgradeOptionsContainer);
                var item = go.GetComponent<UpgradeOptionItem>();
                if (item != null)
                {
                    item.BindUnavailable(
                        "Server upgrade options pending",
                        "The client no longer computes upgrade costs. Add a backend endpoint that returns available upgrades and atomic costs.");
                    return;
                }
            }

            SpawnTextRow(
                upgradeOptionsContainer,
                upgradeHistoryRowPrefab,
                "Upgrade options require a backend endpoint. No client-side UpgradeConfig is used.");
        }

        private static string FormatUpgradeLabel(PlayerCardsApiClient.PlayerCardUpgradeDto upgrade)
        {
            if (upgrade == null)
            {
                return "Unknown upgrade";
            }

            return upgrade.upgradeKind switch
            {
                "attack_bonus" => $"+{upgrade.intValue} ATK",
                "health_bonus" => $"+{upgrade.intValue} HP",
                "armor_bonus" => $"+{upgrade.intValue} ARM",
                "level_up" => "Level Up",
                "added_ability" => $"Ability: {upgrade.stringValue}",
                _ => $"{upgrade.upgradeKind}" + (upgrade.intValue != 0 ? $" +{upgrade.intValue}" : string.Empty)
            };
        }

        private static void SpawnTextRow(Transform container, GameObject prefab, string text)
        {
            if (container == null || prefab == null)
            {
                return;
            }

            var go = Instantiate(prefab, container);
            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = text;
                return;
            }

            var legacyText = go.GetComponent<Text>() ?? go.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = text;
            }
        }

        private static void ClearChildren(Transform container)
        {
            if (container == null)
            {
                return;
            }

            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private void ShowStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void SetLoading(bool active)
        {
            if (loadingOverlay != null)
            {
                loadingOverlay.SetActive(active);
            }
        }
    }
}
