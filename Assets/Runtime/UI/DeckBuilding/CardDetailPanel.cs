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
    [Serializable]
    public sealed class GenericUpgradePreset
    {
        public string title = "Attack Bonus (+1)";
        [TextArea] public string description = "Apply this upgrade through the server.";
        public string upgradeKind = "attack_bonus";
        public int intValue = 1;
        public string stringValue = string.Empty;
        public string appliedBy = "player";
        [TextArea] public string note = string.Empty;
        public bool requiresStringValue;
    }

    /// <summary>
    /// Shows the server-owned details for one player-card instance.
    /// Upgrade options are editable request presets. The server remains authoritative.
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
        [SerializeField] private GenericUpgradePreset[] upgradePresets;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };
        private static readonly string[] FactionNames = { "Ember", "Tidal", "Grove", "Alloy", "Void" };

        private PlayerCardCollectionService _collectionService;

        public event Action OnUpgradeSuccess;

        private void Awake()
        {
            EnsureDefaultUpgradePresets();

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

        private void OnValidate()
        {
            EnsureDefaultUpgradePresets();
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
                BuildUpgradeOptions(playerCardId);
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

        private void BuildUpgradeOptions(string playerCardId)
        {
            ClearChildren(upgradeOptionsContainer);

            if (upgradeOptionItemPrefab == null || upgradeOptionsContainer == null) return;

            if (upgradePresets == null || upgradePresets.Length == 0)
            {
                SpawnTextRow(upgradeOptionsContainer, upgradeOptionItemPrefab, "No upgrade presets configured.");
                return;
            }

            foreach (var preset in upgradePresets)
            {
                if (preset == null)
                {
                    continue;
                }

                var go = Instantiate(upgradeOptionItemPrefab, upgradeOptionsContainer);
                var item = go.GetComponent<UpgradeOptionItem>();
                if (item == null) continue;

                var capturedPreset = preset;
                var capturedId = playerCardId;
                var canApply = !string.IsNullOrWhiteSpace(capturedPreset.upgradeKind) &&
                               (!capturedPreset.requiresStringValue || !string.IsNullOrWhiteSpace(capturedPreset.stringValue));
                var title = string.IsNullOrWhiteSpace(capturedPreset.title)
                    ? capturedPreset.upgradeKind
                    : capturedPreset.title;

                item.BindServerOption(
                    title,
                    string.IsNullOrWhiteSpace(capturedPreset.description)
                        ? "Send generic upgrade request to server."
                        : capturedPreset.description,
                    canApply ? "Server validates cost/effects" : "Missing upgradeKind or stringValue",
                    canApply,
                    () => ApplyUpgrade(capturedId, capturedPreset));
            }
        }

        private async void ApplyUpgrade(string playerCardId, GenericUpgradePreset preset)
        {
            if (preset == null)
            {
                return;
            }

            SetLoading(true);
            ShowStatus("Applying upgrade...");

            try
            {
                var request = new PlayerCardsApiClient.ApplyUpgradeRequestDto
                {
                    upgradeKind = preset.upgradeKind,
                    intValue    = preset.intValue,
                    stringValue = preset.stringValue ?? string.Empty,
                    appliedBy   = string.IsNullOrWhiteSpace(preset.appliedBy) ? "player" : preset.appliedBy,
                    note        = preset.note ?? string.Empty
                };

                var (success, message, updated) = await _collectionService.ApplyUpgradeAsync(playerCardId, request);

                if (success && updated != null)
                {
                    ShowStatus("Upgrade applied!");
                    BindCard(updated);
                    BuildUpgradeHistory(updated.upgrades);
                    BuildUpgradeOptions(playerCardId);
                    OnUpgradeSuccess?.Invoke();
                }
                else
                {
                    ShowStatus(message ?? "Upgrade failed.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CardDetail] ApplyUpgrade failed: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void EnsureDefaultUpgradePresets()
        {
            if (upgradePresets != null && upgradePresets.Length > 0)
            {
                return;
            }

            upgradePresets = new[]
            {
                new GenericUpgradePreset
                {
                    title = "Attack Bonus (+1)",
                    description = "POST upgradeKind=attack_bonus, intValue=1.",
                    upgradeKind = "attack_bonus",
                    intValue = 1
                },
                new GenericUpgradePreset
                {
                    title = "Health Bonus (+1)",
                    description = "POST upgradeKind=health_bonus, intValue=1.",
                    upgradeKind = "health_bonus",
                    intValue = 1
                },
                new GenericUpgradePreset
                {
                    title = "Armor Bonus (+1)",
                    description = "POST upgradeKind=armor_bonus, intValue=1.",
                    upgradeKind = "armor_bonus",
                    intValue = 1
                },
                new GenericUpgradePreset
                {
                    title = "Level Up",
                    description = "POST upgradeKind=level_up.",
                    upgradeKind = "level_up",
                    intValue = 0
                }
            };
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
