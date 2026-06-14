using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Flippy.CardDuelMobile.UI.DeckBuilding; // KenneyUiSkin.ThemeFont (optional themed font; null when disabled)

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Full-screen "preparing the battle" overlay shown while a match initializes (private match
    /// creation + SignalR connect + first snapshot + the initial card-visual compositing burst) so the
    /// player never stares at an empty board. Built ENTIRELY in code on its own top-most Screen-Space
    /// Overlay canvas (no scene editing, no prefab) and themed with placeholder visuals: a dimmed
    /// backdrop, a procedural spinning ring, a status label, and an optional progress bar.
    ///
    /// Usage:
    ///   var overlay = MatchLoadingOverlay.Show("Connecting to match...");
    ///   overlay.SetStatus("Dealing your hand...");
    ///   overlay.SetProgress(0.6f);   // optional; hidden if never called
    ///   overlay.Hide();              // fades out then destroys itself
    ///
    /// Self-contained so it can be wired from any match-start flow without touching the scene.
    /// </summary>
    public sealed class MatchLoadingOverlay : MonoBehaviour
    {
        private const int SortingOrder = 32760; // above HUD/battle canvases, below nothing in practice.

        private RectTransform _spinner;
        private Image _spinnerImage;
        private TextMeshProUGUI _statusLabel;
        private CanvasGroup _canvasGroup;
        private RectTransform _progressFill;
        private GameObject _progressRoot;
        private float _spinSpeedDegPerSec = 220f;
        private bool _hiding;

        public static MatchLoadingOverlay Instance { get; private set; }

        /// <summary>Creates (or reuses) the overlay and shows it with an initial status line.</summary>
        public static MatchLoadingOverlay Show(string status = "Preparing battle...")
        {
            if (Instance != null)
            {
                Instance.SetStatus(status);
                return Instance;
            }

            var go = new GameObject("MatchLoadingOverlay");
            DontDestroyOnLoad(go);
            var overlay = go.AddComponent<MatchLoadingOverlay>();
            overlay.Build(status);
            Instance = overlay;
            return overlay;
        }

        /// <summary>Updates the short status line under the spinner. Safe to call every step.</summary>
        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = status ?? string.Empty;
            }
        }

        /// <summary>
        /// Sets a 0..1 determinate progress bar (revealed on first call). Pass a negative value to keep
        /// the bar hidden (pure spinner / indeterminate mode).
        /// </summary>
        public void SetProgress(float normalized)
        {
            if (_progressRoot == null)
            {
                return;
            }

            if (normalized < 0f)
            {
                _progressRoot.SetActive(false);
                return;
            }

            _progressRoot.SetActive(true);
            if (_progressFill != null)
            {
                _progressFill.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            }
        }

        /// <summary>Fades the overlay out and destroys it. Idempotent.</summary>
        public void Hide()
        {
            if (_hiding)
            {
                return;
            }
            _hiding = true;
            StartCoroutine(FadeOutAndDestroy());
        }

        /// <summary>Hides whatever overlay is up (no-op if none). Convenience for call sites.</summary>
        public static void HideCurrent()
        {
            if (Instance != null)
            {
                Instance.Hide();
            }
        }

        private void Build(string status)
        {
            // Own top-most overlay canvas so it draws above every battle/HUD canvas regardless of scene.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true; // swallow taps on the empty board while loading.

            // Dimmed themed backdrop.
            var backdrop = NewImage("Backdrop", transform);
            Fill(backdrop.rectTransform);
            backdrop.color = new Color(0.04f, 0.05f, 0.09f, 0.92f);

            // Central content column.
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(620f, 420f);

            // Procedural spinner ring (placeholder visual — no texture asset needed).
            var spinnerGo = new GameObject("Spinner", typeof(RectTransform));
            _spinner = (RectTransform)spinnerGo.transform;
            _spinner.SetParent(content, false);
            _spinner.anchorMin = _spinner.anchorMax = new Vector2(0.5f, 0.5f);
            _spinner.pivot = new Vector2(0.5f, 0.5f);
            _spinner.anchoredPosition = new Vector2(0f, 70f);
            _spinner.sizeDelta = new Vector2(150f, 150f);
            _spinnerImage = spinnerGo.AddComponent<Image>();
            _spinnerImage.sprite = BuildRingSprite();
            _spinnerImage.type = Image.Type.Filled;
            _spinnerImage.fillMethod = Image.FillMethod.Radial360;
            _spinnerImage.fillAmount = 0.75f; // partial ring reads as a spinner.
            _spinnerImage.color = new Color(0.45f, 0.78f, 1f, 1f);

            // Status label.
            _statusLabel = NewText("Status", content, status, 40f);
            var labelRt = _statusLabel.rectTransform;
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = new Vector2(0f, -60f);
            labelRt.sizeDelta = new Vector2(640f, 120f);

            // Optional progress bar (hidden until SetProgress(>=0) is called).
            _progressRoot = new GameObject("Progress", typeof(RectTransform));
            var progRt = (RectTransform)_progressRoot.transform;
            progRt.SetParent(content, false);
            progRt.anchorMin = progRt.anchorMax = new Vector2(0.5f, 0.5f);
            progRt.pivot = new Vector2(0.5f, 0.5f);
            progRt.anchoredPosition = new Vector2(0f, -150f);
            progRt.sizeDelta = new Vector2(460f, 16f);
            var progBg = _progressRoot.AddComponent<Image>();
            progBg.color = new Color(1f, 1f, 1f, 0.12f);
            var fillGo = NewImage("Fill", progRt);
            _progressFill = fillGo.rectTransform;
            _progressFill.anchorMin = new Vector2(0f, 0f);
            _progressFill.anchorMax = new Vector2(0f, 1f);
            _progressFill.pivot = new Vector2(0f, 0.5f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
            fillGo.color = new Color(0.45f, 0.78f, 1f, 1f);
            _progressRoot.SetActive(false);

            ApplyThemeFont();
            StartCoroutine(FadeIn());
        }

        private void Update()
        {
            if (_spinner != null && !_hiding)
            {
                _spinner.Rotate(0f, 0f, -_spinSpeedDegPerSec * Time.unscaledDeltaTime);
            }
        }

        private IEnumerator FadeIn()
        {
            const float dur = 0.18f;
            var t = 0f;
            while (t < dur && _canvasGroup != null)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutAndDestroy()
        {
            const float dur = 0.25f;
            var startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            var t = 0f;
            while (t < dur && _canvasGroup != null)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t / dur);
                yield return null;
            }
            if (Instance == this)
            {
                Instance = null;
            }
            Destroy(gameObject);
        }

        // === helpers ===

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<Image>();
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string content, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Themed Kenney font if the deck-builder skin is present; harmless no-op otherwise.
        private void ApplyThemeFont()
        {
            var font = KenneyUiSkin.ThemeFont;
            if (font != null && _statusLabel != null)
            {
                _statusLabel.font = font;
            }
        }

        // Procedural soft ring sprite (cached statically) so the spinner needs no imported texture.
        private static Sprite _ringSprite;

        private static Sprite BuildRingSprite()
        {
            if (_ringSprite != null)
            {
                return _ringSprite;
            }

            const int size = 128;
            var c = (size - 1) * 0.5f;
            var outer = c * 0.98f;
            var inner = c * 0.66f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - c;
                    var dy = y - c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (d > outer || d < inner)
                    {
                        a = 0f;
                    }
                    else
                    {
                        // Soft edges on the inner/outer rims.
                        var edge = Mathf.Min(d - inner, outer - d);
                        a = Mathf.Clamp01(edge / 3f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LoadingRing", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.name = "LoadingRing";
            return _ringSprite;
        }
    }
}
