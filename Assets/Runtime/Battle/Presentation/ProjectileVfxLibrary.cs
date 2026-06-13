using System;
using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Loads per-faction projectile sprite frames from the art pack
    /// (Resources/Art/projectiles/{faction}/projectile_{faction}_NN.png, 128x128 each).
    /// Faction is resolved from a card's <c>cardId</c> prefix (ember_/tidal_/grove_/alloy_/void_).
    /// Cached. Returns null when the faction art is missing so callers can fall back gracefully.
    /// </summary>
    public static class ProjectileVfxLibrary
    {
        private const string Root = "Art/projectiles";

        // Per-faction default projectile tints (used to tint the fallback primitive when no
        // sprite is available, and to lightly multiply the sprite for extra punch).
        private static readonly Dictionary<string, Color> FactionTints = new(StringComparer.Ordinal)
        {
            { "ember", new Color(1f, 0.55f, 0.18f, 1f) },
            { "tidal", new Color(0.32f, 0.7f, 1f, 1f) },
            { "grove", new Color(0.45f, 0.95f, 0.4f, 1f) },
            { "alloy", new Color(0.85f, 0.85f, 0.92f, 1f) },
            { "void", new Color(0.7f, 0.4f, 1f, 1f) },
        };

        // Default amber tint matching AttackMotionPreset.projectileTint, used when faction is unknown.
        public static readonly Color DefaultTint = new(1f, 0.85f, 0.2f, 1f);

        private static readonly string[] KnownFactions = { "ember", "tidal", "grove", "alloy", "void" };

        private static readonly Dictionary<string, Sprite[]> _frameCache = new(StringComparer.Ordinal);

        /// <summary>
        /// Resolves a faction key (ember/tidal/grove/alloy/void) from card data. Uses the cardId
        /// prefix first; returns null when nothing recognizable is found.
        /// </summary>
        public static string ResolveFaction(BoardCardDto card)
        {
            if (card == null)
            {
                return null;
            }

            return ResolveFaction(card.cardId) ?? ResolveFaction(card.runtimeId) ?? ResolveFaction(card.displayName);
        }

        public static string ResolveFaction(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            var lowered = identifier.Trim().ToLowerInvariant();
            foreach (var faction in KnownFactions)
            {
                // match "ember_dragon", "ember-dragon", or a bare "ember"
                if (lowered.StartsWith(faction + "_", StringComparison.Ordinal) ||
                    lowered.StartsWith(faction + "-", StringComparison.Ordinal) ||
                    lowered.Equals(faction, StringComparison.Ordinal) ||
                    lowered.Contains("_" + faction + "_"))
                {
                    return faction;
                }
            }

            return null;
        }

        /// <summary>Returns the tint for a faction (or the default amber when unknown/null).</summary>
        public static Color GetTint(string faction)
        {
            if (!string.IsNullOrEmpty(faction) && FactionTints.TryGetValue(faction, out var tint))
            {
                return tint;
            }

            return DefaultTint;
        }

        /// <summary>
        /// Loads the animated projectile frames for a faction. Returns null when the faction is
        /// unknown or the art is missing (caller should fall back to a tinted primitive).
        /// </summary>
        public static Sprite[] GetFrames(string faction)
        {
            if (string.IsNullOrWhiteSpace(faction))
            {
                return null;
            }

            if (_frameCache.TryGetValue(faction, out var cached))
            {
                return cached;
            }

            var frames = new List<Sprite>(4);
            for (var i = 1; i <= 8; i++)
            {
                var sprite = Resources.Load<Sprite>($"{Root}/{faction}/projectile_{faction}_{i:00}");
                if (sprite == null)
                {
                    break;
                }

                frames.Add(sprite);
            }

            var result = frames.Count > 0 ? frames.ToArray() : null;
            _frameCache[faction] = result;
            return result;
        }

        public static void ClearCache() => _frameCache.Clear();
    }
}
