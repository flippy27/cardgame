using UnityEngine;
using TMPro;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    public class Card3DPlayed : MonoBehaviour, ICardDisplay
    {
        public BoardCardDto CardData { get; private set; }
        public int PlayerIndex { get; private set; }

        [SerializeField] private Renderer _meshRenderer;
        [SerializeField] private TextMeshProUGUI _statsText;
        [SerializeField] private Collider _meshCollider;

        private Material _cardMaterial;

        public void Initialize(BoardCardDto card, int playerIndex)
        {
            CardData = card;
            PlayerIndex = playerIndex;

            if (_meshRenderer == null)
            {
                Debug.LogError("[Card3DPlayed] Mesh renderer not assigned!");
                return;
            }

            if (_statsText == null)
            {
                Debug.LogError("[Card3DPlayed] Stats text not assigned!");
                return;
            }

            _cardMaterial = new Material(Shader.Find("Standard"));
            _cardMaterial.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            _meshRenderer.material = _cardMaterial;

            if (_meshCollider != null)
            {
                _meshCollider.enabled = true;
                _meshCollider.isTrigger = false;
            }

            _statsText.text = FormatStats(card);
            _statsText.alignment = TextAlignmentOptions.Center;
            _statsText.fontSize = 0.5f;
            _statsText.color = Color.white;
            _statsText.outlineWidth = 0.2f;
            _statsText.outlineColor = Color.black;

            GameLogger.Info("Card3DPlayed", $"Initialized {card.displayName}");
        }

        private string FormatStats(BoardCardDto card)
        {
            return $"<b>{card.displayName}</b>\n\n{card.attack} ATK\n{card.currentHealth}/{card.maxHealth} HP";
        }

        public void UpdateStatsDisplay()
        {
            if (_statsText != null && CardData != null)
            {
                _statsText.text = FormatStats(CardData);
            }
        }

        public void SetColor(Color color)
        {
            if (_cardMaterial != null)
            {
                _cardMaterial.color = color;
            }
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
                    color.a = Mathf.Lerp(0.9f, 0f, t);
                    _cardMaterial.color = color;
                }

                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
