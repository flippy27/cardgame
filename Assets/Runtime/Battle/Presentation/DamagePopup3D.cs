using TMPro;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    public class DamagePopup3D : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private float riseHeight = 0.7f;
        [SerializeField] private AnimationCurve riseCurve = null;
        [SerializeField] private AnimationCurve alphaCurve = null;
        [SerializeField] private bool billboardToCamera = true;

        private void Awake()
        {
            riseCurve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            alphaCurve ??= AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }

        public void Play(string text, Color color)
        {
            if (valueText == null)
            {
                valueText = GetComponentInChildren<TMP_Text>(true);
            }

            if (valueText != null)
            {
                valueText.text = text;
                valueText.color = color;
            }

            StartCoroutine(PlayRoutine(color));
        }

        private void LateUpdate()
        {
            if (!billboardToCamera || Camera.main == null)
            {
                return;
            }

            transform.forward = Camera.main.transform.forward;
        }

        private System.Collections.IEnumerator PlayRoutine(Color baseColor)
        {
            var startPosition = transform.position;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var riseT = riseCurve != null ? riseCurve.Evaluate(t) : t;
                var alphaT = alphaCurve != null ? alphaCurve.Evaluate(t) : 1f - t;

                transform.position = startPosition + Vector3.up * (riseHeight * riseT);

                if (valueText != null)
                {
                    var color = baseColor;
                    color.a = alphaT;
                    valueText.color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
