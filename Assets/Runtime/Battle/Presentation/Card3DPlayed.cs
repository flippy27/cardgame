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
            visualRenderer?.ApplyCard(card.cardId, "played");

            GameLogger.Info("Card3DPlayed", $"Initialized {card.displayName}");
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
            SetColor(baseColor);
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

            _cardMaterial = new Material(sourceMaterial)
            {
                color = baseColor
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
