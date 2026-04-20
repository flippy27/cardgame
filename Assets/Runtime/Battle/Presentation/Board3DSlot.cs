using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Representa un slot individual en el board 3D.
    /// </summary>
    public class Board3DSlot : MonoBehaviour
    {
        public int PlayerIndex { get; set; }
        public BoardSlot Slot { get; set; }

        private Renderer _renderer;
        private Material _defaultMaterial;
        private Material _highlightMaterial;

        public void Initialize(Material defaultMat, float size)
        {
            // Crear cube visual
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SlotVisual";
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(size, 0.2f, size);

            // Mantener collider para raycast
            var collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }

            _renderer = cube.GetComponent<Renderer>();
            _defaultMaterial = new Material(defaultMat ?? new Material(Shader.Find("Standard")));
            _defaultMaterial.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);

            _highlightMaterial = new Material(_defaultMaterial);
            _highlightMaterial.color = new Color(0.0f, 1.0f, 0.5f, 0.9f);

            _renderer.material = _defaultMaterial;
        }

        public void SetHighlight(bool highlight)
        {
            if (_renderer != null)
            {
                _renderer.material = highlight ? _highlightMaterial : _defaultMaterial;
            }
        }

        public void SetGlow(bool glow)
        {
            if (_renderer != null && _defaultMaterial != null)
            {
                if (glow)
                {
                    _defaultMaterial.color = new Color(0.5f, 0.5f, 0.3f, 0.9f);
                    _defaultMaterial.SetFloat("_Emission", 0.5f);
                }
                else
                {
                    _defaultMaterial.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
                    _defaultMaterial.SetFloat("_Emission", 0f);
                }
            }
        }
    }
}
