using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.UI.DeckBuilding; // KenneyUiSkin (optional themed skin/font)

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// DIAGNOSTIC: a standalone shader-effects test view that shows ONE card on a 3D quad and lets you
    /// fire every card-surface effect (Shield / Glow / Dissolve / Freeze / Burn / Holo / Outline) at it
    /// in isolation, driving <see cref="CardEffectController"/> DIRECTLY (the controller is not gated;
    /// only the in-match Card3DPlayed path is). The card surface effects are currently DISABLED in a
    /// real match because of a material-binding bug that blanks the card when the shader is swapped.
    /// This view exists to SEE whether the effect shader renders the card correctly (with its composite
    /// + 180deg flip + alpha-clip carried across) or blanks it — so the binding bug can be verified and
    /// debugged before re-enabling the effects in battle.
    ///
    /// Fully code-built / self-contained (no scene or prefab wiring), the same "no SerializeField that
    /// won't be wired" pattern used by <see cref="PlayerProfileHud"/>. Created by, and owned by, the
    /// main-menu controller via the "FX Test" entry button. While open it:
    ///   - builds a 2:3 quad whose material replicates the real card setup (URP/Lit -> Standard
    ///     fallback, _BaseMap/_MainTex = the card composite, tiling (-1,-1)/offset (1,1) flip, alpha-clip
    ///     _Cutoff 0.5) so the card reads upright exactly like in battle;
    ///   - renders it with a DEDICATED camera (high depth, solid clear colour) so the card draws cleanly;
    ///   - HIDES the menu (deactivates the menu root + the profile HUD canvas) so menu overlay UI does
    ///     not bleed through, and restores everything on Back.
    /// Nothing in CardEffectController / the shader / Card3DView / CardArtLibrary is modified.
    /// </summary>
    public sealed class EffectTestView : MonoBehaviour
    {
        // A representative sample card: ember unit / legendary. The composite resolves from per-TYPE art
        // (Resources/Art/cardart_type/unit.png) + the faction frame even when no per-card art exists, so
        // this always produces a real card surface to test the effects against.
        private const string SampleCardId = "ember_0001";
        private const int SampleCardType = 0;    // CardType.Unit
        private const int SampleCardRarity = 3;  // CardRarity.Legendary
        private const int SampleCardFaction = 0; // CardFaction.Ember
        private const int SampleUnitType = 0;    // UnitType.Melee

        private GameObject[] _menuRootsToRestore;
        private Camera _camera;
        private GameObject _cardRoot;
        private CardEffectController _effects;
        private TextMeshProUGUI _activeLabel;

        private CardEffectController.CardEffect _sustainedActive = CardEffectController.CardEffect.None;

        /// <summary>
        /// Opens the test view. <paramref name="menuRootsToHide"/> are the menu GameObjects (menu canvas
        /// root, profile HUD, etc.) to deactivate while the view is open; they are reactivated on Back.
        /// </summary>
        public static EffectTestView Open(params GameObject[] menuRootsToHide)
        {
            var go = new GameObject("EffectTestView");
            var view = go.AddComponent<EffectTestView>();
            view._menuRootsToRestore = menuRootsToHide;
            view.Build();
            return view;
        }

        private void Build()
        {
            HideMenu(true);
            BuildCamera();
            BuildCard();
            BuildUi();
            SetActiveLabel(CardEffectController.CardEffect.None);
        }

        // ---- menu hide/restore -------------------------------------------------

        private void HideMenu(bool hide)
        {
            if (_menuRootsToRestore == null)
            {
                return;
            }
            foreach (var root in _menuRootsToRestore)
            {
                if (root != null)
                {
                    root.SetActive(!hide);
                }
            }
        }

        // ---- dedicated camera --------------------------------------------------

        private void BuildCamera()
        {
            var camGo = new GameObject("FxTestCamera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f); // dark slate so the card pops
            _camera.orthographic = false;
            _camera.fieldOfView = 35f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            _camera.depth = 100; // draw over the (now-hidden) menu camera regardless
            // Look straight at the card placed at the origin.
            camGo.transform.position = new Vector3(0f, 0f, -4f);
            camGo.transform.rotation = Quaternion.identity;
        }

        // ---- the card quad -----------------------------------------------------

        private void BuildCard()
        {
            // 2:3 hand-card aspect, placed at the origin facing the camera.
            _cardRoot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _cardRoot.name = "FxTestCard";
            Destroy(_cardRoot.GetComponent<Collider>()); // no interaction needed
            _cardRoot.transform.SetParent(transform, false);
            _cardRoot.transform.position = Vector3.zero;
            _cardRoot.transform.localScale = new Vector3(2f, 3f, 1f); // 2:3

            var renderer = _cardRoot.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Replicate the real card material: prefer URP/Lit, fall back to Standard (same fallback the
            // real card path uses via Shader.Find("Standard")), then bind the composite with the same
            // flip + alpha-clip as CardVisualLayerBinding so the card reads upright.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = Color.white };
            renderer.material = material;

            ApplyComposite(material);

            // Attach the controller and bind the quad renderer — this is the SAME entry point the battle
            // code uses (GetOrAdd(go, renderer)). We then drive it directly from the buttons.
            _effects = CardEffectController.GetOrAdd(_cardRoot, renderer);
        }

        // Mirrors CardVisualLayerBinding.SetMaterialTexture: set the composite on _BaseMap/_MainTex,
        // force white, enable alpha-clip (_Cutoff 0.5) so the transparent-outside-frame composite renders
        // the card silhouette instead of a black quad, and apply the 180deg flip via tiling/offset.
        private void ApplyComposite(Material material)
        {
            var composite = CardArtLibrary.GetCardComposite(
                SampleCardId, SampleCardType, SampleCardRarity, SampleCardFaction, SampleUnitType,
                hasArmor: false, surface: "hand");
            var texture = composite != null ? composite.texture : null;

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 1f);
                material.EnableKeyword("_ALPHATEST_ON");
            }
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            // 180deg in-plane flip so the art reads upright on the quad (same as the 3D card path).
            var flipScale = new Vector2(-1f, -1f);
            var flipOffset = new Vector2(1f, 1f);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", flipScale);
                material.SetTextureOffset("_BaseMap", flipOffset);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", flipScale);
                material.SetTextureOffset("_MainTex", flipOffset);
            }
        }

        // ---- screen-space overlay UI ------------------------------------------

        private void BuildUi()
        {
            var canvasGo = new GameObject("FxTestCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // Title + active-effect label across the top.
            MakeText(root, "SHADER FX TEST", 52f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 70f));
            _activeLabel = MakeText(root, "Active: None", 38f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 56f));

            // Effect buttons along the bottom, in two rows. One-shots use Play(...); the rest toggle
            // SetSustained(...). Clear = ClearAll(); Back closes the view + restores the menu.
            var labels = new (string label, System.Action action, KenneyUiSkin.ButtonStyle style)[]
            {
                ("Shield",  () => ToggleSustained(CardEffectController.CardEffect.Shield),  KenneyUiSkin.ButtonStyle.Primary),
                ("Glow",    () => PlayOneShot(CardEffectController.CardEffect.Glow),         KenneyUiSkin.ButtonStyle.Primary),
                ("Dissolve",() => PlayOneShot(CardEffectController.CardEffect.Dissolve),     KenneyUiSkin.ButtonStyle.Primary),
                ("Freeze",  () => ToggleSustained(CardEffectController.CardEffect.Freeze),   KenneyUiSkin.ButtonStyle.Primary),
                ("Burn",    () => ToggleSustained(CardEffectController.CardEffect.Burn),     KenneyUiSkin.ButtonStyle.Primary),
                ("Holo",    () => ToggleSustained(CardEffectController.CardEffect.Holo),     KenneyUiSkin.ButtonStyle.Primary),
                ("Outline", () => ToggleSustained(CardEffectController.CardEffect.Outline),  KenneyUiSkin.ButtonStyle.Primary),
                ("Clear",   ClearEffects,                                                    KenneyUiSkin.ButtonStyle.Nav),
                ("Back",    Close,                                                           KenneyUiSkin.ButtonStyle.Nav),
            };

            // Grid: a horizontal layout per row, stacked from the bottom.
            const int perRow = 5;
            const float btnW = 200f;
            const float btnH = 96f;
            const float gap = 16f;
            var rowCount = Mathf.CeilToInt(labels.Length / (float)perRow);

            for (var i = 0; i < labels.Length; i++)
            {
                var row = i / perRow;
                var col = i % perRow;
                var itemsThisRow = Mathf.Min(perRow, labels.Length - row * perRow);
                var rowWidth = itemsThisRow * btnW + (itemsThisRow - 1) * gap;
                var startX = -rowWidth * 0.5f + btnW * 0.5f;
                var x = startX + col * (btnW + gap);
                // bottom-up: row 0 is the lowest row.
                var y = 60f + (rowCount - 1 - row) * (btnH + gap);

                MakeButton(root, labels[i].label, labels[i].action, labels[i].style,
                    new Vector2(x, y), new Vector2(btnW, btnH));
            }
        }

        // ---- effect driving (DIRECT — the controller is not gated) -------------

        private void PlayOneShot(CardEffectController.CardEffect effect)
        {
            if (_effects == null) return;
            // Dissolve gets a longer duration so the phase sweep is visible; Glow uses its default.
            var duration = effect == CardEffectController.CardEffect.Dissolve ? 1.5f : 0.8f;
            _effects.Play(effect, color: null, duration: duration, intensity: 1f);
            SetActiveLabel(effect, oneShot: true);
        }

        private void ToggleSustained(CardEffectController.CardEffect effect)
        {
            if (_effects == null) return;
            // Toggle: if this sustained effect is already on, turn it off; otherwise switch to it
            // (turning off whatever sustained effect was previously on).
            if (_sustainedActive == effect)
            {
                _effects.SetSustained(effect, false);
                _sustainedActive = CardEffectController.CardEffect.None;
            }
            else
            {
                if (_sustainedActive != CardEffectController.CardEffect.None)
                {
                    _effects.SetSustained(_sustainedActive, false);
                }
                _effects.SetSustained(effect, true);
                _sustainedActive = effect;
            }
            SetActiveLabel(_sustainedActive);
        }

        private void ClearEffects()
        {
            if (_effects != null)
            {
                _effects.ClearAll();
            }
            _sustainedActive = CardEffectController.CardEffect.None;
            SetActiveLabel(CardEffectController.CardEffect.None);
        }

        private void SetActiveLabel(CardEffectController.CardEffect effect, bool oneShot = false)
        {
            if (_activeLabel == null) return;
            if (effect == CardEffectController.CardEffect.None)
            {
                _activeLabel.text = "Active: None";
            }
            else
            {
                _activeLabel.text = oneShot ? $"Active: {effect} (one-shot)" : $"Active: {effect}";
            }
        }

        private void Close()
        {
            HideMenu(false); // restore menu + profile HUD
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Defensive: if destroyed without Close() (scene change), make sure the menu is restored.
            HideMenu(false);
        }

        // ---- UI helpers (mirror PlayerProfileHud's code-built widgets) ---------

        private static TextMeshProUGUI MakeText(RectTransform parent, string text, float size,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (KenneyUiSkin.ThemeFont != null) t.font = KenneyUiSkin.ThemeFont;
            return t;
        }

        private static void MakeButton(RectTransform parent, string label, System.Action onClick,
            KenneyUiSkin.ButtonStyle style, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(label + "Button",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); // bottom-centre origin
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = style == KenneyUiSkin.ButtonStyle.Primary
                ? new Color(0.18f, 0.34f, 0.58f, 1f)
                : new Color(0.22f, 0.24f, 0.30f, 1f);

            var labelText = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)labelText.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var t = labelText.GetComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 34f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            if (KenneyUiSkin.ThemeFont != null) t.font = KenneyUiSkin.ThemeFont;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());

            // Apply the Kenney skin if the sprites are present (null-safe; leaves the flat colour otherwise).
            KenneyUiSkin.SkinButton(button, style);
        }
    }
}
