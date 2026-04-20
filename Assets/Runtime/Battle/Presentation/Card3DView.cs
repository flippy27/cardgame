using UnityEngine;
using TMPro;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Representación visual 3D de una carta.
    /// Quad + Canvas 2D para stats.
    /// </summary>
    public class Card3DView : MonoBehaviour
    {
        public BoardCardDto CardData { get; private set; }
        public int PlayerIndex { get; private set; }

        private Material _cardMaterial;
        private TextMeshProUGUI _statsText;
        private GameObject _meshObject;

        public void Initialize(BoardCardDto card, int playerIndex)
        {
            CardData = card;
            PlayerIndex = playerIndex;

            // Crear quad visual
            _meshObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _meshObject.name = "CardMesh";
            _meshObject.transform.SetParent(transform);
            _meshObject.transform.localPosition = Vector3.zero;
            _meshObject.transform.localScale = new Vector3(0.8f, 1f, 1f);
            _meshObject.transform.localRotation = Quaternion.Euler(0, 0, 0);

            // Quitar collider
            var collider = _meshObject.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            // Material
            var renderer = _meshObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                _cardMaterial = new Material(Shader.Find("Standard"));
                _cardMaterial.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
                renderer.material = _cardMaterial;
            }

            // Canvas 2D para stats
            var canvasGo = new GameObject("StatsOverlay");
            canvasGo.transform.SetParent(transform);
            canvasGo.transform.localPosition = Vector3.forward * 0.01f;
            canvasGo.transform.localScale = Vector3.one * 0.01f;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200, 150);

            // Text
            var textGo = new GameObject("Stats");
            textGo.transform.SetParent(canvasGo.transform);
            textGo.transform.localPosition = Vector3.zero;

            _statsText = textGo.AddComponent<TextMeshProUGUI>();
            _statsText.text = FormatStats(card);
            _statsText.alignment = TextAlignmentOptions.Center;
            _statsText.fontSize = 0.5f;
            _statsText.color = Color.white;
            _statsText.outlineWidth = 0.2f;
            _statsText.outlineColor = Color.black;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200, 150);

            // Collider para raycast (en el quad)
            var qCollider = _meshObject.GetComponent<Collider>();
            if (qCollider != null)
                qCollider.enabled = true;

            GameLogger.Info("Card3D", $"Initialized {card.displayName}");
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
