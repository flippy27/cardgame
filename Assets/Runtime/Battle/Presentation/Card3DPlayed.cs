using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    public class Card3DPlayed : MonoBehaviour, ICardDisplay
    {
        [Header("References")]
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Collider meshCollider;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private GameObject armorRoot;
        [SerializeField] private Image attackTypeImage;
        [SerializeField] private GameObject attackTypeRoot;
        [SerializeField] private Sprite meleeAttackTypeSprite;
        [SerializeField] private Sprite rangedAttackTypeSprite;
        [SerializeField] private Sprite magicAttackTypeSprite;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Runtime Icon Groups")]
        [SerializeField] private CardIconGroup abilityIconGroup;
        [SerializeField] private CardIconGroup statusIconGroup;

        [Header("Legacy Ability Icon Slots")]
        [SerializeField] private CardStateIconSlot[] abilityIconSlots;

        [Header("Legacy Buff/Debuff Icon Slots")]
        [SerializeField] private CardStateIconSlot[] statusIconSlots;

        [Header("Legacy State Icons")]
        [SerializeField] private CardStateIconSlot[] stateIconSlots;

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

            if (meshCollider != null)
            {
                meshCollider.enabled = true;
                meshCollider.isTrigger = false;
            }

            UpdateStatsDisplay();
            LayoutStatsOverlay();
            visualRenderer?.ApplyCard(card.cardId, "played");

            GameLogger.Info("Card3DPlayed", $"Initialized {card.displayName}");
        }

        // The board token's stat texts ship hand-tuned to an old frame's coordinate space; normalise
        // them in the 1000x1000 overlay canvas so they land on the current board frame's sockets at
        // a readable size. No mana cost on the board. Tweak offsets to match the frame art.
        private void LayoutStatsOverlay()
        {
            // Numbers sit on top of the baked socket symbols (same fractions as CardArtLibrary, with
            // the centred frame's transparent band accounted for). Overlay canvas is 1000x1000.
            // Canvas 1000x1000. Tracks board socket fractions (attack 0.13,0.74 · health 0.87,0.74).
            PlaceStat(nameText, new Vector2(0.5f, 1f), new Vector2(0f, -180f), 65f);   // name strip (top)
            PlaceStat(attackText, new Vector2(0f, 0f), new Vector2(130f, 260f), 155f); // attack, bottom-left
            PlaceStat(healthText, new Vector2(1f, 0f), new Vector2(-130f, 260f), 155f);// health, bottom-right
            PlaceStat(armorText, new Vector2(1f, 0f), new Vector2(-130f, 345f), 135f); // armor, above health

            // Ability icons float along the top of the board token; status badges along the bottom
            // (prefab had both off-card with zero height).
            PlaceIconPanel(abilityIconGroup, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 140f));
            PlaceIconPanel(statusIconGroup, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(700f, 140f));
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
                null,
                null,
                attackText,
                healthText,
                armorText,
                armorRoot,
                statsText);

            if (healthText != null)
            {
                healthText.text = CardData.currentHealth.ToString();
            }

            CardVisualCommon.ApplyAttackTypeIcon(
                CardData,
                attackTypeImage,
                attackTypeRoot,
                meleeAttackTypeSprite,
                rangedAttackTypeSprite,
                magicAttackTypeSprite);
            CardVisualCommon.ApplyAbilityIcons(CardData, abilityIconGroup, abilityIconSlots);
            CardVisualCommon.ApplyStatusIcons(CardData, statusIconGroup, statusIconSlots != null && statusIconSlots.Length > 0 ? statusIconSlots : stateIconSlots);
        }

        public void SetStateIcons(CardStateVisualData[] states)
        {
            CardVisualCommon.ApplyStateIcons(statusIconGroup, stateIconSlots, states);
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

        /// <summary>
        /// Drops the card "from the sky": starts above the slot, slightly larger than its resting
        /// size, and eases down to the target position and real scale (with a tiny settle overshoot).
        /// </summary>
        public void AnimateDropFromSky(Vector3 targetPos, float duration = 0.32f, float startScaleMultiplier = 1.25f, float skyHeight = 2.2f)
        {
            StartCoroutine(AnimateDropFromSkyCoroutine(targetPos, duration, startScaleMultiplier, skyHeight));
        }

        private System.Collections.IEnumerator AnimateDropFromSkyCoroutine(Vector3 targetPos, float duration, float startScaleMultiplier, float skyHeight)
        {
            var restScale = transform.localScale;
            var startScale = restScale * Mathf.Max(1f, startScaleMultiplier);
            var startPos = targetPos + Vector3.up * skyHeight;
            transform.position = startPos;
            transform.localScale = startScale;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic (fast then settle)
                transform.position = Vector3.Lerp(startPos, targetPos, ease);
                transform.localScale = Vector3.Lerp(startScale, restScale, ease);
                yield return null;
            }

            transform.position = targetPos;
            transform.localScale = restScale;
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

            if (meshCollider == null || !meshCollider.gameObject.activeSelf)
            {
                meshCollider = FindPreferredCollider();
            }

            if (statsText == null)
            {
                statsText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

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
                visualRenderer.EnsureDefaultMaterialBinding(meshRenderer, "played");
                HideUnboundCardRenderers();
            }
        }

        // The card prefab carries a few legacy mesh quads; only the bound one shows the composited
        // card. Disable the others at runtime so their leftover material (a red quad behind the
        // card) stops showing. Runtime-only: the prefab is untouched.
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
