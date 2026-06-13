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

        // The prefab ships the stat texts hand-tuned to an old frame's coordinate space (anchored
        // positions in the thousands, 100x child scales), so they no longer land on the current
        // frame's sockets. Normalise them here in the 1000x1400 overlay canvas: each stat is pinned
        // to its corner socket at a readable size. Tweak these offsets to match the frame art.
        private void LayoutStatsOverlay()
        {
            // Numbers sit on top of the baked socket symbols (same fractions as CardArtLibrary, with
            // the centred frame's transparent band accounted for). Overlay canvas is 1000x1400.
            PlaceStat(costText, new Vector2(1f, 1f), new Vector2(-130f, -238f), 150f);  // cost, top-right (0.87,0.17)
            PlaceStat(nameText, new Vector2(0.5f, 1f), new Vector2(0f, -180f), 70f);    // name, title strip
            PlaceStat(attackText, new Vector2(0f, 0f), new Vector2(130f, 308f), 150f);  // attack, bottom-left (0.13,0.78)
            PlaceStat(healthText, new Vector2(1f, 0f), new Vector2(-130f, 308f), 150f); // health, bottom-right (0.87,0.78)
            PlaceStat(armorText, new Vector2(1f, 0f), new Vector2(-130f, 427f), 130f);  // armor, above health

            // Ability icons: a row across the lower-middle of the card (prefab had it parked off-screen).
            PlaceIconPanel(abilityIconGroup, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(760f, 150f));
        }

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

            CardVisualCommon.ApplyCardTexts(
                CardData,
                nameText,
                costText,
                costRoot,
                attackText,
                healthText,
                armorText,
                armorRoot,
                legacyStatsText);
            CardVisualCommon.ApplyDescriptionText(CardData, descriptionText);
            CardVisualCommon.ApplyAttackTypeIcon(
                CardData,
                attackTypeImage,
                attackTypeRoot,
                meleeAttackTypeSprite,
                rangedAttackTypeSprite,
                magicAttackTypeSprite);
            CardVisualCommon.ApplyAbilityIcons(CardData, abilityIconGroup, abilityIconSlots);
        }

        public void SetStatsOverlayRotation(Quaternion rotation)
        {
            if (uprightOverlayRoot != null)
            {
                uprightOverlayRoot.localRotation = rotation;
            }
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
