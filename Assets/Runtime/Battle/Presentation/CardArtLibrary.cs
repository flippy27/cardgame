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
        private const string FrameHandRoot = "Art/frames/hand";
        private const string FactionOverlayRoot = "Art/factions/overlays";
        private const string FactionCrestRoot = "Art/factions/crests";
        private const string SkillIconRoot = "Art/icons/skills";
        private const string StatusIconRoot = "Art/icons/status";

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
        /// Full composited card sprite (illustration + type/rarity frame + faction overlay + crest),
        /// cached per cardId. Falls back to the raw illustration if compositing is not possible.
        /// </summary>
        public static Sprite GetCardComposite(string cardId, int cardType, int cardRarity, int cardFaction)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return Missing;
            }

            if (_compositeCache.TryGetValue(cardId, out var cached))
            {
                return cached;
            }

            Sprite result;
            try
            {
                result = BuildComposite(cardId, cardType, cardRarity, cardFaction) ?? GetCardArt(cardId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CardArt] Composite failed for '{cardId}': {ex.Message}. Using raw art.");
                result = GetCardArt(cardId);
            }

            _compositeCache[cardId] = result;
            return result;
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

        private static Sprite BuildComposite(string cardId, int cardType, int cardRarity, int cardFaction)
        {
            var artTex = LoadTexture($"{ArtRoot}/{cardId}");
            if (artTex == null)
            {
                return null;
            }

            var canvas = ScaleToCanvas(artTex);

            var frameTex = LoadTexture($"{FrameHandRoot}/frame_hand_{TypeName(cardType)}_{RarityName(cardRarity)}");
            if (frameTex != null)
            {
                AlphaOver(canvas, frameTex, 0, 0);
            }

            var overlayTex = LoadTexture($"{FactionOverlayRoot}/faction_overlay_{FactionName(cardFaction)}");
            if (overlayTex != null)
            {
                AlphaOver(canvas, overlayTex, 0, 0);
            }

            var crestTex = LoadTexture($"{FactionCrestRoot}/faction_crest_{FactionName(cardFaction)}");
            if (crestTex != null)
            {
                var cx = (CanvasWidth - crestTex.width) / 2;
                AlphaOver(canvas, crestTex, cx, 120);
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

        private static Color32[] ScaleToCanvas(Texture2D src)
        {
            var dst = new Color32[CanvasWidth * CanvasHeight];
            var sp = src.GetPixels32();
            var sw = src.width;
            var sh = src.height;

            if (sw == CanvasWidth && sh == CanvasHeight)
            {
                Array.Copy(sp, dst, dst.Length);
            }
            else
            {
                for (var y = 0; y < CanvasHeight; y++)
                {
                    var sy = y * sh / CanvasHeight;
                    for (var x = 0; x < CanvasWidth; x++)
                    {
                        var sx = x * sw / CanvasWidth;
                        dst[y * CanvasWidth + x] = sp[sy * sw + sx];
                    }
                }
            }

            // illustration is the opaque base
            for (var i = 0; i < dst.Length; i++)
            {
                dst[i].a = 255;
            }
            return dst;
        }

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
                    var dst = canvas[di];
                    var sa = src.a / 255f;
                    var ia = 1f - sa;
                    canvas[di] = new Color32(
                        (byte)(src.r * sa + dst.r * ia),
                        (byte)(src.g * sa + dst.g * ia),
                        (byte)(src.b * sa + dst.b * ia),
                        255);
                }
            }
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
