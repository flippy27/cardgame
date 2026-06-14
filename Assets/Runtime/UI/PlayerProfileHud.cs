using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI.DeckBuilding; // KenneyUiSkin.ThemeFont (optional themed font)

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Main-menu player widget: a circular "Lv N" badge (top-right) that doubles as the PLAYER PROFILE
    /// button — tapping it opens an overlay with the player's level, XP bar and stats (W/L, win-rate,
    /// rating). Fully code-built on its own screen-space canvas (no scene/prefab wiring); created and
    /// owned by <see cref="MatchmakingPanelController"/> so it only lives on the menu. Data comes from
    /// the live endpoints via <see cref="ProgressApiClient"/> + <see cref="UserApiClient"/>.
    /// </summary>
    public sealed class PlayerProfileHud : MonoBehaviour
    {
        private AuthService _auth;
        private readonly ProgressApiClient _progressApi = new ProgressApiClient();
        private readonly UserApiClient _userApi = new UserApiClient();

        private TextMeshProUGUI _circleLevelText;
        private GameObject _overlay;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _levelText;
        private Image _xpFill;
        private TextMeshProUGUI _xpText;
        private TextMeshProUGUI _statsText;

        private static Sprite _circleSprite;

        /// <summary>Creates the HUD (own canvas) and kicks off the first data refresh.</summary>
        public static PlayerProfileHud Create(AuthService auth)
        {
            var go = new GameObject("PlayerProfileHud");
            var hud = go.AddComponent<PlayerProfileHud>();
            hud._auth = auth;
            hud.Build();
            hud.Refresh();
            return hud;
        }

        private void Build()
        {
            // Own top-most overlay canvas so it doesn't depend on the scene's canvas layout.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildLevelCircle((RectTransform)transform);
            BuildOverlay((RectTransform)transform);
        }

        // The circular "Lv N" badge in the top-right — also the profile button.
        private void BuildLevelCircle(RectTransform root)
        {
            var btnGo = new GameObject("LevelCircle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)btnGo.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta = new Vector2(120f, 120f);

            var img = btnGo.GetComponent<Image>();
            img.sprite = CircleSprite();
            img.type = Image.Type.Simple;
            img.color = new Color(0.12f, 0.14f, 0.20f, 0.96f);

            // Gold ring
            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.SetParent(rt, false);
            Fill(ringRt);
            var ring = ringGo.GetComponent<Image>();
            ring.sprite = CircleSprite();
            ring.color = new Color(0.95f, 0.80f, 0.30f, 1f);
            ring.raycastTarget = false;
            // inner darker fill on top of the ring so only a rim shows
            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var innerRt = (RectTransform)innerGo.transform;
            innerRt.SetParent(rt, false);
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerRt.pivot = new Vector2(0.5f, 0.5f);
            innerRt.sizeDelta = new Vector2(102f, 102f);
            var inner = innerGo.GetComponent<Image>();
            inner.sprite = CircleSprite();
            inner.color = new Color(0.12f, 0.14f, 0.20f, 1f);
            inner.raycastTarget = false;

            _circleLevelText = MakeText(rt, "Lv\n—", 30f, TextAlignmentOptions.Center);
            Fill((RectTransform)_circleLevelText.transform);
            _circleLevelText.raycastTarget = false;

            btnGo.GetComponent<Button>().onClick.AddListener(ToggleOverlay);
        }

        private void BuildOverlay(RectTransform root)
        {
            _overlay = new GameObject("ProfileOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)_overlay.transform;
            rt.SetParent(root, false);
            Fill(rt);
            _overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // dim backdrop
            _overlay.GetComponent<Button>().onClick.AddListener(() => SetOverlayVisible(false)); // tap-out closes

            // Panel
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var prt = (RectTransform)panelGo.transform;
            prt.SetParent(rt, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(820f, 900f);
            panelGo.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f, 0.98f);

            MakeText(prt, "PLAYER PROFILE", 46f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 70f));

            _nameText = MakeText(prt, "—", 40f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(760f, 56f));
            _levelText = MakeText(prt, "Level —", 56f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(760f, 80f));

            // XP bar
            var trackGo = new GameObject("XpTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var trackRt = (RectTransform)trackGo.transform;
            trackRt.SetParent(prt, false);
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 1f);
            trackRt.pivot = new Vector2(0.5f, 1f);
            trackRt.anchoredPosition = new Vector2(0f, -360f);
            trackRt.sizeDelta = new Vector2(680f, 44f);
            trackGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var fillGo = new GameObject("XpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.SetParent(trackRt, false);
            Fill(fillRt);
            _xpFill = fillGo.GetComponent<Image>();
            _xpFill.color = new Color(0.30f, 0.85f, 1f, 1f);
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            _xpFill.fillOrigin = 0;
            _xpFill.fillAmount = 0f;
            _xpFill.sprite = SolidSprite(); // Filled needs a sprite to clip cleanly

            _xpText = MakeText(trackRt, "— / — XP", 26f, TextAlignmentOptions.Center);
            Fill((RectTransform)_xpText.transform);
            _xpText.raycastTarget = false;

            _statsText = MakeText(prt, "", 34f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(760f, 260f));

            // Close button
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var crt = (RectTransform)closeGo.transform;
            crt.SetParent(prt, false);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 40f);
            crt.sizeDelta = new Vector2(320f, 96f);
            closeGo.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.32f, 1f);
            var closeLabel = MakeText(crt, "Close", 36f, TextAlignmentOptions.Center);
            Fill((RectTransform)closeLabel.transform);
            closeLabel.raycastTarget = false;
            closeGo.GetComponent<Button>().onClick.AddListener(() => SetOverlayVisible(false));

            _overlay.SetActive(false);
        }

        private void ToggleOverlay()
        {
            var show = _overlay != null && !_overlay.activeSelf;
            SetOverlayVisible(show);
            if (show)
            {
                Refresh(); // pull fresh data when opening
            }
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlay != null)
            {
                _overlay.SetActive(visible);
            }
        }

        public async void Refresh()
        {
            var playerId = _auth?.CurrentPlayerId;
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            // Progress (level + XP) — drives the circle and the bar.
            try
            {
                var p = await _progressApi.GetProgress(playerId);
                if (p != null && this != null)
                {
                    if (_circleLevelText != null) _circleLevelText.text = $"Lv\n{p.level}";
                    if (_levelText != null) _levelText.text = $"Level {p.level}";
                    if (_xpFill != null) _xpFill.fillAmount = p.xpForNextLevel > 0 ? Mathf.Clamp01((float)p.xpIntoLevel / p.xpForNextLevel) : 1f;
                    if (_xpText != null) _xpText.text = p.xpForNextLevel > 0 ? $"{p.xpIntoLevel} / {p.xpForNextLevel} XP" : "MAX";
                }
            }
            catch (System.Exception e) { GameLogger.Warning("PlayerProfileHud", $"progress fetch failed: {e.Message}"); }

            // Name + stats (best-effort; the overlay still works without them).
            try
            {
                var profile = await _userApi.GetProfile(playerId);
                if (profile != null && _nameText != null && this != null)
                {
                    _nameText.text = string.IsNullOrWhiteSpace(profile.username) ? playerId : profile.username;
                }
            }
            catch (System.Exception e) { GameLogger.Warning("PlayerProfileHud", $"profile fetch failed: {e.Message}"); }

            try
            {
                var stats = await _userApi.GetStats(playerId);
                if (stats != null && _statsText != null && this != null)
                {
                    var winRate = Mathf.RoundToInt(stats.winRate * 100f);
                    _statsText.text =
                        $"Record:  <b>{stats.wins}W - {stats.losses}L</b>\n" +
                        $"Win rate:  <b>{winRate}%</b>\n" +
                        $"Games:  <b>{stats.totalGames}</b>\n" +
                        $"Rating:  <b>{stats.rating}</b>";
                }
            }
            catch (System.Exception e) { GameLogger.Warning("PlayerProfileHud", $"stats fetch failed: {e.Message}"); }
        }

        // ---- helpers ----

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, string text, float size, TextAlignmentOptions align,
            Vector2? anchor = null, Vector2? pos = null, Vector2? sizeDelta = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (anchor.HasValue)
            {
                rt.anchorMin = rt.anchorMax = anchor.Value;
                rt.pivot = new Vector2(0.5f, anchor.Value.y);
                rt.anchoredPosition = pos ?? Vector2.zero;
                rt.sizeDelta = sizeDelta ?? new Vector2(400f, 60f);
            }
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (KenneyUiSkin.ThemeFont != null) t.font = KenneyUiSkin.ThemeFont;
            return t;
        }

        private static Sprite _solidSprite;

        // 4x4 solid white sprite (cached) for the XP fill / track so Image.Type.Filled clips cleanly.
        private static Sprite SolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            const int s = 4;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _solidSprite;
        }

        // Small procedural white circle sprite (cached) so the badge needs no imported texture.
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var c = (s - 1) * 0.5f;
            var r = c;
            var px = new Color32[s * s];
            for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
            {
                var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                var a = Mathf.Clamp01(r - d); // 1px soft edge
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }
    }
}
