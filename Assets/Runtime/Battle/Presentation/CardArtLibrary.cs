using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Client-side card art resolver. Maps a stable <c>cardId</c> to a single Sprite.
    ///
    /// This replaces the old server-driven layered visual system: the server no longer
    /// sends visual profiles / layers / asset refs. Art lives entirely in the client and
    /// is resolved here by convention from the card's id (and rarity for the frame).
    ///
    /// Current backend: <see cref="Resources"/> (art shipped in the build). Art files live at
    ///   Assets/Resources/CardArt/{cardId}.png      → per-card illustration
    ///   Assets/Resources/CardArt/frames/frame_{rarity}.png → rarity frame (0..3)
    /// Use <c>Tools/CardDuel/Generate Placeholder Card Art</c> to fill these with placeholders.
    ///
    /// ROLLOUT (future): swap the body of <see cref="LoadSprite"/> for an Addressables load
    /// (Addressables.LoadAssetAsync&lt;Sprite&gt;($"CardArt/{cardId}")) so new cards can be
    /// downloaded from a remote content catalog at the menu without an app store release.
    /// The public API here stays the same; only the loader changes.
    /// </summary>
    public static class CardArtLibrary
    {
        public const string ResourceRoot = "CardArt";

        private static readonly Dictionary<string, Sprite> _artCache = new();
        private static readonly Dictionary<int, Sprite> _frameCache = new();
        private static Sprite _missing;

        /// <summary>Per-card illustration. Returns <see cref="Missing"/> if no art exists yet.</summary>
        public static Sprite GetCardArt(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return Missing;
            }

            if (_artCache.TryGetValue(cardId, out var cached))
            {
                return cached ?? Missing;
            }

            var sprite = LoadSprite($"{ResourceRoot}/{cardId}");
            _artCache[cardId] = sprite;
            return sprite ?? Missing;
        }

        /// <summary>Rarity frame sprite. rarity: 0 Common, 1 Rare, 2 Epic, 3 Legendary. Null if none.</summary>
        public static Sprite GetFrame(int cardRarity)
        {
            if (_frameCache.TryGetValue(cardRarity, out var cached))
            {
                return cached;
            }

            var sprite = LoadSprite($"{ResourceRoot}/frames/frame_{cardRarity}");
            _frameCache[cardRarity] = sprite;
            return sprite;
        }

        /// <summary>Magenta fallback sprite shown when a card has no art yet.</summary>
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

        /// <summary>Drop the in-memory cache (call after a content update / addressables refresh).</summary>
        public static void ClearCache()
        {
            _artCache.Clear();
            _frameCache.Clear();
        }

        // Single point of asset loading. Swap this for Addressables to enable remote rollout.
        private static Sprite LoadSprite(string resourcePath)
        {
            return Resources.Load<Sprite>(resourcePath);
        }
    }
}
