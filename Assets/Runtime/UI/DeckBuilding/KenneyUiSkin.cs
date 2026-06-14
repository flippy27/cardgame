using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Self-contained, code-side Kenney UI re-skin for the deck-builder screens
    /// (TEMPORARY art pass — see <c>Assets/Art/KenneyUI/WIRING-PLAN.md</c>).
    ///
    /// Why this exists: the deck-builder panels/buttons are flat white default
    /// UISprites. Rather than wire sprites onto every Image in the scene/prefabs
    /// by hand in the editor, each screen calls into this helper from its
    /// Awake/Start to apply the Kenney 9-slice panel + button sprites in code —
    /// the same "no prefab wiring" pattern used by
    /// <see cref="Flippy.CardDuelMobile.UI.SinglePlayerDeckSelectOverlay"/>.
    ///
    /// Runtime sprite loading uses <see cref="Resources.Load{T}"/>, so the handful
    /// of sprites we reference must live under a <c>Resources/</c> folder. The
    /// raw Kenney pack sits at <c>Assets/Art/KenneyUI/</c> which is NOT a Resources
    /// folder, so it cannot be loaded at runtime as-is. To enable the skin, copy
    /// (or move) the referenced PNGs into <c>Assets/Resources/Art/KenneyUI/</c>
    /// preserving the sub-paths below (and import them as Sprite (2D and UI) with
    /// 9-slice borders). See the MANUAL STEP note in WIRING-PLAN.md.
    ///
    /// Everything here is null-safe: if a sprite is missing the targets are left
    /// untouched, so the UI keeps working (just unskinned) when the assets are
    /// absent. No logic, layout, or text content is changed.
    /// </summary>
    public static class KenneyUiSkin
    {
        // Resources-relative paths (no extension). Update these if the sprites are
        // copied under a different sub-path. The loader tries each candidate root
        // in order, so both "Art/KenneyUI/..." and a flatter "KenneyUI/..." layout
        // work without code changes.
        private static readonly string[] ResourceRoots =
        {
            "Art/KenneyUI/",
            "KenneyUI/",
        };

        // Logical sprite keys → file path (relative to a resource root).
        private const string PanelWindow   = "RPGExpansion/PNG/panel_brown";
        private const string PanelInset    = "RPGExpansion/PNG/panelInset_beige";
        private const string ButtonPrimary = "PNG/Blue/Default/button_rectangle_depth_gloss";
        private const string ButtonPrimaryPressed = "PNG/Blue/Default/button_rectangle_gloss";
        private const string ButtonNav     = "PNG/Grey/Default/button_rectangle_depth_flat";
        private const string ButtonNavPressed = "PNG/Grey/Default/button_rectangle_flat";
        private const string ButtonIcon    = "PNG/Grey/Default/button_square_depth_flat";
        private const string ButtonIconPressed = "PNG/Grey/Default/button_square_flat";
        private const string InputField    = "PNG/Extra/Default/input_rectangle";

        // Bottom-tab segmented look. Selected tab uses the GREEN flat button (raised/active),
        // unselected uses the GREY flat button (recessed). These reuse the existing Kenney pack —
        // the Green sprites must live under Resources/Art/KenneyUI/PNG/Green/Default/ (copied from
        // the raw pack at Assets/Art/KenneyUI/PNG/Green/Default/). Null-safe if absent (the tab bar
        // falls back to a flat colour tint).
        private const string TabSelected   = "PNG/Green/Default/button_rectangle_depth_flat";
        private const string TabUnselected = "PNG/Grey/Default/button_rectangle_flat";

        private static readonly Dictionary<string, Sprite> _cache = new();
        private static bool _availabilityChecked;
        private static bool _available;

        /// <summary>Style intent for a skinned button.</summary>
        public enum ButtonStyle
        {
            /// <summary>Primary call-to-action (Save / Create / Craft / Add). Blue.</summary>
            Primary,
            /// <summary>Secondary / navigation (Decks / Catalog / Back / Close). Grey.</summary>
            Nav,
            /// <summary>Small square icon button (page arrows, remove-card). Grey square.</summary>
            Icon,
        }

        /// <summary>
        /// True once we have confirmed at least the main panel sprite resolves.
        /// Lets callers cheaply skip skinning work when the assets aren't present.
        /// </summary>
        public static bool Available
        {
            get
            {
                if (!_availabilityChecked)
                {
                    _availabilityChecked = true;
                    _available = Load(PanelWindow) != null;
                    // TEMP DIAGNOSTIC: the skin "looks the same" — confirm whether the sprites resolve
                    // under Resources at all. If false, the PNGs aren't imported/located as expected.
                    Debug.Log($"[KENNEYDBG] Available={_available} — panel sprite '{PanelWindow}' " +
                              $"{(_available ? "loaded OK" : "NOT FOUND under Resources/Art/KenneyUI/ or Resources/KenneyUI/")}");
                }
                return _available;
            }
        }

        // ---- Panels -------------------------------------------------------------

        /// <summary>
        /// Skins a window/panel root: assigns the brown panel sprite as a sliced
        /// background and clears any leftover tint so the sprite reads true.
        /// Pass the panel's own <see cref="Component"/> (e.g. <c>this</c>); we look
        /// for an <see cref="Image"/> on the same GameObject.
        /// </summary>
        public static void SkinPanelWindow(Component panelRoot)
        {
            if (panelRoot == null) return;
            ApplySliced(panelRoot.GetComponent<Image>(), Load(PanelWindow));
        }

        /// <summary>Skins a scroll-view / inset background with the beige inset sprite.</summary>
        public static void SkinPanelInset(Component target)
        {
            if (target == null) return;
            ApplySliced(target.GetComponent<Image>(), Load(PanelInset));
        }

        /// <summary>Skins an arbitrary <see cref="Image"/> as a sliced panel inset.</summary>
        public static void SkinInsetImage(Image image)
        {
            ApplySliced(image, Load(PanelInset));
        }

        /// <summary>Skins an <see cref="Image"/> as a sliced input-field background.</summary>
        public static void SkinInputImage(Image image)
        {
            ApplySliced(image, Load(InputField));
        }

        // ---- Buttons ------------------------------------------------------------

        /// <summary>
        /// Skins a single button: sets its target-graphic sprite (sliced) and wires
        /// a sprite-swap pressed/highlighted transition. Null-safe.
        /// </summary>
        public static void SkinButton(Button button, ButtonStyle style = ButtonStyle.Nav)
        {
            if (button == null) return;

            var img = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (img == null) return;

            var (normal, pressed) = SpritesFor(style);
            if (normal == null) return;

            ApplySliced(img, normal);

            // Sprite-swap transition (keeps the button readable when pressed).
            button.transition = Selectable.Transition.SpriteSwap;
            var ss = button.spriteState;
            ss.pressedSprite = pressed != null ? pressed : normal;
            ss.highlightedSprite = normal;
            ss.selectedSprite = normal;
            ss.disabledSprite = normal;
            button.spriteState = ss;

            // Size the caption for phone legibility (button labels were authored ~24pt = tiny).
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) DeckBuilderTextScale.ApplyAutoSize(label, DeckBuilderTextScale.Role.Button);
        }

        /// <summary>
        /// Skins every <see cref="Button"/> under <paramref name="root"/> with the
        /// same style. Use for action bars / panels where all buttons share a role.
        /// </summary>
        public static void SkinButtonsUnder(Component root, ButtonStyle style = ButtonStyle.Nav)
        {
            if (root == null) return;
            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                SkinButton(btn, style);
            }
        }

        /// <summary>Skins a button AND sets its TMP label text. Use to give buttons that
        /// only show their GameObject name a real caption. Null-safe.</summary>
        public static void SkinButtonWithLabel(Button button, ButtonStyle style, string label)
        {
            SkinButton(button, style);
            if (button == null) return;
            var t = button.GetComponentInChildren<TMP_Text>(true);
            if (t != null && !string.IsNullOrEmpty(label)) t.text = label;
        }

        // ---- Window / panel backdrops ------------------------------------------

        /// <summary>
        /// Ensures a screen/panel root shows the Kenney brown window panel as a full-rect
        /// background. If the root already has an <see cref="Image"/> it is skinned in place;
        /// otherwise a stretched backdrop child Image is created BEHIND all other children
        /// (so the existing layout sits on top of a framed window). Idempotent / null-safe.
        /// </summary>
        public static void EnsureWindowBackdrop(Component root) => EnsureBackdrop(root, PanelWindow, raycast: false, preferFullScreen: true);

        /// <summary>Like <see cref="EnsureWindowBackdrop"/> but uses the lighter beige inset
        /// sprite — for grouping a sub-area (filter bar, pagination, scroll viewport).</summary>
        public static void EnsureInsetBackdrop(Component root) => EnsureBackdrop(root, PanelInset, raycast: false, preferFullScreen: false);

        private static void EnsureBackdrop(Component root, string spriteKey, bool raycast, bool preferFullScreen)
        {
            if (root == null) return;
            var sprite = Load(spriteKey);
            if (sprite == null) return;

            // If the root carries its own Image, skin that directly.
            var ownImage = root.GetComponent<Image>();
            if (ownImage != null) { ApplySliced(ownImage, sprite); return; }

            if (!(root.transform is RectTransform rt)) return;

            // The owner may be a tiny logical container (the deck-builder controller root is a
            // 100x100 RectTransform anchored centre), so a stretched child would be invisible.
            // For a window backdrop, anchor to the root Canvas when the owner's own rect is too
            // small to act as a full window.
            RectTransform parent = rt;
            if (preferFullScreen)
            {
                var size = rt.rect;
                if (size.width < 300f || size.height < 300f)
                {
                    var canvas = root.GetComponentInParent<Canvas>();
                    if (canvas != null) parent = (RectTransform)canvas.rootCanvas.transform;
                }
            }

            var existing = parent.Find("KenneySkin_Backdrop");
            Image img;
            if (existing != null)
            {
                img = existing.GetComponent<Image>();
            }
            else
            {
                var go = new GameObject("KenneySkin_Backdrop", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                var brt = go.GetComponent<RectTransform>();
                brt.SetParent(parent, false);
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                brt.SetSiblingIndex(0); // draw behind the existing children
                // Never let a parent layout group move/size the backdrop — it must stay full-rect.
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                img = go.GetComponent<Image>();
            }
            if (img != null) img.raycastTarget = raycast;
            ApplySliced(img, sprite);
        }

        // ---- Controls (dropdown / toggle / scrollbar / input) -------------------

        /// <summary>Skins a TMP dropdown: the closed control gets the input sprite, the
        /// popup template + its scrollbar get the inset/nav sprites. Null-safe.</summary>
        public static void SkinDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null) return;
            ApplySliced((dropdown.targetGraphic as Image) ?? dropdown.GetComponent<Image>(), Load(InputField));

            if (dropdown.template != null)
            {
                ApplySliced(dropdown.template.GetComponent<Image>(), Load(PanelInset));
                SkinScrollbarsUnder(dropdown.template);
            }
            if (dropdown.itemText != null && dropdown.itemText.transform.parent != null)
            {
                // The "Item" toggle's own graphic reads as each row's background.
                var itemToggle = dropdown.itemText.GetComponentInParent<Toggle>();
                if (itemToggle != null) ApplySliced(itemToggle.targetGraphic as Image, Load(InputField));
                DeckBuilderTextScale.ApplyAutoSize(dropdown.itemText, DeckBuilderTextScale.Role.Label);
            }
            // The closed-state caption text was authored ~14pt — size it for the phone, and keep it on
            // a single line (the narrow dropdown otherwise wraps "All Rarities" into a stacked mess).
            if (dropdown.captionText != null)
            {
                DeckBuilderTextScale.ApplyAutoSize(dropdown.captionText, DeckBuilderTextScale.Role.Label);
                dropdown.captionText.enableWordWrapping = false;
                dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>Skins a toggle's background box with the nav-button sprite. Null-safe.</summary>
        public static void SkinToggle(Toggle toggle)
        {
            if (toggle == null) return;
            ApplySliced(toggle.targetGraphic as Image, Load(ButtonNav));
        }

        /// <summary>
        /// Skins a bottom-tab button to reflect its selected state: selected tabs get the green
        /// "active" sprite, unselected the grey "recessed" sprite. Falls back to a flat colour tint
        /// when the sprites aren't present so the selected tab still reads. Null-safe.
        /// </summary>
        public static void SkinTab(Image image, bool selected)
        {
            if (image == null) return;
            var sprite = Load(selected ? TabSelected : TabUnselected);
            if (sprite != null)
            {
                ApplySliced(image, sprite);
            }
            else
            {
                // No art — at least make the selected tab visually distinct.
                image.sprite = null;
                image.color = selected ? new Color(0.20f, 0.55f, 0.28f, 1f) : new Color(0.22f, 0.23f, 0.28f, 1f);
            }
        }

        /// <summary>Skins every scrollbar under root (track = inset, handle = nav). Null-safe.</summary>
        public static void SkinScrollbarsUnder(Component root)
        {
            if (root == null) return;
            foreach (var sb in root.GetComponentsInChildren<Scrollbar>(true))
            {
                ApplySliced(sb.GetComponent<Image>(), Load(PanelInset));
                if (sb.handleRect != null) ApplySliced(sb.handleRect.GetComponent<Image>(), Load(ButtonNav));
            }
        }

        // ---- Theme font (Kenney, built at runtime — no editor baking) -----------

        /// <summary>When false, deck-builder text keeps TMP's default font (LiberationSans). The
        /// Kenney Future TTFs render some glyphs wrong, so the themed font is off by default.</summary>
        public const bool UseThemeFont = false;

        private static bool _fontChecked;
        private static TMP_FontAsset _themeFont;

        /// <summary>
        /// A dynamic TMP font asset built once from the Kenney Future Narrow TTF under
        /// Resources/Art/KenneyUI/Font/. No editor font-asset baking required. Returns null
        /// (callers keep the default font) if the TTF isn't present or creation fails.
        /// </summary>
        public static TMP_FontAsset ThemeFont
        {
            get
            {
                if (_fontChecked) return _themeFont;
                _fontChecked = true;

                // Disabled: the Kenney Future fonts render some glyphs wrong (X→H, very condensed).
                // Keep TMP's clean default (LiberationSans) — ApplyFontUnder still enforces the size
                // floor. Flip this to re-enable a themed font once a clean TTF is available.
                if (!UseThemeFont) { Debug.Log("[KENNEYDBG] ThemeFont disabled (using default font)"); return null; }

                Font ttf = null;
                foreach (var root in ResourceRoots)
                {
                    ttf = Resources.Load<Font>(root + "Font/KenneyFutureNarrow")
                          ?? Resources.Load<Font>(root + "Font/KenneyFuture");
                    if (ttf != null) break;
                }
                if (ttf != null)
                {
                    try { _themeFont = TMP_FontAsset.CreateFontAsset(ttf); }
                    catch (System.Exception e) { Debug.LogWarning($"[KENNEYDBG] ThemeFont create failed: {e.Message}"); }
                }
                Debug.Log($"[KENNEYDBG] ThemeFont={(_themeFont != null ? "OK" : "null (default font kept)")}");
                return _themeFont;
            }
        }

        /// <summary>
        /// Applies the Kenney theme font to every TMP_Text under root AND enforces a phone
        /// readability floor (<see cref="DeckBuilderTextScale.MinReadable"/>) so text that bypasses
        /// the role-based sizing (button captions, dropdown items, placeholders authored at 14-24pt)
        /// never renders tiny on a handset. Null-safe.
        /// </summary>
        public static void ApplyFontUnder(Component root)
        {
            if (root == null) return;
            var font = ThemeFont; // may be null — still apply the size floor below
            float floor = DeckBuilderTextScale.MinReadable;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null) continue;
                if (font != null) t.font = font;
                if (t.enableAutoSizing)
                {
                    if (t.fontSizeMin < floor) t.fontSizeMin = floor;
                    if (t.fontSizeMax < floor + 8f) t.fontSizeMax = floor + 8f;
                }
                else if (t.fontSize < floor)
                {
                    t.fontSize = floor;
                }
            }
        }

        // ---- Rect helpers (fix broken prefab layouts in code) ------------------

        /// <summary>Sets a child's RectTransform anchors/pivot/pos/size in one call. Null-safe.</summary>
        public static void SetRect(Component c, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            if (c == null || !(c.transform is RectTransform rt)) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        /// <summary>Stretches a child to fill its parent with per-edge insets (left,right,top,bottom). Null-safe.</summary>
        public static void Fill(Component c, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            if (c == null || !(c.transform is RectTransform rt)) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Sets/updates a LayoutElement on a component for horizontal/vertical layout groups. Null-safe.</summary>
        public static void SetLayoutElement(Component c, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            if (c == null) return;
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            le.flexibleHeight = flexibleHeight;
        }

        // ---- Layout ------------------------------------------------------------

        /// <summary>
        /// Ensures a scroll-list content transform lays its children out vertically
        /// (no overlap), with sensible spacing/padding, and grows to fit its children.
        /// Idempotent — safe to call every time the list is (re)built. Null-safe.
        ///
        /// This fixes the "crafting recipe rows overlap" problem when the prefab/scene
        /// forgot to put a <see cref="VerticalLayoutGroup"/> on the container.
        /// </summary>
        public static void EnsureVerticalList(Component container, float spacing = 12f, int padding = 12)
        {
            if (container == null) return;

            // Unity forbids two LayoutGroups on one GameObject — remove any GridLayoutGroup immediately
            // (a deferred Destroy still blocks AddComponent<VerticalLayoutGroup> this frame).
            var grid = container.GetComponent<GridLayoutGroup>();
            if (grid != null) UnityEngine.Object.DestroyImmediate(grid);

            var layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            if (layout == null) return;
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            // Stretch width so rows fill the viewport; never force-expand height (rows
            // keep their own preferred height → no squashing/overlap).
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            EnsureVerticalFitter(container);
        }

        /// <summary>
        /// Ensures a grid content transform uses a <see cref="GridLayoutGroup"/> with a
        /// sensible cell size / spacing / padding so collection &amp; catalog cells don't
        /// overlap or render tiny. Idempotent / null-safe.
        /// </summary>
        public static void EnsureGrid(
            Component container,
            Vector2? cellSize = null,
            Vector2? spacing = null,
            int padding = 12)
        {
            if (container == null) return;

            // Unity forbids two LayoutGroups on one GameObject (AddComponent returns null), so remove any
            // vertical/horizontal group IMMEDIATELY (a deferred Destroy still blocks the AddComponent this frame).
            var hv = container.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (hv != null) UnityEngine.Object.DestroyImmediate(hv);

            var grid = container.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = container.gameObject.AddComponent<GridLayoutGroup>();
            if (grid == null) return;
            grid.cellSize = cellSize ?? new Vector2(220f, 300f);
            grid.spacing = spacing ?? new Vector2(16f, 16f);
            grid.padding = new RectOffset(padding, padding, padding, padding);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            // Fit as many columns as the width allows, then wrap.
            grid.constraint = GridLayoutGroup.Constraint.Flexible;

            EnsureVerticalFitter(container);
        }

        /// <summary>
        /// Guarantees a spawned row/cell reports a usable height to its parent layout
        /// group, so rows can't collapse onto each other. Idempotent / null-safe.
        /// </summary>
        public static void EnsureRowHeight(Component row, float minHeight)
        {
            if (row == null) return;
            var le = row.GetComponent<LayoutElement>();
            if (le == null) le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
        }

        /// <summary>
        /// Ensures a horizontal strip (e.g. cost-chip row) lays children out left→right
        /// without overlap. Idempotent / null-safe.
        /// </summary>
        public static void EnsureHorizontalStrip(Component container, float spacing = 8f, int padding = 0)
        {
            if (container == null) return;
            var layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void EnsureVerticalFitter(Component container)
        {
            var fitter = container.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = container.gameObject.AddComponent<ContentSizeFitter>();
            // Width is driven by the parent scroll-rect; height grows with content.
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ---- Internals ----------------------------------------------------------

        private static (Sprite normal, Sprite pressed) SpritesFor(ButtonStyle style) => style switch
        {
            ButtonStyle.Primary => (Load(ButtonPrimary), Load(ButtonPrimaryPressed)),
            ButtonStyle.Icon    => (Load(ButtonIcon), Load(ButtonIconPressed)),
            _                   => (Load(ButtonNav), Load(ButtonNavPressed)),
        };

        private static void ApplySliced(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            // Sliced rendering needs a non-zero pixels-per-unit multiplier; 1 keeps
            // the imported 9-slice border intact.
            image.pixelsPerUnitMultiplier = 1f;
            // Drop any flat tint baked onto the default white sprite so the art shows.
            if (image.color != Color.white)
            {
                var c = image.color;
                image.color = new Color(1f, 1f, 1f, c.a <= 0f ? 1f : c.a);
            }
        }

        private static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_cache.TryGetValue(path, out var cached)) return cached;

            Texture2D tex = null;
            foreach (var root in ResourceRoots)
            {
                var loadedSprite = Resources.Load<Sprite>(root + path);
                if (loadedSprite != null) { tex = loadedSprite.texture; break; }
                var loadedTex = Resources.Load<Texture2D>(root + path);
                if (loadedTex != null) { tex = loadedTex; break; }
            }

            Sprite sprite = null;
            if (tex != null)
            {
                // Build the sprite WITH a 9-slice border at runtime. The imported sprites ship with a
                // zero border (spriteBorder {0,0,0,0}), so Image.Type.Sliced stretched them flat — which
                // is why the skin "looked the same". Doing it here needs NO editor reimport / no .meta
                // edits: panels get a thicker border, buttons/inputs a moderate one.
                var w = tex.width;
                var h = tex.height;
                var minDim = Mathf.Min(w, h);
                var fraction = path.Contains("panel") ? 0.33f : 0.25f; // panel_brown / panelInset_beige
                var b = Mathf.Clamp(Mathf.RoundToInt(minDim * fraction), 1, Mathf.Max(1, (minDim - 2) / 2));
                var border = new Vector4(b, b, b, b);
                sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                sprite.name = "KenneySkin_" + path;
            }

            _cache[path] = sprite; // cache misses too (null) to avoid repeated I/O
            return sprite;
        }
    }
}
