using UnityEngine;
using TMPro;
using System.Collections;

namespace Flippy.CardDuelMobile.UI
{
    public class Toast : MonoBehaviour
    {
        public TextMeshProUGUI messageText;
        public CanvasGroup canvasGroup;
        public float displayDuration = 2f;
        public float fadeDuration = 0.3f;

        private Coroutine _displayCoroutine;

        private void OnEnable()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (messageText == null)
                messageText = GetComponentInChildren<TextMeshProUGUI>();

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        public void Show(string message)
        {
            if (messageText != null)
                messageText.text = message;

            if (_displayCoroutine != null)
                StopCoroutine(_displayCoroutine);

            _displayCoroutine = StartCoroutine(DisplayCoroutine());
        }

        private IEnumerator DisplayCoroutine()
        {
            // Fade in
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }

            // Display
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }
        }
    }
}
