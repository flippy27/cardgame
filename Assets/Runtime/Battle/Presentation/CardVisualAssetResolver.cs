using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
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

        [ShowInInspector] private Dictionary<string, CardVisualAssetEntry> _cache;
        private static Sprite _missingSprite;
        private static Texture2D _missingTexture;

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
            if (Instance == null)
            {
                Debug.LogError($"[CardVisualAssetResolver] No resolver instance found. assetRef: {assetRef}");
                return MissingSprite;
            }

            return Instance.ResolveSpriteInternal(assetRef);
        }

        public static Texture2D ResolveTexture(string assetRef)
        {
            return Instance != null ? Instance.ResolveTextureInternal(assetRef) : MissingTexture;
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
                Debug.LogWarning("[CardVisualAssetResolver] Empty assetRef received. Showing magenta placeholder.", this);
                return MissingSprite;
            }

            assetRef = assetRef.Trim();

            if (_cache == null)
            {
                RebuildCache();
            }

            if (!_cache.TryGetValue(assetRef, out var entry))
            {
                Debug.LogWarning($"[CardVisualAssetResolver] Missing backend assetRef '{assetRef}' in resolver cache. Showing magenta placeholder.", this);
                return MissingSprite;
            }

            if (entry.sprite != null)
            {
                return entry.sprite;
            }

            if (entry.texture != null)
            {
                var sprite = Sprite.Create(
                    entry.texture,
                    new Rect(0f, 0f, entry.texture.width, entry.texture.height),
                    new Vector2(0.5f, 0.5f)
                );

                return sprite;
            }

            Debug.LogWarning($"[CardVisualAssetResolver] AssetRef exists but has no sprite or texture assigned: '{assetRef}'.", this);
            return MissingSprite;
        }

        private Texture2D ResolveTextureInternal(string assetRef)
        {
            if (string.IsNullOrWhiteSpace(assetRef))
            {
                return MissingTexture;
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

            return MissingTexture;
        }

        public static Sprite MissingSprite
        {
            get
            {
                if (_missingSprite != null)
                {
                    return _missingSprite;
                }

                _missingSprite = Sprite.Create(
                    MissingTexture,
                    new Rect(0f, 0f, MissingTexture.width, MissingTexture.height),
                    new Vector2(0.5f, 0.5f));
                _missingSprite.name = "MissingCardVisual_Magenta";
                return _missingSprite;
            }
        }

        public static Texture2D MissingTexture
        {
            get
            {
                if (_missingTexture != null)
                {
                    return _missingTexture;
                }

                _missingTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "MissingCardVisual_Magenta"
                };

                var pixels = new[]
                {
                    Color.magenta, Color.black, Color.magenta, Color.black,
                    Color.black, Color.magenta, Color.black, Color.magenta,
                    Color.magenta, Color.black, Color.magenta, Color.black,
                    Color.black, Color.magenta, Color.black, Color.magenta
                };
                _missingTexture.SetPixels(pixels);
                _missingTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                return _missingTexture;
            }
        }
    }
}
