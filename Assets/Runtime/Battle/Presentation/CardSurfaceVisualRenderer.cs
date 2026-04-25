using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Data;
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

            if (materialRenderer != null && materialRenderer.material != null && materialRenderer.material.HasProperty(materialTextureProperty))
            {
                materialRenderer.material.SetTexture(materialTextureProperty, null);
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

            if (materialRenderer != null && materialRenderer.material != null && materialRenderer.material.HasProperty(materialTextureProperty))
            {
                materialRenderer.material.SetTexture(materialTextureProperty, texture);
            }
        }
    }

    public sealed class CardSurfaceVisualRenderer : MonoBehaviour
    {
        [SerializeField] private string defaultSurface = "hand";
        [SerializeField] private string requestedProfileKey;
        [SerializeField] private bool fetchDetailedCardData = true;
        [SerializeField] private bool clearBindingsWhenMissing;
        [SerializeField] private CardVisualLayerBinding[] layerBindings;

        private int _requestVersion;

        public void ApplyCard(string cardId, string surfaceOverride = null, string profileKeyOverride = null)
        {
            _requestVersion++;
            _ = ApplyCardAsync(cardId, surfaceOverride ?? defaultSurface, profileKeyOverride ?? requestedProfileKey, _requestVersion);
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

        private async Task ApplyCardAsync(string cardId, string surface, string profileKey, int requestVersion)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                if (clearBindingsWhenMissing)
                {
                    ClearBindings();
                }

                return;
            }

            try
            {
                var definition = await CardVisualCompositionResolver.ResolveCardDefinitionAsync(cardId, fetchDetailedCardData);
                if (this == null || requestVersion != _requestVersion)
                {
                    return;
                }

                var resolvedLayers = CardVisualCompositionResolver.ResolveLayers(definition, surface, profileKey);
                if (resolvedLayers.Count > 0)
                {
                    ApplyResolvedLayers(resolvedLayers);
                    return;
                }

                ApplyLocalFallback(cardId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CardVisuals] Could not resolve visuals for '{cardId}' on surface '{surface}': {ex.Message}");
                ApplyLocalFallback(cardId);
            }
        }

        private void ApplyResolvedLayers(IReadOnlyList<CardVisualLayerDto> layers)
        {
            if (layerBindings == null || layerBindings.Length == 0)
            {
                return;
            }

            var appliedBindings = new HashSet<CardVisualLayerBinding>();
            foreach (var layer in layers)
            {
                if (layer == null || string.IsNullOrWhiteSpace(layer.layer))
                {
                    continue;
                }

                var binding = FindBinding(layer.layer);
                if (binding == null)
                {
                    continue;
                }

                var sprite = CardVisualAssetResolver.ResolveSprite(layer.assetRef);
                var texture = sprite != null ? sprite.texture : CardVisualAssetResolver.ResolveTexture(layer.assetRef);
                binding.Apply(sprite, texture);
                appliedBindings.Add(binding);
            }

            foreach (var binding in layerBindings)
            {
                if (binding == null)
                {
                    continue;
                }

                if (!appliedBindings.Contains(binding))
                {
                    binding.Clear();
                }
            }
        }

        private void ApplyLocalFallback(string cardId)
        {
            var localDefinition = CardRegistry.GetCard(cardId);
            var localProfile = localDefinition?.visualProfile;
            if (localProfile == null)
            {
                if (clearBindingsWhenMissing)
                {
                    ClearBindings();
                }

                return;
            }

            ApplyFallbackLayer("frame", localProfile.frame);
            ApplyFallbackLayer("art", localProfile.artwork);
            ApplyFallbackLayer("icon", localProfile.icon);
        }

        private void ApplyFallbackLayer(string layerKey, Sprite sprite)
        {
            var binding = FindBinding(layerKey);
            if (binding == null)
            {
                return;
            }

            binding.Apply(sprite, sprite != null ? sprite.texture : null);
        }

        private CardVisualLayerBinding FindBinding(string layerKey)
        {
            if (layerBindings == null || layerBindings.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(layerKey))
            {
                foreach (var binding in layerBindings)
                {
                    if (binding != null && string.Equals(binding.layer, layerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return binding;
                    }
                }
            }

            if (layerBindings.Length == 1)
            {
                return layerBindings[0];
            }

            return layerBindings.FirstOrDefault(binding => binding != null);
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

    internal static class CardVisualCompositionResolver
    {
        public static async Task<ServerCardDefinition> ResolveCardDefinitionAsync(string cardId, bool fetchDetailedCardData)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            var gameService = GameService.Instance;
            var catalog = GameService.Instance?.CardCatalog;
            ServerCardDefinition definition = null;
            if (catalog != null)
            {
                catalog.TryGetCard(cardId, out definition);
                if (fetchDetailedCardData)
                {
                    definition = await catalog.EnsureCardDetailsLoaded(cardId) ?? definition;
                }
            }

            if (definition == null && fetchDetailedCardData && gameService?.ApiClient != null)
            {
                try
                {
                    definition = await gameService.ApiClient.FetchCard(cardId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CardVisuals] Could not fetch detailed card '{cardId}': {ex.Message}");
                }
            }

            return definition;
        }

        public static List<CardVisualLayerDto> ResolveLayers(ServerCardDefinition definition, string surface, string requestedProfileKey)
        {
            var results = new List<CardVisualLayerDto>();
            if (definition?.visualProfiles == null || definition.visualProfiles.Length == 0)
            {
                return results;
            }

            var normalizedSurface = string.IsNullOrWhiteSpace(surface) ? "hand" : surface.Trim().ToLowerInvariant();
            var selectedProfile = SelectProfile(definition.visualProfiles, normalizedSurface, requestedProfileKey);
            if (selectedProfile?.layers == null)
            {
                return results;
            }

            results.AddRange(selectedProfile.layers
                .Where(layer => layer != null && string.Equals(NormalizeSurface(layer.surface), normalizedSurface, StringComparison.Ordinal))
                .OrderBy(layer => layer.sortOrder));

            return results;
        }

        private static CardVisualProfileDto SelectProfile(CardVisualProfileDto[] profiles, string surface, string requestedProfileKey)
        {
            if (profiles == null || profiles.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(requestedProfileKey))
            {
                var exactMatch = profiles.FirstOrDefault(profile =>
                    profile != null &&
                    string.Equals(profile.profileKey, requestedProfileKey, StringComparison.OrdinalIgnoreCase) &&
                    ContainsSurface(profile, surface));
                if (exactMatch != null)
                {
                    return exactMatch;
                }
            }

            var defaultMatch = profiles.FirstOrDefault(profile =>
                profile != null &&
                profile.isDefault &&
                ContainsSurface(profile, surface));
            if (defaultMatch != null)
            {
                return defaultMatch;
            }

            return profiles.FirstOrDefault(profile => profile != null && ContainsSurface(profile, surface));
        }

        private static bool ContainsSurface(CardVisualProfileDto profile, string surface)
        {
            if (profile?.layers == null)
            {
                return false;
            }

            for (var index = 0; index < profile.layers.Length; index++)
            {
                var layer = profile.layers[index];
                if (layer != null && string.Equals(NormalizeSurface(layer.surface), surface, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeSurface(string surface)
        {
            return string.IsNullOrWhiteSpace(surface) ? string.Empty : surface.Trim().ToLowerInvariant();
        }
    }
}
