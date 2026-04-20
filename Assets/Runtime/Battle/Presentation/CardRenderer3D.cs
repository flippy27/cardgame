using UnityEngine;
using TMPro;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Renderer mejorado para cartas 3D.
    /// Usa quad con material y canvas 2D para stats.
    /// </summary>
    public class CardRenderer3D : MonoBehaviour
    {
        private Material _cardMaterial;
        private TextMeshProUGUI _statsText;
        private Canvas _statsCanvas;

        public void Initialize(string displayName, int attack, int health)
        {
            // Crear quad para la carta (visual principal)
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "CardQuad";
            quad.transform.SetParent(transform);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localScale = new Vector3(1f, 1.3f, 1f);

            // Remover collider del quad
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                DestroyImmediate(collider);

            // Material de la carta
            _cardMaterial = new Material(Shader.Find("Standard"));
            _cardMaterial.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            var renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = _cardMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // Canvas 2D para stats (hijo del quad)
            var canvasGo = new GameObject("StatsCanvas");
            canvasGo.transform.SetParent(quad.transform);
            canvasGo.transform.localPosition = Vector3.forward * 0.01f;
            canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            _statsCanvas = canvasGo.AddComponent<Canvas>();
            _statsCanvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = canvasGo.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(1024, 1024);

            // Text con stats
            var textGo = new GameObject("StatsText");
            textGo.transform.SetParent(canvasGo.transform);
            textGo.transform.localPosition = Vector3.zero;

            _statsText = textGo.AddComponent<TextMeshProUGUI>();
            _statsText.text = $"{displayName}\n{attack} ATK | {health} HP";
            _statsText.alignment = TextAlignmentOptions.Center;
            _statsText.fontSize = 80;
            _statsText.color = Color.white;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(1024, 1024);
        }

        public void UpdateStats(string displayName, int attack, int health)
        {
            if (_statsText != null)
            {
                _statsText.text = $"{displayName}\n{attack} ATK | {health} HP";
            }
        }

        public void SetColor(Color color)
        {
            if (_cardMaterial != null)
            {
                _cardMaterial.color = color;
            }
        }

        public void SetGlow(bool enabled)
        {
            if (_cardMaterial != null)
            {
                if (enabled)
                {
                    _cardMaterial.SetFloat("_Emission", 1f);
                }
                else
                {
                    _cardMaterial.SetFloat("_Emission", 0f);
                }
            }
        }
    }
}
