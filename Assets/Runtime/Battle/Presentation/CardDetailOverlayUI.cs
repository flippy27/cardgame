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
            // FindPreferredDetailImage only finds a real art image when the scene has one named
            // art/visual/card; otherwise it falls back to an unrelated icon panel image (which made the
            // preview render into a tiny wrong rect under the full-screen dim panel). Guarantee a proper
            // centred 2:3 art surface by creating a dedicated one when none is named.
            if (detailImage == null || !detailImage.gameObject.name.ToLowerInvariant().Contains("art"))
            {
                detailImage = CreateDetailArtImage();
            }
            if (visualRenderer != null && detailImage != null)
            {
                visualRenderer.EnsureDefaultImageBinding(detailImage, "played");
                detailImage.preserveAspect = true; // 2:3 card, never stretched across the overlay
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

        // Builds a dedicated centred 2:3 card-art Image under the panel (behind the stat/text children,
        // above the dim backdrop) so the composite shows as a card, not a stretched full-screen wash.
        private Image CreateDetailArtImage()
        {
            var parent = (panelRoot != null ? panelRoot.transform : transform) as RectTransform;
            var go = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 630f); // 2:3 card
            rect.anchoredPosition = Vector2.zero;
            rect.SetSiblingIndex(0); // behind the title/stat children, in front of the panel backdrop

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private Image FindPreferredDetailImage()
        {
            var searchRoot = panelRoot != null ? panelRoot.transform : transform;
            var images = searchRoot.GetComponentsInChildren<Image>(true);
            var panelImage = panelRoot != null ? panelRoot.GetComponent<Image>() : null;
            Image fallback = null;

            // Prefer an explicit art/visual image; never bind the panel's own (usually full-screen)
            // background image, which would stretch the card art across the whole overlay.
            foreach (var image in images)
            {
                if (image == null || image == panelImage)
                {
                    continue;
                }

                var objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("art") || objectName.Contains("visual"))
                {
                    return image;
                }
            }

            foreach (var image in images)
            {
                if (image == null || image == panelImage)
                {
                    continue;
                }

                if (image.gameObject.name.ToLowerInvariant().Contains("card"))
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
