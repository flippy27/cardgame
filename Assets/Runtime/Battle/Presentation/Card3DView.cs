using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    public class Card3DView : MonoBehaviour, ICardDisplay
    {
        [Header("References")]
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private Transform uprightOverlayRoot;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private GameObject costRoot;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private GameObject armorRoot;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image attackTypeImage;
        [SerializeField] private GameObject attackTypeRoot;
        [SerializeField] private Sprite meleeAttackTypeSprite;
        [SerializeField] private Sprite rangedAttackTypeSprite;
        [SerializeField] private Sprite magicAttackTypeSprite;
        [SerializeField] private TextMeshProUGUI legacyStatsText;

        [Header("Runtime Icon Groups")]
        [SerializeField] private CardIconGroup abilityIconGroup;

        [Header("Legacy Ability Icon Slots")]
        [SerializeField] private CardStateIconSlot[] abilityIconSlots;

        [Header("Colors")]
        [SerializeField] private Color baseColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        public BoardCardDto CardData { get; private set; }
        public int PlayerIndex { get; private set; }

        private Material _cardMaterial;

        private void Awake()
        {
            AutoAssignReferences();
            EnsureRuntimeMaterial();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        public void Initialize(BoardCardDto card, int playerIndex)
        {
            CardData = card;
            PlayerIndex = playerIndex;

            AutoAssignReferences();
            EnsureRuntimeMaterial();

            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
                interactionCollider.isTrigger = false;
            }

            UpdateStatsDisplay();
            LayoutStatsOverlay();
            visualRenderer?.ApplyCard(card.cardId, "hand");

            GameLogger.Info("Card3D", $"Initialized {card.displayName}");
        }

        // The stat circles+icons+numbers are now ONE overlay-UI badge per stat, built by
        // CardStatBadges under the StatsOverlay rect (number is a child of its circle, so they can
        // never drift). This method only positions the NAME strip and the ability-icon panel; the
        // badges (cost/attack/health/armor/rarity) are placed by CardStatBadges.Apply at the socket
        // fractions in CardArtLibrary. Tune the sockets/sizes there, not here.
        private void LayoutStatsOverlay()
        {
            // Hide the prefab's per-stat TMP refs — CardStatBadges owns the stat numbers now. The
            // shared "name" object is kept; ApplyCardTexts still drives nameText.
            HideLegacyStat(costText);
            HideLegacyStat(attackText);
            HideLegacyStat(healthText);
            HideLegacyStat(armorText);

            PlaceStat(nameText, new Vector2(0.5f, 1f), new Vector2(0f, -180f), 70f);    // name, title strip

            // Ability icons: a BIG, centred row of square cells across the lower-middle of the card
            // (max 3). Larger + lower than before so the skills read clearly (see reference). The hand
            // overlay is 1000x1400; anchor (0.5,0.5) is centre, y is negative = downward.
            abilityIconGroup?.SetCellSize(new Vector2(180f, 180f));
            PlaceIconPanel(abilityIconGroup, new Vector2(0.5f, 0.5f), new Vector2(0f, -350f), new Vector2(560f, 180f));
        }

        // The prefab wires a couple of stat TMPs to the SAME GameObject as the name strip; don't
        // disable those (it would blank the name). Only blank the text on dedicated stat objects so a
        // stray prefab number never shows behind the new overlay badges.
        private void HideLegacyStat(TextMeshProUGUI text)
        {
            if (text == null || (nameText != null && ReferenceEquals(text.gameObject, nameText.gameObject)))
            {
                return;
            }
            text.text = string.Empty;
        }

        private RectTransform OverlayRect => uprightOverlayRoot as RectTransform;

        private static void PlaceIconPanel(CardIconGroup group, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            if (group == null || group.transform is not RectTransform rect)
            {
                return;
            }
            rect.localScale = Vector3.one;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private static void PlaceStat(TextMeshProUGUI text, Vector2 anchor, Vector2 anchoredPosition, float fontSize)
        {
            if (text == null)
            {
                return;
            }

            // Guarantee the stat object is showing. The prefab wires legacyStatsText to the SAME
            // GameObject as nameText; ApplyCardTexts disables that shared object when dedicated stats
            // exist, so force every placed stat active to keep numbers from being hidden.
            if (!text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }

            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 320f);
            rect.anchoredPosition = anchoredPosition;

            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            StyleStat(text);
        }

        // White, bold, black-outlined so numbers read clearly over the frame (no flat black text).
        private static void StyleStat(TextMeshProUGUI text)
        {
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            // Outline via the instance material (text.outlineWidth alone often doesn't take on the
            // default font material). fontMaterial returns a per-instance copy, so this doesn't leak.
            var mat = text.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        }

        public void UpdateStatsDisplay()
        {
            if (CardData == null)
            {
                return;
            }

            // Stat numbers (cost/attack/health/armor) are owned by the overlay badges now, so don't
            // pass those TMPs to ApplyCardTexts — only the NAME strip is driven here. nameText != null
            // keeps hasDedicatedStats true, so the legacy combined-stats string stays hidden.
            CardVisualCommon.ApplyCardTexts(
                CardData,
                nameText,
                null,
                costRoot,
                null,
                null,
                null,
                armorRoot,
                legacyStatsText);

            // Build/refresh the overlay stat badges (circle + icon + number per stat) on the overlay.
            CardStatBadges.Apply(OverlayRect, CardData, isBoard: false);

            CardVisualCommon.ApplyDescriptionText(CardData, descriptionText);
            // Old unit-type square is redundant (the attack badge shows sword/bow/magic) — hide it.
            if (attackTypeImage != null) attackTypeImage.enabled = false;
            if (attackTypeRoot != null) attackTypeRoot.SetActive(false);
            if (abilityIconGroup == null)
            {
                abilityIconGroup = CreateRuntimeIconGroup("AbilityIcons");
                abilityIconGroup?.SetCellSize(new Vector2(180f, 180f));
                PlaceIconPanel(abilityIconGroup, new Vector2(0.5f, 0.5f), new Vector2(0f, -350f), new Vector2(560f, 180f));
            }
            CardVisualCommon.ApplyAbilityIcons(CardData, abilityIconGroup, abilityIconSlots);
        }

        // Builds the ability-icon group at runtime when the prefab didn't wire one, so unit skills
        // always show. Parented under the world-space StatsOverlay (same canvas as the stat badges).
        private CardIconGroup CreateRuntimeIconGroup(string groupName)
        {
            var parent = OverlayRect;
            if (parent == null)
            {
                return null;
            }
            var go = new GameObject(groupName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<CardIconGroup>();
        }

        public void SetStatsOverlayRotation(Quaternion rotation)
        {
            if (uprightOverlayRoot != null)
            {
                uprightOverlayRoot.localRotation = rotation;
            }
        }

        private void OnDestroy()
        {
            CardStatBadges.Release(OverlayRect);
        }

        public void SetColor(Color color)
        {
            if (_cardMaterial != null)
            {
                _cardMaterial.color = color;
            }
        }

        public void ResetColor()
        {
            SetColor(Color.white);
        }

        public void AnimateDrop(Vector3 targetPos, float duration = 0.3f)
        {
            StartCoroutine(AnimateDropCoroutine(targetPos, duration));
        }

        public void AnimateAttack(Vector3 targetPos, float returnDuration = 0.4f)
        {
            StartCoroutine(AnimateAttackCoroutine(targetPos, returnDuration));
        }

        public void AnimateDeath(float duration = 0.5f)
        {
            StartCoroutine(AnimateDeathCoroutine(duration));
        }

        private void AutoAssignReferences()
        {
            if (meshRenderer == null || !meshRenderer.gameObject.activeSelf)
            {
                meshRenderer = FindPreferredRenderer();
            }

            if (interactionCollider == null || !interactionCollider.gameObject.activeSelf)
            {
                interactionCollider = FindPreferredCollider();
            }

            if (uprightOverlayRoot == null)
            {
                var overlay = transform.Find("StatsOverlay");
                if (overlay != null)
                {
                    uprightOverlayRoot = overlay;
                }
            }

            if (legacyStatsText == null)
            {
                legacyStatsText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            descriptionText ??= FindTextByName("description", "body", "rules", "ability");
            attackTypeImage ??= FindImageByName("attacktype", "delivery", "range", "combat");

            if (attackTypeRoot == null && attackTypeImage != null)
            {
                attackTypeRoot = attackTypeImage.gameObject;
            }

            if (visualRenderer == null)
            {
                visualRenderer = GetComponent<CardSurfaceVisualRenderer>() ?? GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            }

            if (visualRenderer == null && Application.isPlaying)
            {
                visualRenderer = gameObject.AddComponent<CardSurfaceVisualRenderer>();
            }

            if (visualRenderer != null && meshRenderer != null)
            {
                visualRenderer.EnsureDefaultMaterialBinding(meshRenderer, "hand");
                HideUnboundCardRenderers();
            }
        }

        // The card prefab carries a few legacy mesh quads (Visual, CardMesh, Visual (1)); only the
        // bound one shows the composited card. Disable the others at runtime so their leftover
        // material (a red quad behind the card) stops showing. Runtime-only: the prefab is untouched.
        private void HideUnboundCardRenderers()
        {
            if (!Application.isPlaying || meshRenderer == null)
            {
                return;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != meshRenderer)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void EnsureRuntimeMaterial()
        {
            if (meshRenderer == null || _cardMaterial != null)
            {
                return;
            }

            var sourceMaterial = meshRenderer.sharedMaterial != null
                ? meshRenderer.sharedMaterial
                : new Material(Shader.Find("Standard"));

            // White base so the card art texture shows untinted (the binding sets the texture +
            // forces white; baseColor is only kept for highlight/death-fade effects).
            _cardMaterial = new Material(sourceMaterial)
            {
                color = Color.white
            };
            meshRenderer.material = _cardMaterial;
        }

        private Renderer FindPreferredRenderer()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var candidate in renderers)
            {
                if (candidate.gameObject.activeSelf)
                {
                    return candidate;
                }
            }

            return renderers.Length > 0 ? renderers[0] : null;
        }

        private Collider FindPreferredCollider()
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var candidate in colliders)
            {
                if (candidate.gameObject.activeSelf)
                {
                    return candidate;
                }
            }

            return colliders.Length > 0 ? colliders[0] : null;
        }

        private TextMeshProUGUI FindTextByName(params string[] keywords)
        {
            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var objectName = text.gameObject.name.ToLowerInvariant();
                foreach (var keyword in keywords)
                {
                    if (objectName.Contains(keyword))
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private Image FindImageByName(params string[] keywords)
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                var objectName = image.gameObject.name.ToLowerInvariant();
                foreach (var keyword in keywords)
                {
                    if (objectName.Contains(keyword))
                    {
                        return image;
                    }
                }
            }

            return null;
        }

        private System.Collections.IEnumerator AnimateDropCoroutine(Vector3 targetPos, float duration)
        {
            var startPos = transform.position;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            transform.position = targetPos;
        }

        private System.Collections.IEnumerator AnimateAttackCoroutine(Vector3 targetPos, float duration)
        {
            var startPos = transform.position;
            var elapsed = 0f;

            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                transform.position = Vector3.Lerp(targetPos, startPos, t);
                yield return null;
            }

            transform.position = startPos;
        }

        private System.Collections.IEnumerator AnimateDeathCoroutine(float duration)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                if (_cardMaterial != null)
                {
                    var color = _cardMaterial.color;
                    color.a = Mathf.Lerp(baseColor.a, 0f, t);
                    _cardMaterial.color = color;
                }

                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
