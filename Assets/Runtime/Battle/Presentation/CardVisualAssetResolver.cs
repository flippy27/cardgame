using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Flippy.CardDuelMobile.UI
{
    [Serializable]
    public sealed class CardVisualAssetEntry
    {
        public string assetRef;
        public Sprite sprite;
        public Texture2D texture;
    }

    public sealed class CardVisualAssetResolver : MonoBehaviour
    {
        [Header("Card Surfaces")]
        [SerializeField] private CardVisualAssetEntry[] frameAssets;
        [SerializeField] private CardVisualAssetEntry[] artAssets;

        [Header("Icons")]
        [SerializeField] private CardVisualAssetEntry[] attackIconAssets;
        [SerializeField] private CardVisualAssetEntry[] skillIconAssets;
        [SerializeField] private CardVisualAssetEntry[] statusIconAssets;

        [Header("Fallback / Migration")]
        [SerializeField] private CardVisualAssetEntry[] miscAssets;
        [FormerlySerializedAs("assetEntries")]
        [SerializeField] private CardVisualAssetEntry[] legacyAssetEntries;

        public static CardVisualAssetResolver Instance { get; private set; }

        private Dictionary<string, CardVisualAssetEntry> _cache;
        private readonly Dictionary<string, Sprite> _runtimeSpriteCache = new(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            RebuildCache();
        }

        private void OnValidate()
        {
            RebuildCache();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static Sprite ResolveSprite(string assetRef)
        {
            return Instance != null ? Instance.ResolveSpriteInternal(assetRef) : ResolveSpriteFallback(assetRef, null);
        }

        public static Texture2D ResolveTexture(string assetRef)
        {
            return Instance != null ? Instance.ResolveTextureInternal(assetRef) : ResolveTextureFallback(assetRef);
        }

        private void RebuildCache()
        {
            _cache = new Dictionary<string, CardVisualAssetEntry>(StringComparer.OrdinalIgnoreCase);

            AddEntries(legacyAssetEntries);
            AddEntries(frameAssets);
            AddEntries(artAssets);
            AddEntries(attackIconAssets);
            AddEntries(skillIconAssets);
            AddEntries(statusIconAssets);
            AddEntries(miscAssets);
        }

        private void AddEntries(IEnumerable<CardVisualAssetEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.assetRef))
                {
                    continue;
                }

                _cache[entry.assetRef.Trim()] = entry;
            }
        }

        private Sprite ResolveSpriteInternal(string assetRef)
        {
            if (string.IsNullOrWhiteSpace(assetRef))
            {
                return null;
            }

            if (_cache != null && _cache.TryGetValue(assetRef, out var entry) && entry.sprite != null)
            {
                return entry.sprite;
            }

            return ResolveSpriteFallback(assetRef, _runtimeSpriteCache);
        }

        private Texture2D ResolveTextureInternal(string assetRef)
        {
            if (string.IsNullOrWhiteSpace(assetRef))
            {
                return null;
            }

            if (_cache != null && _cache.TryGetValue(assetRef, out var entry))
            {
                if (entry.texture != null)
                {
                    return entry.texture;
                }

                if (entry.sprite != null)
                {
                    return entry.sprite.texture;
                }
            }

            return ResolveTextureFallback(assetRef);
        }

        private static Sprite ResolveSpriteFallback(string assetRef, Dictionary<string, Sprite> runtimeCache)
        {
            if (string.IsNullOrWhiteSpace(assetRef))
            {
                return null;
            }

            if (runtimeCache != null && runtimeCache.TryGetValue(assetRef, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>(assetRef);
            if (sprite != null)
            {
                if (runtimeCache != null)
                {
                    runtimeCache[assetRef] = sprite;
                }

                return sprite;
            }

            var texture = ResolveTextureFallback(assetRef);
            if (texture == null)
            {
                return null;
            }

            var generatedSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            if (runtimeCache != null)
            {
                runtimeCache[assetRef] = generatedSprite;
            }

            return generatedSprite;
        }

        private static Texture2D ResolveTextureFallback(string assetRef)
        {
            if (string.IsNullOrWhiteSpace(assetRef))
            {
                return null;
            }

            var sprite = Resources.Load<Sprite>(assetRef);
            if (sprite != null)
            {
                return sprite.texture;
            }

            return Resources.Load<Texture2D>(assetRef);
        }
    }
}
