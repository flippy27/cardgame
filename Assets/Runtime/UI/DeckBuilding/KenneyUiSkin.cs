using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

            Sprite sprite = null;
            foreach (var root in ResourceRoots)
            {
                sprite = Resources.Load<Sprite>(root + path);
                if (sprite != null) break;
            }

            _cache[path] = sprite; // cache misses too (null) to avoid repeated I/O
            return sprite;
        }
    }
}
