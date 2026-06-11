using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Client-side card art resolver. Composites a card's final sprite from layers that live in
    /// the client (Resources), keyed by <c>cardId</c> and the card's type/rarity/faction:
    ///
    ///   1. card illustration   Resources/CardArt/{cardId}.png            (512x768, placeholder today)
    ///   2. type+rarity frame   Resources/Art/frames/hand/frame_hand_{type}_{rarity}.png
    ///   3. faction overlay     Resources/Art/factions/overlays/faction_overlay_{faction}.png
    ///   4. faction crest       Resources/Art/factions/crests/faction_crest_{faction}.png (256x256)
    ///
    /// The composite is baked into a single Sprite (cached per cardId) so the existing single
    /// "art" binding shows the full production-ready card without per-prefab layer wiring.
    /// Source textures are imported readable+Sprite by CardArtImportPostprocessor.
    ///
    /// Replace any Resources/CardArt/{cardId}.png with real art (same name, 512x768) to swap it in.
    /// ROLLOUT (future): swap Resources.Load in <see cref="Load"/> for Addressables to enable
    /// remote content download at the menu.
    /// </summary>
    public static class CardArtLibrary
    {
        public const int CanvasWidth = 512;
        public const int CanvasHeight = 768;

        private const string ArtRoot = "CardArt";
        // Modular frame set: blank-socket frames per faction + per variant (hand/board, ±armor),
        // and separate stat/attack symbol sprites baked into the sockets.
        private const string FrameRoot = "Art/frames2";
        private const string ModularIconRoot = "Art/icons/modular";
        private const string SkillIconRoot = "Art/icons/skills";
        private const string StatusIconRoot = "Art/icons/status";

        // Socket centres as fractions of the 512x768 card (top-left origin). The 512x512 frame is
        // placed centred, so it occupies the middle with a ~0.167 transparent band top & bottom. Stat
        // symbols are baked here and the TMP numbers overlay at the same fractions (LayoutStatsOverlay).
        // Tune to the art.
        private static readonly Vector2 HandSocketMana = new Vector2(0.27f, 0.267f);
        private static readonly Vector2 HandSocketRarity = new Vector2(0.71f, 0.267f);
        private static readonly Vector2 HandSocketAttack = new Vector2(0.27f, 0.733f);
        private static readonly Vector2 HandSocketHealth = new Vector2(0.71f, 0.733f);
        private static readonly Vector2 BoardSocketAttack = new Vector2(0.27f, 0.700f);
        private static readonly Vector2 BoardSocketHealth = new Vector2(0.73f, 0.700f);
        private const float SocketArmorYOffset = 0.115f; // armor socket sits this much above health
        private const float SocketIconFraction = 0.16f;  // icon size as fraction of canvas width

        private static readonly Dictionary<string, Sprite> _rawCache = new();
        private static readonly Dictionary<string, Sprite> _compositeCache = new();
        private static readonly Dictionary<string, Sprite> _iconCache = new();
        private static Sprite _missing;

        /// <summary>Raw per-card illustration only (no frame). Returns <see cref="Missing"/> if absent.</summary>
        public static Sprite GetCardArt(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return Missing;
            }

            if (_rawCache.TryGetValue(cardId, out var cached))
            {
                return cached ?? Missing;
            }

            var sprite = Load($"{ArtRoot}/{cardId}");
            _rawCache[cardId] = sprite;
            return sprite ?? Missing;
        }

        /// <summary>
        /// Full composited card sprite (illustration + faction frame + baked stat/attack symbols),
        /// cached per cardId+variant. Falls back to the raw illustration if compositing fails.
        /// unitType: 0 Melee / 1 Ranged / 2 Magic / negative = none (non-unit).
        /// </summary>
        public static Sprite GetCardComposite(string cardId, int cardType, int cardRarity, int cardFaction, int unitType = -1, bool hasArmor = false, string surface = null)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return Missing;
            }

            // Board ("played") cards use the board frame; everything else uses the hand frame.
            var isBoard = !string.IsNullOrWhiteSpace(surface) &&
                          (surface.Equals("played", StringComparison.OrdinalIgnoreCase) ||
                           surface.Equals("board", StringComparison.OrdinalIgnoreCase));
            var cacheKey = $"{cardId}:{(isBoard ? "b" : "h")}:{(hasArmor ? "a" : "n")}";

            if (_compositeCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            Sprite result;
            try
            {
                result = BuildComposite(cardId, cardType, cardRarity, cardFaction, unitType, hasArmor, isBoard) ?? GetCardArt(cardId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CardArt] Composite failed for '{cardId}': {ex.Message}. Using raw art.");
                result = GetCardArt(cardId);
            }

            _compositeCache[cacheKey] = result;
            return result;
        }

        /// <summary>Modular symbol sprite from the new set: Resources/Art/icons/modular/{name}. Null if absent.</summary>
        public static Sprite GetModularIcon(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? null : LoadIcon($"{ModularIconRoot}/{name}");
        }

        /// <summary>Ability icon from the pack: Resources/Art/icons/skills/skill_{abilityId}. Null if absent.</summary>
        public static Sprite GetSkillIcon(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }
            return LoadIcon($"{SkillIconRoot}/skill_{abilityId.Trim().ToLowerInvariant()}");
        }

        /// <summary>
        /// Status badge from the pack (grayscale, meant to be tinted by the UI):
        /// Resources/Art/icons/status/status_{poisoned|stunned|shielded|enrage_cooldown}. Null if absent.
        /// StatusEffectKind: 0 Poison, 1 Stun, 2 Shield, 3 EnrageCooldown.
        /// </summary>
        public static Sprite GetStatusIcon(int statusKind)
        {
            var key = statusKind switch
            {
                0 => "poisoned",
                1 => "stunned",
                2 => "shielded",
                3 => "enrage_cooldown",
                _ => null
            };
            return key == null ? null : LoadIcon($"{StatusIconRoot}/status_{key}");
        }

        private static Sprite LoadIcon(string resourcePath)
        {
            if (_iconCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }
            var sprite = Resources.Load<Sprite>(resourcePath);
            _iconCache[resourcePath] = sprite;
            return sprite;
        }

        // Art window (fraction of the 512x768 card, TOP-left origin) — the frame's transparent
        // interior, with the centred 512x512 frame's ~0.167 band baked into these values.
        private static readonly Rect HandArtWindow = new Rect(0.30f, 0.28f, 0.40f, 0.45f);
        private static readonly Rect BoardArtWindow = new Rect(0.22f, 0.32f, 0.56f, 0.36f);

        private static Sprite BuildComposite(string cardId, int cardType, int cardRarity, int cardFaction, int unitType, bool hasArmor, bool isBoard)
        {
            var artTex = LoadTexture($"{ArtRoot}/{cardId}");
            if (artTex == null)
            {
                return null;
            }

            // Start fully transparent: the card silhouette is defined by the frame, so outside the
            // frame the quad is see-through (the stray sibling quads are disabled in the renderer).
            var canvas = new Color32[CanvasWidth * CanvasHeight];

            // 1) Illustration, clipped to the frame's art window only.
            BlitArtToWindow(canvas, artTex, isBoard ? BoardArtWindow : HandArtWindow);

            // 2) Faction frame (blank sockets), placed at native size and centred so its own
            //    proportions are preserved (no stretch). Frames are authored 512x512.
            var surface = isBoard ? "board" : "hand";
            var armor = hasArmor ? "_armor" : string.Empty;
            var frameTex = LoadTexture($"{FrameRoot}/frame_{surface}{armor}_{FactionName(cardFaction)}");
            if (frameTex != null)
            {
                var offX = (CanvasWidth - frameTex.width) / 2;
                var offY = (CanvasHeight - frameTex.height) / 2;
                AlphaOver(canvas, frameTex, offX, offY);
            }

            // 3) Bake the modular stat/attack symbols into the frame sockets. Numbers overlay on top
            //    at the same socket fractions (LayoutStatsOverlay). Board frames carry no mana/rarity
            //    sockets, so those symbols are hand-only.
            var isUnit = cardType == 0;
            var attackSocket = isBoard ? BoardSocketAttack : HandSocketAttack;
            var healthSocket = isBoard ? BoardSocketHealth : HandSocketHealth;
            if (!isBoard)
            {
                BlitIcon(canvas, GetModularIcon("stat_mana"), HandSocketMana);
                BlitIcon(canvas, GetModularIcon("stat_rarity"), HandSocketRarity);
            }
            if (isUnit)
            {
                BlitIcon(canvas, GetModularIcon(AttackIconName(unitType)), attackSocket);
                BlitIcon(canvas, GetModularIcon("stat_health"), healthSocket);
            }
            if (hasArmor)
            {
                BlitIcon(canvas, GetModularIcon("stat_armor"), new Vector2(healthSocket.x, healthSocket.y - SocketArmorYOffset));
            }

            var baked = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false)
            {
                name = $"CardComposite_{cardId}",
                wrapMode = TextureWrapMode.Clamp
            };
            baked.SetPixels32(canvas);
            baked.Apply(false, false);

            var sprite = Sprite.Create(baked, new Rect(0, 0, CanvasWidth, CanvasHeight), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"CardComposite_{cardId}";
            return sprite;
        }

        // --- compositing helpers (CPU alpha-over; requires readable source textures) ---

        // Draws the illustration (opaque) into the given window rect, expressed as fractions of the
        // canvas with a TOP-left origin. Pixels outside the window are left untouched (transparent),
        // so the frame clips the art and the art can never spill past the frame.
        private static void BlitArtToWindow(Color32[] canvas, Texture2D src, Rect normWindowTopLeft)
        {
            var x0 = Mathf.Clamp(Mathf.RoundToInt(normWindowTopLeft.xMin * CanvasWidth), 0, CanvasWidth);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(normWindowTopLeft.xMax * CanvasWidth), 0, CanvasWidth);
            // top-left origin -> bottom-left origin for the texture buffer
            var winY0 = Mathf.Clamp(Mathf.RoundToInt(CanvasHeight - normWindowTopLeft.yMax * CanvasHeight), 0, CanvasHeight);
            var winY1 = Mathf.Clamp(Mathf.RoundToInt(CanvasHeight - normWindowTopLeft.yMin * CanvasHeight), 0, CanvasHeight);

            var ww = x1 - x0;
            var wh = winY1 - winY0;
            if (ww <= 0 || wh <= 0)
            {
                return;
            }

            var sp = src.GetPixels32();
            var sw = src.width;
            var sh = src.height;

            for (var y = 0; y < wh; y++)
            {
                var sy = y * sh / wh;
                for (var x = 0; x < ww; x++)
                {
                    var sx = x * sw / ww;
                    var c = sp[sy * sw + sx];
                    c.a = 255;
                    canvas[(winY0 + y) * CanvasWidth + (x0 + x)] = c;
                }
            }
        }

        // Alpha-over a frame layer cropped to its opaque bounds, scaled to fill the whole canvas. This
        // normalises frames authored with different transparent margins (512x512 with the card body
        // centred) so they all fill the 512x768 card and the sockets land at consistent fractions.
        private static void AlphaOverScaledCropped(Color32[] canvas, Texture2D layer)
        {
            var lp = layer.GetPixels32();
            var lw = layer.width;
            var lh = layer.height;
            if (lw <= 0 || lh <= 0)
            {
                return;
            }

            int minx = lw, miny = lh, maxx = -1, maxy = -1;
            for (var y = 0; y < lh; y++)
            {
                for (var x = 0; x < lw; x++)
                {
                    if (lp[y * lw + x].a > 16)
                    {
                        if (x < minx) minx = x;
                        if (x > maxx) maxx = x;
                        if (y < miny) miny = y;
                        if (y > maxy) maxy = y;
                    }
                }
            }
            if (maxx < minx || maxy < miny)
            {
                return;
            }

            var bw = maxx - minx + 1;
            var bh = maxy - miny + 1;
            for (var y = 0; y < CanvasHeight; y++)
            {
                var ly = miny + y * bh / CanvasHeight; // both buffers are bottom-origin: no flip
                for (var x = 0; x < CanvasWidth; x++)
                {
                    var lx = minx + x * bw / CanvasWidth;
                    var src = lp[ly * lw + lx];
                    if (src.a == 0)
                    {
                        continue;
                    }

                    var di = y * CanvasWidth + x;
                    canvas[di] = Over(src, canvas[di]);
                }
            }
        }

        // Bake a modular symbol centred on a socket (socket centre as a fraction of the card,
        // TOP-left origin). Sized to SocketIconFraction of the card width.
        private static void BlitIcon(Color32[] canvas, Sprite icon, Vector2 socketTopLeft)
        {
            if (icon == null || icon.texture == null)
            {
                return;
            }

            var size = Mathf.RoundToInt(SocketIconFraction * CanvasWidth);
            if (size <= 0)
            {
                return;
            }

            var x0 = Mathf.RoundToInt(socketTopLeft.x * CanvasWidth) - size / 2;
            var y0 = Mathf.RoundToInt((1f - socketTopLeft.y) * CanvasHeight) - size / 2; // top-left -> bottom origin

            var tex = icon.texture;
            var sp = tex.GetPixels32();
            var sw = tex.width;
            var sh = tex.height;

            for (var y = 0; y < size; y++)
            {
                var cy = y0 + y;
                if (cy < 0 || cy >= CanvasHeight)
                {
                    continue;
                }

                var sy = y * sh / size;
                for (var x = 0; x < size; x++)
                {
                    var cx = x0 + x;
                    if (cx < 0 || cx >= CanvasWidth)
                    {
                        continue;
                    }

                    var src = sp[sy * sw + (x * sw / size)];
                    if (src.a == 0)
                    {
                        continue;
                    }

                    var di = cy * CanvasWidth + cx;
                    canvas[di] = Over(src, canvas[di]);
                }
            }
        }

        private static string AttackIconName(int unitType) => unitType switch
        {
            1 => "attack_bow",
            2 => "attack_magic",
            _ => "attack_sword"
        };

        private static void AlphaOver(Color32[] canvas, Texture2D layer, int offsetX, int offsetY)
        {
            var lp = layer.GetPixels32();
            var lw = layer.width;
            var lh = layer.height;

            for (var ly = 0; ly < lh; ly++)
            {
                var cy = offsetY + ly;
                if (cy < 0 || cy >= CanvasHeight)
                {
                    continue;
                }

                for (var lx = 0; lx < lw; lx++)
                {
                    var cx = offsetX + lx;
                    if (cx < 0 || cx >= CanvasWidth)
                    {
                        continue;
                    }

                    var src = lp[ly * lw + lx];
                    if (src.a == 0)
                    {
                        continue;
                    }

                    var di = cy * CanvasWidth + cx;
                    canvas[di] = Over(src, canvas[di]);
                }
            }
        }

        // Alpha-over a full-card layer (frame) scaled to the canvas, so layers authored at a
        // different size (e.g. 256x384 board frames) still cover the 512x768 card.
        private static void AlphaOverScaled(Color32[] canvas, Texture2D layer)
        {
            var lp = layer.GetPixels32();
            var lw = layer.width;
            var lh = layer.height;
            if (lw <= 0 || lh <= 0)
            {
                return;
            }

            for (var y = 0; y < CanvasHeight; y++)
            {
                var sy = y * lh / CanvasHeight;
                for (var x = 0; x < CanvasWidth; x++)
                {
                    var sx = x * lw / CanvasWidth;
                    var src = lp[sy * lw + sx];
                    if (src.a == 0)
                    {
                        continue;
                    }

                    var di = y * CanvasWidth + x;
                    canvas[di] = Over(src, canvas[di]);
                }
            }
        }

        // Porter-Duff "source over destination" for straight-alpha Color32. Preserves alpha so the
        // composite keeps the frame's silhouette (fully transparent outside the frame).
        private static Color32 Over(Color32 src, Color32 dst)
        {
            var sa = src.a / 255f;
            var da = dst.a / 255f;
            var oa = sa + da * (1f - sa);
            if (oa <= 0f)
            {
                return new Color32(0, 0, 0, 0);
            }

            var inv = 1f / oa;
            return new Color32(
                (byte)((src.r * sa + dst.r * da * (1f - sa)) * inv),
                (byte)((src.g * sa + dst.g * da * (1f - sa)) * inv),
                (byte)((src.b * sa + dst.b * da * (1f - sa)) * inv),
                (byte)(oa * 255f));
        }

        private static string TypeName(int cardType) => cardType switch
        {
            1 => "utility",
            2 => "equipment",
            3 => "spell",
            _ => "unit"
        };

        private static string RarityName(int cardRarity) => cardRarity switch
        {
            1 => "rare",
            2 => "epic",
            3 => "legendary",
            _ => "common"
        };

        private static string FactionName(int cardFaction) => cardFaction switch
        {
            0 => "ember",
            1 => "tidal",
            2 => "grove",
            3 => "alloy",
            4 => "void",
            _ => "neutral"
        };

        public static Sprite Missing
        {
            get
            {
                if (_missing != null)
                {
                    return _missing;
                }

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 0, 255, 255);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _missing = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                _missing.name = "CardArtMissing";
                return _missing;
            }
        }

        public static void ClearCache()
        {
            _rawCache.Clear();
            _compositeCache.Clear();
        }

        private static Sprite Load(string resourcePath) => Resources.Load<Sprite>(resourcePath);

        private static Texture2D LoadTexture(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null && sprite.texture != null)
            {
                return sprite.texture;
            }
            return Resources.Load<Texture2D>(resourcePath);
        }
    }
}
