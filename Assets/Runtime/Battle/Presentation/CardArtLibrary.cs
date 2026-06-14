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
    ///   2. ornamental frame     Resources/Art/frames3/frame_{hand|board}_{design}.png
    ///                           hand = 512x768 (2:3), board = 512x512 (square)
    ///   3. baked stat icons      Resources/Art/icons/modular/{attack_*, stat_*}.png at sockets
    ///   4. rarity badge          procedural hexagon (tinted by rarity) + stat_rarity gem
    ///
    /// The new frames3 set is an OPEN ornamental border with NO baked sockets/circles, so the
    /// compositor draws the stat icons (and the rarity hexagon) itself at fixed sockets. The
    /// composite is baked into a single Sprite (cached per cardId+variant) so the existing single
    /// "art" binding shows the full card without per-prefab layer wiring.
    /// Source textures are imported readable+Sprite by CardArtImportPostprocessor.
    ///
    /// THREE coordinate systems must stay in sync (this is the #1 source of tuning bugs):
    ///   - composite canvas (hand 512x768, board 512x512), top-left-origin fractions in this file;
    ///   - the 3D quad (hand 2:3, board square — see Card3DPlaced.prefab CardVisual scale);
    ///   - the world-space stat overlay (hand 1000x1400, board 1000x1000) in LayoutStatsOverlay.
    /// The stat NUMBERS derive their overlay position from the SAME socket fractions exposed here
    /// (see <see cref="HandSocket"/>/<see cref="BoardSocket"/>), so icon and number never drift.
    ///
    /// Replace any Resources/CardArt/{cardId}.png with real art (same name, 512x768) to swap it in.
    /// ROLLOUT (future): swap Resources.Load in <see cref="Load"/> for Addressables to enable
    /// remote content download at the menu.
    /// </summary>
    public static class CardArtLibrary
    {
        // Canvas width is shared; height is per-surface (hand portrait 2:3, board square 1:1). All
        // blit/fraction math uses the surface's actual width/height — never a hardcoded 768.
        public const int CanvasWidth = 512;
        public const int HandCanvasHeight = 768;   // hand = 512x768 (2:3)
        public const int BoardCanvasHeight = 512;  // board = 512x512 (square)

        // Back-compat alias (old code referenced CanvasHeight = the hand height).
        public const int CanvasHeight = HandCanvasHeight;

        private const string ArtRoot = "CardArt";
        // One shared illustration per card type (test art): unit / spell / equipment / misc (utility).
        private const string ArtTypeRoot = "Art/cardart_type";

        // CardType -> shared type-art file name. Utility(1) reuses the "misc" illustration.
        private static string TypeArtName(int cardType) => cardType switch
        {
            1 => "misc",
            2 => "equipment",
            3 => "spell",
            _ => "unit"
        };
        private const string FrameRoot = "Art/frames3";          // frame_{hand|board}_{design}.png
        private const string ModularIconRoot = "Art/icons/modular"; // attack_*/stat_* symbols
        private const string SkillIconRoot = "Art/icons/skills";
        private const string StatusIconRoot = "Art/icons/status";
        private const string FactionIconRoot = "Art/icons/faction"; // faction_{ember|tidal|grove|alloy|void}
        private const string TypeIconRoot = "Art/icons/type";       // type_{unit|utility|equipment|spell}
        private const string RarityIconRoot = "Art/icons/rarity";   // rarity_{common|rare|epic|legendary} gems

        // === SOCKET FRACTIONS (single source of truth) ===
        // Centres as fractions of the surface canvas, TOP-left origin (y grows down). The baked
        // icons AND the TMP stat numbers both read from these, so they always line up. Tune by
        // screenshot. Layout: cost top-left (hand only), attack bottom-left, rarity bottom-middle,
        // health bottom-right (heart-as-badge, no circle), armor ABOVE health (raised to clear the
        // larger heart). Each disc badge's small stat icon pokes to the card's OUTER corner (see
        // CardStatBadges CornerIcon* constants).
        // These are OVERLAY/quad fractions. The frame inside the composite has a ~9-10% transparent
        // margin, so the frame's visible border sits at quad ~0.10..0.90; the stat row must land on
        // that border (~0.79), NOT at the quad edge (0.85+ pokes below the visible frame).
        private static readonly Vector2 _handCost = new Vector2(0.23f, 0.19f);
        private static readonly Vector2 _handAttack = new Vector2(0.26f, 0.77f);
        private static readonly Vector2 _handRarity = new Vector2(0.50f, 0.80f);
        private static readonly Vector2 _handHealth = new Vector2(0.74f, 0.77f);
        // Armor sits ABOVE the (now larger, circle-less heart) health badge. Raised from 0.63 -> 0.55
        // so the armor disc clears the heart (heart is ~0.24 of width tall; old 0.14 gap collided).
        private static readonly Vector2 _handArmor = new Vector2(0.74f, 0.55f);

        // Non-unit effect badges: a status icon sits ABOVE the +/- number on each column. The LEFT
        // column (offensive: damage/debuff) mirrors the attack socket; the RIGHT (defensive/grant)
        // reuses the armor socket. Numbers reuse the Attack (left) / Health (right) sockets.
        private static readonly Vector2 _handEffectLeft = new Vector2(0.26f, 0.63f);

        private static readonly Vector2 _boardAttack = new Vector2(0.26f, 0.76f);
        private static readonly Vector2 _boardRarity = new Vector2(0.50f, 0.79f);
        private static readonly Vector2 _boardHealth = new Vector2(0.74f, 0.76f);
        // Board armor raised from 0.62 -> 0.53 to clear the larger circle-less heart (see hand note).
        private static readonly Vector2 _boardArmor = new Vector2(0.74f, 0.53f);
        private static readonly Vector2 _boardEffectLeft = new Vector2(0.26f, 0.62f);

        // NOTE: the old baked-icon size fractions (SocketIconFraction / RarityBadgeFraction /
        // SocketBadgeFraction) moved to CardStatBadges as overlay-UI fractions — nothing is baked here.

        // Art window (fraction of the surface canvas, TOP-left origin: x, y, w, h). Clipped to the
        // frame's open centre so the illustration never spills past the opening ("fondo que sobra").
        // MEASURED (PIL, robust row/column transparency scan) over all 16 frames3 designs — the
        // TIGHTEST inner opening common to EVERY design (top-left origin) is:
        //   hand  x:[0.217..0.781] y:[0.290..0.730]
        //   board x:[0.217..0.779] y:[0.256..0.777]
        // The windows below sit a hair INSIDE those tightest bounds so the illustration never pokes
        // past ANY frame's inner edge on either surface (better slightly small than spilling). The
        // stat badges are NOW overlay UI (CardStatBadges) and are unaffected by this window.
        // ASPECT MATTERS: the canvases are hand 512x768 (2:3) and board 512x512 (1:1); the per-type art
        // is 2:3 (hand) / 1:1 (board). To avoid squishing the portrait, the window must keep the source
        // aspect — on a 2:3 canvas that means EQUAL x/y fractions (0.62w x 0.62h = 317x476 px = 2:3); on
        // a square canvas equal fractions = square. Sized to fill most of the opening (frames are open
        // ornamental borders; a little art under the border is masked by it). Tune by screenshot.
        private static readonly Rect HandArtWindow = new Rect(0.19f, 0.19f, 0.62f, 0.62f);
        private static readonly Rect BoardArtWindow = new Rect(0.17f, 0.17f, 0.66f, 0.66f);

        // Window for PER-CARD art (Resources/CardArt/{cardId}[_board].png). It must match the frame's
        // INNER OPENING, NOT the outer edge — filling to the outer edge (the old 0.10..0.90) made the art
        // read as BIGGER than the frame (it spilled under the border and out to the transparent corners).
        // Measured frames3 silver_knight inner opening: hand x[0.17..0.83] (~0.66 wide). Equal x/y fractions
        // keep the source 2:3 aspect on the 2:3 hand canvas (no distortion); top/bottom tuck under the
        // frame's ornaments. Board is squarer. Tune by screenshot.
        private static readonly Rect HandArtWindowFull = new Rect(0.17f, 0.17f, 0.66f, 0.66f);
        private static readonly Rect BoardArtWindowFull = new Rect(0.17f, 0.17f, 0.66f, 0.66f);

        private static readonly Dictionary<string, Sprite> _rawCache = new();
        private static readonly Dictionary<string, Sprite> _compositeCache = new();
        private static readonly Dictionary<string, Sprite> _iconCache = new();
        private static readonly Dictionary<int, Sprite> _rarityBadgeCache = new();
        // Decoded source pixel buffers keyed by texture instance id. GetPixels32() decodes the whole
        // texture each call; the frame and per-type art layers are shared across MANY cards, so caching
        // their decoded buffers turns the per-card composite cost from "decode every layer again" into a
        // dictionary hit. Cleared on ClearCache(). Low-risk: same pixels, just memoized.
        private static readonly Dictionary<int, (Color32[] pixels, int w, int h)> _texturePixelCache = new();

        private static (Color32[] pixels, int w, int h) GetPixelsCached(Texture2D tex)
        {
            if (tex == null)
            {
                return (null, 0, 0);
            }
            var key = tex.GetInstanceID();
            if (_texturePixelCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
            var entry = (tex.GetPixels32(), tex.width, tex.height);
            _texturePixelCache[key] = entry;
            return entry;
        }
        private static Sprite _socketBadge;
        private static Sprite _missing;

        // === PUBLIC socket accessors (LayoutStatsOverlay derives number positions from these) ===
        /// <summary>Hand socket centre (top-left-origin fraction) for a stat: cost/attack/rarity/health/armor.</summary>
        public static Vector2 HandSocket(CardStatSocket socket) => socket switch
        {
            CardStatSocket.Cost => _handCost,
            CardStatSocket.Attack => _handAttack,
            CardStatSocket.Rarity => _handRarity,
            CardStatSocket.Health => _handHealth,
            CardStatSocket.Armor => _handArmor,
            CardStatSocket.EffectLeft => _handEffectLeft,
            CardStatSocket.EffectRight => _handArmor,
            _ => Vector2.zero
        };

        /// <summary>Board socket centre (top-left-origin fraction) for a stat: attack/rarity/health/armor (no cost).</summary>
        public static Vector2 BoardSocket(CardStatSocket socket) => socket switch
        {
            CardStatSocket.Attack => _boardAttack,
            CardStatSocket.Rarity => _boardRarity,
            CardStatSocket.Health => _boardHealth,
            CardStatSocket.Armor => _boardArmor,
            CardStatSocket.EffectLeft => _boardEffectLeft,
            CardStatSocket.EffectRight => _boardArmor,
            _ => Vector2.zero
        };

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
        /// Full composited card sprite (illustration + ornamental frame + baked stat/attack/rarity
        /// symbols), cached per cardId+variant. Falls back to the raw illustration if compositing
        /// fails. unitType: 0 Melee / 1 Ranged / 2 Magic / negative = none (non-unit).
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

        /// <summary>
        /// Stat-icon sprite for the overlay badges (stat_mana, attack_sword/attack_bow/attack_magic,
        /// stat_health, stat_armor). Alias of <see cref="GetModularIcon"/> with an intent-revealing name.
        /// </summary>
        public static Sprite GetStatIconSprite(string name) => GetModularIcon(name);

        /// <summary>
        /// The dark disc + metallic ring socket badge, drawn UNDER a stat icon/number in the overlay
        /// so the stat reads as a seated socket on the open frame. One shared, cached sprite.
        /// </summary>
        public static Sprite GetSocketBadgeSprite() => GetSocketBadge();

        /// <summary>
        /// The procedural rarity hexagon (tinted by rarity) with the stat_rarity gem composited on top.
        /// Cached per rarity. Shown by the overlay rarity badge (no number).
        /// </summary>
        public static Sprite GetRarityBadgeSprite(int rarity) => GetRarityBadge(rarity);

        /// <summary>UnitType -> attack-type icon name (0/none Melee, 1 Ranged, 2 Magic).</summary>
        public static string AttackIconName(int unitType) => unitType switch
        {
            1 => "attack_bow",
            2 => "attack_magic",
            _ => "attack_sword"
        };

        /// <summary>Ability icon from the pack: Resources/Art/icons/skills/skill_{abilityId}. Null if absent.</summary>
        public static Sprite GetSkillIcon(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }
            return LoadIcon($"{SkillIconRoot}/skill_{abilityId.Trim().ToLowerInvariant()}");
        }

        /// <summary>Faction emblem: Resources/Art/icons/faction/faction_{ember|tidal|grove|alloy|void}. Null if absent.</summary>
        public static Sprite GetFactionIcon(int faction)
        {
            var key = faction switch
            {
                0 => "ember", 1 => "tidal", 2 => "grove", 3 => "alloy", 4 => "void", _ => null
            };
            return key == null ? null : LoadIcon($"{FactionIconRoot}/faction_{key}");
        }

        /// <summary>Card-type emblem: Resources/Art/icons/type/type_{unit|utility|equipment|spell}. Null if absent.</summary>
        public static Sprite GetTypeIcon(int cardType)
        {
            var key = cardType switch
            {
                0 => "unit", 1 => "utility", 2 => "equipment", 3 => "spell", _ => null
            };
            return key == null ? null : LoadIcon($"{TypeIconRoot}/type_{key}");
        }

        /// <summary>Per-tier rarity gem: Resources/Art/icons/rarity/rarity_{common|rare|epic|legendary}. Null if absent.</summary>
        public static Sprite GetRarityGem(int rarity)
        {
            var key = rarity switch
            {
                0 => "common", 1 => "rare", 2 => "epic", 3 => "legendary", _ => null
            };
            return key == null ? null : LoadIcon($"{RarityIconRoot}/rarity_{key}");
        }

        /// <summary>
        /// Status badge from the pack (grayscale, meant to be tinted by the UI):
        /// Resources/Art/icons/status/status_{key}. Null if absent (then the UI falls back to a
        /// tinted circle + letter — see <see cref="StatusBadgeLetter"/>/<see cref="StatusBadgeTint"/>).
        /// StatusEffectKind (mirror <see cref="Flippy.CardDuelMobile.Core.StatusEffectKind"/>):
        /// 0 Poison, 1 Stun, 2 Shield, 3 EnrageCooldown, 4 Burn, 5 Regeneration, 6 Paralyze,
        /// 7 Confuse, 8 Silence, 9 Vulnerable, 10 Weaken, 11 Ward.
        /// TODO(art): drop real sprites named status_{key} for the new kinds (burned/regenerating/
        /// paralyzed/confused/silenced/vulnerable/weakened/warded) into Resources/Art/icons/status/.
        /// </summary>
        public static Sprite GetStatusIcon(int statusKind)
        {
            var key = StatusIconKey(statusKind);
            return key == null ? null : LoadIcon($"{StatusIconRoot}/status_{key}");
        }

        /// <summary>StatusEffectKind -> sprite-file key (null if no known key).</summary>
        public static string StatusIconKey(int statusKind) => statusKind switch
        {
            0 => "poisoned",
            1 => "stunned",
            2 => "shielded",
            3 => "enrage_cooldown",
            4 => "burned",
            5 => "regenerating",
            6 => "paralyzed",
            7 => "confused",
            8 => "silenced",
            9 => "vulnerable",
            10 => "weakened",
            11 => "warded",
            _ => null
        };

        /// <summary>
        /// First-pass placeholder for a status with no sprite yet: a short letter drawn on a tinted
        /// circle (see <see cref="StatusBadgeTint"/>). Keep in sync with <see cref="StatusIconKey"/>.
        /// </summary>
        public static string StatusBadgeLetter(int statusKind) => statusKind switch
        {
            0 => "P",   // Poison
            1 => "S",   // Stun
            2 => "SH",  // Shield
            3 => "E",   // EnrageCooldown
            4 => "B",   // Burn
            5 => "R",   // Regeneration
            6 => "PA",  // Paralyze
            7 => "C",   // Confuse
            8 => "SI",  // Silence
            9 => "V",   // Vulnerable
            10 => "W",  // Weaken
            11 => "WD", // Ward
            _ => "?"
        };

        /// <summary>
        /// Distinct tint per status so the placeholder circle reads at a glance. Mirrors the tints in
        /// <c>CardVisualCommon.ResolveStatusTint</c>; keep the two in sync.
        /// </summary>
        public static Color StatusBadgeTint(int statusKind) => statusKind switch
        {
            0 => new Color(0.55f, 0.85f, 0.30f, 1f),  // Poison  - green
            1 => new Color(1.00f, 0.85f, 0.20f, 1f),  // Stun    - yellow
            2 => new Color(0.45f, 0.75f, 1.00f, 1f),  // Shield  - blue
            3 => new Color(1.00f, 0.45f, 0.25f, 1f),  // Enrage  - orange-red
            4 => new Color(1.00f, 0.40f, 0.10f, 1f),  // Burn    - fire orange
            5 => new Color(0.40f, 0.95f, 0.55f, 1f),  // Regen   - bright green
            6 => new Color(0.70f, 0.55f, 1.00f, 1f),  // Paralyze- violet
            7 => new Color(0.95f, 0.55f, 0.95f, 1f),  // Confuse - magenta
            8 => new Color(0.60f, 0.60f, 0.65f, 1f),  // Silence - grey
            9 => new Color(1.00f, 0.35f, 0.45f, 1f),  // Vulnerable - red
            10 => new Color(0.55f, 0.45f, 0.35f, 1f), // Weaken  - brown
            11 => new Color(0.85f, 0.90f, 1.00f, 1f), // Ward    - pale blue/white
            _ => Color.white
        };

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

        // === FRAME MAPPING (cardType, faction) -> frames3 design. Trivial to re-map here. ===
        // Non-unit types get one design each; units get a design per faction.
        //   CardType{Unit=0,Utility=1,Equipment=2,Spell=3}
        //   CardFaction{Ember=0,Tidal=1,Grove=2,Alloy=3,Void=4}
        private static string FrameDesign(int cardType, int cardFaction)
        {
            switch (cardType)
            {
                case 3: return "violet_arcane";    // Spell
                case 2: return "forged_steel";     // Equipment
                case 1: return "parchment_relic";  // Utility
                default: // Unit (0) -> by faction
                    return cardFaction switch
                    {
                        0 => "ember_obsidian", // Ember
                        1 => "frost_magic",    // Tidal
                        2 => "mossy_temple",   // Grove
                        3 => "silver_knight",  // Alloy
                        4 => "shadow_thorn",   // Void
                        _ => "royal_gold"      // neutral / default
                    };
            }
        }

        private static Sprite BuildComposite(string cardId, int cardType, int cardRarity, int cardFaction, int unitType, bool hasArmor, bool isBoard)
        {
            var canvasW = CanvasWidth;
            var canvasH = isBoard ? BoardCanvasHeight : HandCanvasHeight;

            // ART: PER-CARD illustration takes precedence (Resources/CardArt/{cardId}[_board].png) — now
            // that real per-card art exists. Board cards prefer the square {cardId}_board variant, then the
            // portrait {cardId}. Falls back to the shared per-TYPE test illustration
            // (Resources/Art/cardart_type/{type}.png) for cards without per-card art yet, then a faction tint.
            var perCardArt = isBoard
                ? (LoadTexture($"{ArtRoot}/{cardId}_board") ?? LoadTexture($"{ArtRoot}/{cardId}"))
                : LoadTexture($"{ArtRoot}/{cardId}");
            var typeArtName = TypeArtName(cardType);
            var typeArt = isBoard
                ? (LoadTexture($"{ArtTypeRoot}/{typeArtName}_board") ?? LoadTexture($"{ArtTypeRoot}/{typeArtName}"))
                : LoadTexture($"{ArtTypeRoot}/{typeArtName}");
            // Per-card art is full-bleed (fills the surface); type art is a smaller windowed illustration.
            var usingPerCard = perCardArt != null;
            var artTex = perCardArt ?? typeArt;

            // FRAME by DATA: open ornamental border for this (type, faction). Already authored at the
            // surface aspect (hand 2:3, board square), so it fills the canvas ~1:1, no distortion.
            var design = FrameDesign(cardType, cardFaction);
            var frameTex = LoadTexture($"{FrameRoot}/frame_{(isBoard ? "board" : "hand")}_{design}");

            // Only give up (-> magenta Missing) when there is genuinely nothing to draw.
            if (artTex == null && frameTex == null)
            {
                return null;
            }

            // Start fully transparent: the card silhouette is defined by the frame, so outside the
            // frame the quad is see-through (the material alpha-clips it; stray quads are disabled).
            var canvas = new Color32[canvasW * canvasH];
            // Full-bleed window for per-card art (fills to the frame edge); tighter window for type art.
            var artWindow = usingPerCard
                ? (isBoard ? BoardArtWindowFull : HandArtWindowFull)
                : (isBoard ? BoardArtWindow : HandArtWindow);

            // 1) Illustration, clipped to the frame's open centre. When the per-card art is missing,
            //    fill the window with a faction tint so it reads "this faction, art pending".
            if (artTex != null)
            {
                BlitArtToWindow(canvas, canvasW, canvasH, artTex, artWindow);
            }
            else
            {
                FillWindow(canvas, canvasW, canvasH, artWindow, FactionColor(cardFaction));
            }

            // 2) Ornamental frame, scaled to fill the whole canvas (matches the surface aspect).
            AlphaOverFill(canvas, canvasW, canvasH, frameTex);

            // NOTE: the socket badges, stat/attack ICONS, and the rarity hexagon are NO LONGER baked
            // here. They are now rendered as overlay-UI GameObjects by CardStatBadges (so each number
            // is a child of its circle and the whole badge moves/aligns together — no parallax, no
            // ZTest hacks, no cross-card bleed). This composite is ART + FRAME only. The unitType /
            // cardRarity / hasArmor parameters are kept for cache-key compatibility and possible reuse.

            var baked = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false)
            {
                name = $"CardComposite_{cardId}",
                wrapMode = TextureWrapMode.Clamp
            };
            baked.SetPixels32(canvas);
            baked.Apply(false, false);

            var sprite = Sprite.Create(baked, new Rect(0, 0, canvasW, canvasH), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"CardComposite_{cardId}";
            return sprite;
        }

        // --- compositing helpers (CPU alpha-over; requires readable source textures) ---

        // Draws the illustration (opaque) into the given window rect, expressed as fractions of the
        // canvas with a TOP-left origin. Pixels outside the window are left untouched (transparent),
        // so the frame clips the art and the art can never spill past the frame opening.
        private static void BlitArtToWindow(Color32[] canvas, int canvasW, int canvasH, Texture2D src, Rect normWindowTopLeft)
        {
            var x0 = Mathf.Clamp(Mathf.RoundToInt(normWindowTopLeft.xMin * canvasW), 0, canvasW);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(normWindowTopLeft.xMax * canvasW), 0, canvasW);
            // top-left origin -> bottom-left origin for the texture buffer
            var winY0 = Mathf.Clamp(Mathf.RoundToInt(canvasH - normWindowTopLeft.yMax * canvasH), 0, canvasH);
            var winY1 = Mathf.Clamp(Mathf.RoundToInt(canvasH - normWindowTopLeft.yMin * canvasH), 0, canvasH);

            var ww = x1 - x0;
            var wh = winY1 - winY0;
            if (ww <= 0 || wh <= 0)
            {
                return;
            }

            var (sp, sw, sh) = GetPixelsCached(src);
            if (sp == null)
            {
                return;
            }

            for (var y = 0; y < wh; y++)
            {
                var sy = y * sh / wh;
                for (var x = 0; x < ww; x++)
                {
                    var sx = x * sw / ww;
                    var c = sp[sy * sw + sx];
                    c.a = 255;
                    canvas[(winY0 + y) * canvasW + (x0 + x)] = c;
                }
            }
        }

        // === SOCKET BADGE: a generated circular socket (dark translucent disc + bright metallic
        // ring) drawn UNDER a stat icon so the icon reads as a seated badge on the open frame. One
        // shared, cached sprite (rarity-independent). Now consumed by CardStatBadges (overlay UI). ===
        private static Sprite GetSocketBadge()
        {
            if (_socketBadge != null)
            {
                return _socketBadge;
            }

            const int size = 128;
            var c = (size - 1) * 0.5f;
            var radius = c * 0.98f;
            var disc = new Color(0.06f, 0.07f, 0.09f, 0.85f);   // dark translucent socket interior
            var ringInner = new Color(0.78f, 0.80f, 0.85f, 1f); // bright metallic highlight
            var ringOuter = new Color(0.40f, 0.42f, 0.47f, 1f); // darker metallic edge

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - c) / radius;
                    var dy = (y - c) / radius;
                    var d = Mathf.Sqrt(dx * dx + dy * dy); // 0 centre .. 1 edge
                    Color col;
                    if (d > 1f) col = new Color(0, 0, 0, 0);
                    else if (d > 0.90f) col = ringOuter;                                      // outer metallic edge
                    else if (d > 0.80f) col = Color.Lerp(ringInner, ringOuter, (d - 0.80f) / 0.10f); // bright ring
                    else if (d > 0.74f) col = Color.Lerp(disc, ringInner, (d - 0.74f) / 0.06f);      // inner bevel
                    else col = disc;                                                           // dark interior
                    pixels[y * size + x] = col;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SocketBadge", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _socketBadge = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _socketBadge.name = "SocketBadge";
            return _socketBadge;
        }

        // === RARITY BADGE: procedural pointy-top hexagon tinted by rarity, with the stat_rarity gem
        // composited centred on top. Cached per rarity. No number is drawn for rarity. ===
        private static Sprite GetRarityBadge(int cardRarity)
        {
            if (_rarityBadgeCache.TryGetValue(cardRarity, out var cached) && cached != null)
            {
                return cached;
            }

            const int size = 128;
            var c = (size - 1) * 0.5f;
            var radius = c * 0.96f;
            var fill = RarityColor(cardRarity);
            var rim = new Color(
                Mathf.Clamp01(fill.r * 1.5f + 0.18f),
                Mathf.Clamp01(fill.g * 1.5f + 0.18f),
                Mathf.Clamp01(fill.b * 1.5f + 0.18f),
                1f);

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Pointy-top regular hexagon: inside test via the max of the three axis distances.
                    var px = (x - c) / radius;
                    var py = (y - c) / radius;
                    var ax = Mathf.Abs(px);
                    var ay = Mathf.Abs(py);
                    // pointy-top hex half-extents: |y| <= 1 and 0.866*|x| + 0.5*|y| <= 1
                    var d = Mathf.Max(ay, 0.8660254f * ax + 0.5f * ay); // 0 centre .. 1 edge
                    Color col;
                    if (d > 1f) col = new Color(0, 0, 0, 0);
                    else if (d > 0.86f) col = rim;                              // bright metallic rim
                    else if (d > 0.78f) col = Color.Lerp(rim, fill, (d - 0.78f) / 0.08f);
                    else col = fill;                                            // tinted interior
                    pixels[y * size + x] = col;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = $"RarityBadge_{cardRarity}", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);

            // Composite the gem centred on top, scaled to ~0.72 of the badge. Prefer the per-tier
            // gem (rarity_{common|rare|epic|legendary}); fall back to the single stat_rarity gem.
            var gem = GetRarityGem(cardRarity) ?? GetModularIcon("stat_rarity");
            if (gem != null && gem.texture != null)
            {
                var gp = gem.texture.GetPixels32();
                var gw = gem.texture.width;
                var gh = gem.texture.height;
                var gemSize = Mathf.RoundToInt(size * 0.72f);
                var off = (size - gemSize) / 2;
                for (var y = 0; y < gemSize; y++)
                {
                    var cy = off + y;
                    if (cy < 0 || cy >= size) continue;
                    var sy = y * gh / gemSize;
                    for (var x = 0; x < gemSize; x++)
                    {
                        var cx = off + x;
                        if (cx < 0 || cx >= size) continue;
                        var src = gp[sy * gw + (x * gw / gemSize)];
                        if (src.a == 0) continue;
                        var di = cy * size + cx;
                        pixels[di] = Over(src, pixels[di]);
                    }
                }
                tex.SetPixels32(pixels);
            }

            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"RarityBadge_{cardRarity}";
            _rarityBadgeCache[cardRarity] = sprite;
            return sprite;
        }

        // Rarity tints (opaque). Common grey, Rare blue, Epic purple, Legendary gold.
        private static Color RarityColor(int cardRarity) => cardRarity switch
        {
            1 => new Color(0.25f, 0.50f, 0.95f, 1f), // Rare      blue
            2 => new Color(0.60f, 0.30f, 0.90f, 1f), // Epic      purple
            3 => new Color(0.95f, 0.75f, 0.20f, 1f), // Legendary gold
            _ => new Color(0.55f, 0.58f, 0.62f, 1f)  // Common    grey
        };

        // Alpha-over an entire layer scaled to fill the whole canvas (nearest sample). The new
        // frames3 are authored at the surface aspect, so this is ~1:1 with no distortion.
        private static void AlphaOverFill(Color32[] canvas, int canvasW, int canvasH, Texture2D layer)
        {
            if (layer == null)
            {
                return;
            }
            var (lp, lw, lh) = GetPixelsCached(layer);
            if (lp == null || lw <= 0 || lh <= 0)
            {
                return;
            }
            for (var y = 0; y < canvasH; y++)
            {
                var ly = y * lh / canvasH; // both bottom-origin
                for (var x = 0; x < canvasW; x++)
                {
                    var src = lp[ly * lw + (x * lw / canvasW)];
                    if (src.a == 0)
                    {
                        continue;
                    }
                    var di = y * canvasW + x;
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

        // Faction tints (opaque) used to fill the art window when a card has no per-card illustration.
        private static Color32 FactionColor(int cardFaction) => cardFaction switch
        {
            0 => new Color32(0xE2, 0x49, 0x2B, 0xFF), // Ember  red-orange
            1 => new Color32(0x1E, 0x6F, 0xA8, 0xFF), // Tidal  deep blue
            2 => new Color32(0x3E, 0x8E, 0x41, 0xFF), // Grove  moss green
            3 => new Color32(0x8A, 0x93, 0xA0, 0xFF), // Alloy  steel
            4 => new Color32(0x5B, 0x2A, 0x86, 0xFF), // Void   purple
            _ => new Color32(0x55, 0x5B, 0x66, 0xFF)  // neutral grey
        };

        // Fills the art window (fraction of the canvas, TOP-left origin) with a flat opaque colour.
        // Mirrors BlitArtToWindow's coordinate mapping so the fill lands exactly in the frame window.
        private static void FillWindow(Color32[] canvas, int canvasW, int canvasH, Rect normWindowTopLeft, Color32 color)
        {
            var x0 = Mathf.Clamp(Mathf.RoundToInt(normWindowTopLeft.xMin * canvasW), 0, canvasW);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(normWindowTopLeft.xMax * canvasW), 0, canvasW);
            var y0 = Mathf.Clamp(Mathf.RoundToInt(canvasH - normWindowTopLeft.yMax * canvasH), 0, canvasH);
            var y1 = Mathf.Clamp(Mathf.RoundToInt(canvasH - normWindowTopLeft.yMin * canvasH), 0, canvasH);
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    canvas[y * canvasW + x] = color;
                }
            }
        }

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
            _texturePixelCache.Clear();
        }

        /// <summary>
        /// Warms the composite cache for a batch of cards, ONE card per call, so a caller can spread the
        /// CPU compositing across frames (e.g. on the match loading screen) instead of taking the whole
        /// burst synchronously when the hand first appears. Returns true if it actually built/looked up a
        /// composite for <paramref name="index"/>, false when the index is past the end of the list.
        /// Identical result to the lazy path — it just primes <see cref="_compositeCache"/> ahead of time
        /// (and <see cref="_texturePixelCache"/> for the shared frame/type-art layers).
        /// </summary>
        public static bool WarmCompositeAt(IReadOnlyList<(string cardId, int cardType, int cardRarity, int cardFaction, int unitType, bool hasArmor, bool board)> requests, int index)
        {
            if (requests == null || index < 0 || index >= requests.Count)
            {
                return false;
            }

            var r = requests[index];
            // Re-uses the normal cached path; only composites if not already cached.
            GetCardComposite(r.cardId, r.cardType, r.cardRarity, r.cardFaction, r.unitType, r.hasArmor, r.board ? "board" : "hand");
            return true;
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

    /// <summary>Stat sockets exposed by <see cref="CardArtLibrary"/> so overlays derive positions from them.</summary>
    public enum CardStatSocket
    {
        Cost,
        Attack,
        Rarity,
        Health,
        Armor,
        // Non-unit effect badges: a status icon sits above the +/- number on each column.
        EffectLeft,  // offensive column (damage / debuff), above the left number
        EffectRight  // defensive/grant column (heal/shield/buff), above the right number
    }
}
