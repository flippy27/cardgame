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

        [SerializeField] private Renderer _visualRenderer;
        [SerializeField] private Collider _visualCollider;

        private Renderer _renderer;
        private Material _defaultMaterial;
        private Material _highlightMaterial;
        private Coroutine _pulseCoroutine;

        public void Initialize(Material defaultMat, float size)
        {
            if (_visualRenderer == null)
            {
                Debug.LogError("[Board3DSlot] Visual renderer not assigned in inspector!");
                return;
            }

            _renderer = _visualRenderer;
            _defaultMaterial = new Material(defaultMat ?? new Material(Shader.Find("Standard")));
            _defaultMaterial.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);

            _highlightMaterial = new Material(_defaultMaterial);
            _highlightMaterial.color = new Color(0.0f, 1.0f, 0.5f, 0.9f);

            _renderer.material = _defaultMaterial;

            if (_visualCollider != null)
            {
                _visualCollider.enabled = true;
                _visualCollider.isTrigger = false;
            }
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

        public void PulseImpact(Color pulseColor, float intensity, float duration)
        {
            if (_renderer == null || duration <= 0f)
            {
                return;
            }

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }

            _pulseCoroutine = StartCoroutine(PulseImpactCoroutine(pulseColor, intensity, duration));
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

                if (_renderer != null)
                {
                    var color = _defaultMaterial.color;
                    color.a = Mathf.Lerp(0.9f, 0f, t);
                    _defaultMaterial.color = color;
                }

                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                yield return null;
            }

            Destroy(gameObject);
        }

        private System.Collections.IEnumerator PulseImpactCoroutine(Color pulseColor, float intensity, float duration)
        {
            var material = _renderer.material;
            if (material == null)
            {
                yield break;
            }

            var originalColor = material.color;
            var hasEmissionColor = material.HasProperty("_EmissionColor");
            var originalEmission = hasEmissionColor ? material.GetColor("_EmissionColor") : Color.black;

            if (hasEmissionColor)
            {
                material.EnableKeyword("_EMISSION");
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var envelope = Mathf.Sin(t * Mathf.PI);
                material.color = Color.Lerp(originalColor, pulseColor, envelope * 0.45f);

                if (hasEmissionColor)
                {
                    material.SetColor("_EmissionColor", pulseColor * (intensity * envelope));
                }

                yield return null;
            }

            material.color = originalColor;
            if (hasEmissionColor)
            {
                material.SetColor("_EmissionColor", originalEmission);
            }

            _pulseCoroutine = null;
        }
    }
}
