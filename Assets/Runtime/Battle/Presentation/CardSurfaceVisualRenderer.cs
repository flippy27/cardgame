using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    [Serializable]
    public sealed class CardVisualLayerBinding
    {
        public string layer = "art";
        public Image image;
        public RawImage rawImage;
        public SpriteRenderer spriteRenderer;
        public Renderer materialRenderer;
        public string materialTextureProperty = "_MainTex";

        public void Clear()
        {
            if (image != null)
            {
                image.sprite = null;
                image.enabled = false;
            }

            if (rawImage != null)
            {
                rawImage.texture = null;
                rawImage.enabled = false;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
                spriteRenderer.enabled = false;
            }

            if (materialRenderer != null && materialRenderer.material != null)
            {
                SetMaterialTexture(materialRenderer.material, null);
            }
        }

        public void Apply(Sprite sprite, Texture texture)
        {
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
            }

            if (rawImage != null)
            {
                rawImage.texture = texture;
                rawImage.enabled = texture != null;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.enabled = sprite != null;
            }

            if (materialRenderer != null && materialRenderer.material != null)
            {
                SetMaterialTexture(materialRenderer.material, texture);
            }
        }

        // Sets the card texture on whatever main-texture property the shader exposes (URP _BaseMap
        // and/or built-in _MainTex) and forces the base color to white so the art shows untinted.
        private void SetMaterialTexture(Material material, Texture texture)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (texture != null)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", Color.white);
                }
                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", Color.white);
                }

                // The card quads face the camera flipped 180deg in-plane, so the art reads
                // upside-down/mirrored. Rotate the texture 180deg via tiling (-1) + offset (1)
                // ONLY on the 3D material path; UI Image/SpriteRenderer bindings stay upright.
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
        }
    }

    /// <summary>
    /// Renders a card's art onto one or more bound surfaces (Image / RawImage / SpriteRenderer /
    /// material). Art is resolved client-side by <c>cardId</c> through <see cref="CardArtLibrary"/>.
    ///
    /// The old server-driven layered composition (visual profiles + asset-ref layers fetched from
    /// the API) is gone. Bindings whose <c>layer</c> is "frame" get the rarity frame; every other
    /// binding (including the default single "art" binding wired by the deck/battle views) gets the
    /// per-card illustration. The public API (<see cref="ApplyCard"/>, EnsureDefault*Binding) is
    /// unchanged so existing prefabs and call sites keep working.
    /// </summary>
    public sealed class CardSurfaceVisualRenderer : MonoBehaviour
    {
        [SerializeField] private string defaultSurface = "hand";
        [SerializeField] private bool clearBindingsWhenMissing;
        [SerializeField] private CardVisualLayerBinding[] layerBindings;

        public void ApplyCard(string cardId, string surfaceOverride = null, string profileKeyOverride = null)
        {
            // surfaceOverride / profileKeyOverride are kept for call-site compatibility but no longer
            // change resolution: a card now has a single illustration regardless of surface.
            if (string.IsNullOrWhiteSpace(cardId))
            {
                if (clearBindingsWhenMissing)
                {
                    ClearBindings();
                }

                return;
            }

            ApplyArt(cardId);
        }

        public void EnsureDefaultMaterialBinding(Renderer renderer, string surface = null)
        {
            if (renderer == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(surface))
            {
                defaultSurface = surface;
            }

            if (HasAnyBindings())
            {
                return;
            }

            layerBindings = new[]
            {
                new CardVisualLayerBinding
                {
                    layer = "art",
                    materialRenderer = renderer
                }
            };
        }

        public void EnsureDefaultImageBinding(Image image, string surface = null)
        {
            if (image == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(surface))
            {
                defaultSurface = surface;
            }

            if (HasAnyBindings())
            {
                return;
            }

            layerBindings = new[]
            {
                new CardVisualLayerBinding
                {
                    layer = "art",
                    image = image
                }
            };
        }

        private void ApplyArt(string cardId)
        {
            if (layerBindings == null || layerBindings.Length == 0)
            {
                return;
            }

            // The composite already bakes in the type/rarity frame + faction overlay/crest,
            // so the single "art" binding shows the full card. Any explicit "frame" binding is
            // cleared to avoid drawing the frame twice.
            var (cardType, cardRarity, cardFaction) = ResolveCardMeta(cardId);
            var composite = CardArtLibrary.GetCardComposite(cardId, cardType, cardRarity, cardFaction);

            foreach (var binding in layerBindings)
            {
                if (binding == null)
                {
                    continue;
                }

                if (string.Equals(binding.layer, "frame", StringComparison.OrdinalIgnoreCase))
                {
                    binding.Clear();
                    continue;
                }

                binding.Apply(composite, composite != null ? composite.texture : null);
            }
        }

        private static (int cardType, int cardRarity, int cardFaction) ResolveCardMeta(string cardId)
        {
            try
            {
                var catalog = GameService.Instance?.CardCatalog;
                if (catalog != null && catalog.TryGetCard(cardId, out ServerCardDefinition definition) && definition != null)
                {
                    return (definition.cardType, definition.cardRarity, definition.cardFaction);
                }
            }
            catch (Exception)
            {
                // Catalog not ready / lookup failed — composite falls back to defaults/raw art.
            }

            return (-1, -1, -1);
        }

        private void ClearBindings()
        {
            if (layerBindings == null)
            {
                return;
            }

            foreach (var binding in layerBindings)
            {
                binding?.Clear();
            }
        }

        private bool HasAnyBindings()
        {
            return layerBindings != null && layerBindings.Any(binding => binding != null);
        }
    }
}
