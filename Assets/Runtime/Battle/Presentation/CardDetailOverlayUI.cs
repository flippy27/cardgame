using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Flippy.CardDuelMobile.UI
{
    public class CardDetailOverlayUI : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private GameObject costRoot;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private GameObject armorRoot;
        [SerializeField] private Image attackTypeImage;
        [SerializeField] private GameObject attackTypeRoot;
        [SerializeField] private Sprite meleeAttackTypeSprite;
        [SerializeField] private Sprite rangedAttackTypeSprite;
        [SerializeField] private Sprite magicAttackTypeSprite;
        [SerializeField] private TextMeshProUGUI ownerText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI legacyStatsText;

        [Header("Runtime Icon Groups")]
        [SerializeField] private CardIconGroup abilityIconGroup;
        [SerializeField] private CardIconGroup statusIconGroup;

        [Header("Legacy Ability Icon Slots")]
        [SerializeField] private CardStateIconSlot[] abilityIconSlots;

        [Header("Legacy Buff/Debuff Icon Slots")]
        [SerializeField] private CardStateIconSlot[] statusIconSlots;

        [Header("Legacy State Icons")]
        [SerializeField] private CardStateIconSlot[] stateIconSlots;

        public bool IsVisible { get; private set; }
        public ICardDisplay CurrentSource { get; private set; }
        public Card3DView CurrentHandCardSource => CurrentSource as Card3DView;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (visualRenderer == null)
            {
                visualRenderer = panelRoot.GetComponent<CardSurfaceVisualRenderer>() ??
                                 GetComponent<CardSurfaceVisualRenderer>() ??
                                 GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            }

            if (visualRenderer == null)
            {
                visualRenderer = panelRoot.AddComponent<CardSurfaceVisualRenderer>();
            }

            var detailImage = FindPreferredDetailImage();
            if (visualRenderer != null && detailImage != null)
            {
                visualRenderer.EnsureDefaultImageBinding(detailImage, "played");
            }

            attackTypeImage ??= FindPreferredAttackTypeImage();
            if (attackTypeRoot == null && attackTypeImage != null)
            {
                attackTypeRoot = attackTypeImage.gameObject;
            }

            SetVisible(false);
        }

        public void Show(ICardDisplay source, string ownerLabel = null)
        {
            if (source?.CardData == null)
            {
                return;
            }

            CurrentSource = source;
            var card = source.CardData;

            CardVisualCommon.ApplyCardTexts(
                card,
                titleText,
                costText,
                costRoot,
                attackText,
                healthText,
                armorText,
                armorRoot,
                legacyStatsText);

            if (ownerText != null)
            {
                ownerText.text = ownerLabel ?? string.Empty;
                ownerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(ownerLabel));
            }

            CardVisualCommon.ApplyDescriptionText(card, bodyText);
            CardVisualCommon.ApplyAttackTypeIcon(
                card,
                attackTypeImage,
                attackTypeRoot,
                meleeAttackTypeSprite,
                rangedAttackTypeSprite,
                magicAttackTypeSprite);

            visualRenderer?.ApplyCard(card.cardId, source is Card3DView ? "hand" : "played");
            CardVisualCommon.ApplyAbilityIcons(card, abilityIconGroup, abilityIconSlots);
            var resolvedStatusSlots = statusIconSlots != null && statusIconSlots.Length > 0 ? statusIconSlots : stateIconSlots;
            CardVisualCommon.ApplyStatusIcons(card, statusIconGroup, resolvedStatusSlots);
            SetVisible(true);
        }

        public void Hide()
        {
            CurrentSource = null;
            SetVisible(false);
        }

        public bool IsShowing(ICardDisplay source)
        {
            return IsVisible && CurrentSource != null && ReferenceEquals(CurrentSource, source);
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        private Image FindPreferredDetailImage()
        {
            var searchRoot = panelRoot != null ? panelRoot.transform : transform;
            var images = searchRoot.GetComponentsInChildren<Image>(true);
            Image fallback = null;

            foreach (var image in images)
            {
                if (image == null)
                {
                    continue;
                }

                var objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("art") || objectName.Contains("visual") || objectName.Contains("card"))
                {
                    return image;
                }

                fallback ??= image;
            }

            return fallback;
        }

        private Image FindPreferredAttackTypeImage()
        {
            var searchRoot = panelRoot != null ? panelRoot.transform : transform;
            foreach (var image in searchRoot.GetComponentsInChildren<Image>(true))
            {
                if (image == null)
                {
                    continue;
                }

                var objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("attacktype") || objectName.Contains("delivery") || objectName.Contains("combat"))
                {
                    return image;
                }
            }

            return null;
        }
    }
}
