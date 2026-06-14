using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.UI
{
    [Serializable]
    public sealed class CardStateIconSlot
    {
        public GameObject root;
        public Image iconImage;
        public TextMeshProUGUI stackText;

        public void Clear()
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (stackText != null)
            {
                stackText.text = string.Empty;
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Apply(CardStateVisualData state)
        {
            if (state == null)
            {
                Clear();
                return;
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            if (iconImage != null)
            {
                iconImage.sprite = state.icon;
                iconImage.enabled = state.icon != null;
                if (state.tint.a > 0f)
                {
                    iconImage.color = state.tint;
                }
            }

            if (stackText != null)
            {
                stackText.text = state.stackCount > 1 ? state.stackCount.ToString() : string.Empty;
            }
        }
    }

    [Serializable]
    public sealed class CardStateVisualData
    {
        public string stateId;
        public string displayName;
        public Sprite icon;
        public int stackCount = 1;
        public Color tint = Color.white;
    }

    internal static class CardVisualCommon
    {
        public static void ApplyCardTexts(
            BoardCardDto card,
            TMP_Text nameText,
            TMP_Text costText,
            GameObject costRoot,
            TMP_Text attackText,
            TMP_Text healthText,
            TMP_Text armorText,
            GameObject armorRoot,
            TMP_Text legacyStatsText)
        {
            if (card == null)
            {
                return;
            }

            var hasDedicatedStats =
                nameText != null ||
                costText != null ||
                attackText != null ||
                healthText != null ||
                armorText != null;

            if (nameText != null)
            {
                nameText.text = card.displayName ?? string.Empty;
            }

            var resolvedManaCost = ResolveManaCost(card);
            if (costText != null)
            {
                costText.text = resolvedManaCost.ToString();
            }

            if (costRoot != null)
            {
                costRoot.SetActive(resolvedManaCost >= 0);
            }

            if (attackText != null)
            {
                attackText.text = card.attack.ToString();
            }

            if (healthText != null)
            {
                healthText.text = $"{card.currentHealth}/{card.maxHealth}";
            }

            if (armorText != null)
            {
                armorText.text = card.armor.ToString();
            }

            if (armorRoot != null)
            {
                armorRoot.SetActive(card.armor > 0);
            }

            if (legacyStatsText != null)
            {
                legacyStatsText.gameObject.SetActive(!hasDedicatedStats);
                if (!hasDedicatedStats)
                {
                    legacyStatsText.text = FormatLegacyStats(card);
                }
            }
        }

        public static void ApplyDescriptionText(BoardCardDto card, TMP_Text descriptionText)
        {
            if (descriptionText == null)
            {
                return;
            }

            descriptionText.text = ResolveDescriptionText(card);
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(descriptionText.text));
        }

        public static void ApplyAttackTypeIcon(
            BoardCardDto card,
            Image attackTypeImage,
            GameObject attackTypeRoot,
            Sprite meleeSprite,
            Sprite rangedSprite,
            Sprite magicSprite)
        {
            if (attackTypeImage == null)
            {
                if (attackTypeRoot != null)
                {
                    attackTypeRoot.SetActive(false);
                }

                return;
            }

            var sprite = ResolveAttackTypeSprite(card, meleeSprite, rangedSprite, magicSprite);
            attackTypeImage.sprite = sprite;
            attackTypeImage.enabled = sprite != null;

            if (attackTypeRoot != null)
            {
                attackTypeRoot.SetActive(sprite != null);
            }
        }

        public static void ApplyStateIcons(CardStateIconSlot[] slots, IReadOnlyList<CardStateVisualData> states)
        {
            if (slots == null || slots.Length == 0)
            {
                return;
            }

            for (var index = 0; index < slots.Length; index++)
            {
                var state = states != null && index < states.Count ? states[index] : null;
                slots[index]?.Apply(state);
            }
        }

        public static void ApplyStateIcons(CardIconGroup iconGroup, CardStateIconSlot[] fallbackSlots, IReadOnlyList<CardStateVisualData> states)
        {
            if (iconGroup != null)
            {
                iconGroup.Apply(states);
                return;
            }

            ApplyStateIcons(fallbackSlots, states);
        }

        public static void ApplyAbilityIcons(BoardCardDto card, CardStateIconSlot[] slots)
        {
            ApplyStateIcons(slots, BuildAbilityVisuals(card));
        }

        public static void ApplyAbilityIcons(BoardCardDto card, CardIconGroup iconGroup, CardStateIconSlot[] fallbackSlots = null)
        {
            ApplyStateIcons(iconGroup, fallbackSlots, BuildAbilityVisuals(card));
        }

        public static void ApplyStatusIcons(BoardCardDto card, CardStateIconSlot[] slots)
        {
            ApplyStateIcons(slots, BuildStatusVisuals(card));
        }

        public static void ApplyStatusIcons(BoardCardDto card, CardIconGroup iconGroup, CardStateIconSlot[] fallbackSlots = null)
        {
            ApplyStateIcons(iconGroup, fallbackSlots, BuildStatusVisuals(card));
        }

        public static CardStateVisualData[] BuildAbilityVisuals(BoardCardDto card)
        {
            var abilities = ResolveAbilities(card);
            if (abilities == null || abilities.Length == 0)
            {
                return System.Array.Empty<CardStateVisualData>();
            }

            var results = new List<CardStateVisualData>();
            foreach (var ability in abilities)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.abilityId))
                {
                    continue;
                }

                results.Add(new CardStateVisualData
                {
                    stateId = ability.abilityId,
                    displayName = string.IsNullOrWhiteSpace(ability.displayName) ? ability.abilityId : ability.displayName,
                    icon = CardArtLibrary.GetSkillIcon(ability.abilityId),
                    stackCount = 1,
                    tint = Color.white
                });
            }

            return results.ToArray();
        }

        public static CardStateVisualData[] BuildStatusVisuals(BoardCardDto card)
        {
            if (card?.statusEffects == null || card.statusEffects.Length == 0)
            {
                return System.Array.Empty<CardStateVisualData>();
            }

            var results = new List<CardStateVisualData>();
            foreach (var status in card.statusEffects)
            {
                if (status == null)
                {
                    continue;
                }

                var statusId = ResolveStatusId(status);
                results.Add(new CardStateVisualData
                {
                    stateId = statusId,
                    displayName = ResolveStatusDisplayName(status),
                    icon = CardArtLibrary.GetStatusIcon(status.kind),
                    stackCount = status.remainingTurns > 0 ? status.remainingTurns : Mathf.Max(1, status.amount),
                    tint = ResolveStatusTint(status.kind)
                });
            }

            return results.ToArray();
        }

        private static string FormatLegacyStats(BoardCardDto card)
        {
            var armorSuffix = card.armor > 0 ? $"\n{card.armor} ARM" : string.Empty;
            var manaCost = ResolveManaCost(card);
            var costPrefix = manaCost >= 0 ? $"{manaCost} COST\n" : string.Empty;
            return $"<b>{card.displayName}</b>\n\n{costPrefix}{card.attack} ATK\n{card.currentHealth}/{card.maxHealth} HP{armorSuffix}";
        }

        private static Sprite ResolveAttackTypeSprite(BoardCardDto card, Sprite meleeSprite, Sprite rangedSprite, Sprite magicSprite)
        {
            return ResolveStableDeliveryType(card) switch
            {
                AttackPresentationResolver.DeliveryTypeProjectile => rangedSprite,
                AttackPresentationResolver.DeliveryTypeBeam => magicSprite,
                AttackPresentationResolver.DeliveryTypeArc => magicSprite,
                _ => meleeSprite
            };
        }

        private static string ResolveStableDeliveryType(BoardCardDto card)
        {
            if (card == null)
            {
                return AttackPresentationResolver.DeliveryTypeMelee;
            }

            if (!string.IsNullOrWhiteSpace(card.attackDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(card.attackDeliveryType);
            }

            // Last-resort presentation fallback: unit type is card identity, not board position,
            // so the icon will not change when the server moves a card between slots.
            return card.unitType switch
            {
                1 => AttackPresentationResolver.DeliveryTypeProjectile,
                2 => AttackPresentationResolver.DeliveryTypeBeam,
                _ => AttackPresentationResolver.DeliveryTypeMelee
            };
        }

        private static string ResolveDescriptionText(BoardCardDto card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(card.cardId))
            {
                var definition = CardRegistry.GetCard(card.cardId);
                if (definition != null && !string.IsNullOrWhiteSpace(definition.description))
                {
                    return definition.description;
                }
            }

            return !string.IsNullOrWhiteSpace(card.cardId)
                ? $"Card ID: {card.cardId}"
                : string.Empty;
        }

        public static int ResolveManaCost(BoardCardDto card)
        {
            if (card == null)
            {
                return -1;
            }

            if (card.manaCost > 0)
            {
                return card.manaCost;
            }

            if (!string.IsNullOrWhiteSpace(card.cardId))
            {
                var definition = CardRegistry.GetCard(card.cardId);
                if (definition != null)
                {
                    return definition.manaCost;
                }
            }

            return card.manaCost == 0 ? 0 : -1;
        }

        private static CardAbilityDto[] ResolveAbilities(BoardCardDto card)
        {
            if (card == null)
            {
                return System.Array.Empty<CardAbilityDto>();
            }

            if (card.abilities != null && card.abilities.Length > 0)
            {
                return card.abilities;
            }

            if (!string.IsNullOrWhiteSpace(card.cardId) && GameService.Instance?.CardCatalog != null)
            {
                if (GameService.Instance.CardCatalog.TryGetCard(card.cardId, out var serverDefinition) &&
                    serverDefinition?.abilities != null)
                {
                    return serverDefinition.abilities;
                }
            }

            return System.Array.Empty<CardAbilityDto>();
        }

        private static string ResolveStatusId(StatusEffectDto status)
        {
            if (status == null)
            {
                return "status";
            }

            // CardArtLibrary owns the kind->key mapping (mirror StatusEffectKind 0..11).
            var key = CardArtLibrary.StatusIconKey(status.kind);
            if (key != null)
            {
                return key;
            }

            return !string.IsNullOrWhiteSpace(status.abilityId)
                ? NormalizeAssetId(status.abilityId)
                : $"status_{status.kind}";
        }

        private static string ResolveStatusDisplayName(StatusEffectDto status)
        {
            // Reuse the canonical kind names (Poison/Burn/Ward/...) from the event mapper.
            return status != null ? BattlePhaseStructuredEventMapper.StatusKindName(status.kind) : "Status";
        }

        private static Color ResolveStatusTint(int statusKind)
        {
            // CardArtLibrary owns the per-kind tint (mirror StatusEffectKind 0..11).
            return CardArtLibrary.StatusBadgeTint(statusKind);
        }

        private static string NormalizeAssetId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().ToLowerInvariant().Replace(' ', '_');
        }
    }
}
