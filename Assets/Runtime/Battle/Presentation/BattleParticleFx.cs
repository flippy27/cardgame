using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Procedural battle particle effects — built entirely in code (no Asset Store import, no prefabs,
    /// no manual wiring), same philosophy as CardArtLibrary generating sprites at runtime. Each effect
    /// key (card_damage, heal, death, shield, poison, stun, magic, frost_magic, status_applied, …) maps
    /// to a one-shot ParticleSystem "recipe" (colour / count / speed / size / lifetime / gravity). A soft
    /// round particle texture + an unlit additive material are generated once and shared.
    ///
    /// Single source of truth: tune a recipe here and every call site that plays that key updates.
    /// <see cref="BattleVfxPlayer.Play"/> prefers an imported CFX prefab first, then these procedural
    /// particles, then the legacy sprite frames.
    /// </summary>
    public static class BattleParticleFx
    {
        private struct Recipe
        {
            public Color A;        // start colour (min)
            public Color B;        // start colour (max) — particles randomise between A..B
            public int Count;      // burst size
            public float Speed;    // start speed
            public float Size;     // start size (world units, pre-scale)
            public float Life;     // start lifetime
            public float Gravity;  // gravityModifier (negative = rise, positive = fall)
        }

        private static readonly Color White = new(1f, 1f, 0.92f);
        private static readonly Dictionary<string, Recipe> Recipes = new()
        {
            { "card_damage",  new Recipe { A = White, B = new Color(1f, 0.55f, 0.2f), Count = 24, Speed = 5.5f, Size = 0.45f, Life = 0.45f, Gravity = 0.2f } },
            { "magic",        new Recipe { A = new Color(0.72f, 0.42f, 1f), B = new Color(0.92f, 0.72f, 1f), Count = 24, Speed = 4.5f, Size = 0.5f, Life = 0.55f, Gravity = 0f } },
            { "frost_magic",  new Recipe { A = new Color(0.6f, 0.85f, 1f), B = new Color(0.88f, 0.96f, 1f), Count = 24, Speed = 4.5f, Size = 0.45f, Life = 0.55f, Gravity = 0.3f } },
            { "heal",         new Recipe { A = new Color(0.45f, 1f, 0.5f), B = new Color(0.8f, 1f, 0.65f), Count = 18, Speed = 2.2f, Size = 0.5f, Life = 0.95f, Gravity = -2.2f } },
            { "death",        new Recipe { A = new Color(0.42f, 0.42f, 0.48f), B = new Color(0.14f, 0.14f, 0.18f), Count = 28, Speed = 3f, Size = 0.62f, Life = 0.85f, Gravity = 1.6f } },
            { "shield",       new Recipe { A = new Color(0.4f, 0.85f, 1f), B = new Color(0.72f, 0.95f, 1f), Count = 26, Speed = 4f, Size = 0.42f, Life = 0.6f, Gravity = 0f } },
            { "poison",       new Recipe { A = new Color(0.45f, 0.88f, 0.22f), B = new Color(0.2f, 0.6f, 0.12f), Count = 18, Speed = 1.6f, Size = 0.7f, Life = 1.0f, Gravity = -0.4f } },
            { "stun",         new Recipe { A = new Color(1f, 0.9f, 0.3f), B = new Color(1f, 1f, 0.65f), Count = 16, Speed = 3f, Size = 0.45f, Life = 0.7f, Gravity = -0.6f } },
            { "status_applied", new Recipe { A = new Color(1f, 0.95f, 0.5f), B = new Color(1f, 1f, 0.82f), Count = 16, Speed = 2f, Size = 0.42f, Life = 0.85f, Gravity = -1.6f } },
            { "status_expired", new Recipe { A = new Color(0.6f, 0.6f, 0.66f), B = new Color(0.35f, 0.35f, 0.4f), Count = 12, Speed = 1.6f, Size = 0.45f, Life = 0.7f, Gravity = 1.0f } },
            { "skill_begin",  new Recipe { A = White, B = new Color(0.8f, 0.85f, 1f), Count = 20, Speed = 4f, Size = 0.5f, Life = 0.5f, Gravity = 0f } },
        };

        // Variant keys that reuse a base recipe.
        private static readonly Dictionary<string, string> Aliases = new()
        {
            { "hero_damage", "card_damage" }, { "card_counterattack", "card_damage" }, { "hit", "card_damage" },
            { "shielded", "shield" }, { "shield_block", "shield" },
            { "poisoned", "poison" },
            { "stunned", "stun" }, { "stun_skip", "stun" },
            { "status", "status_applied" }, { "buff", "status_applied" },
        };

        private static Texture2D _dot;
        private static Material _mat;

        /// <summary>
        /// Spawns the procedural particle burst for an effect key at a world position and auto-destroys
        /// it. Returns false if the key has no recipe (caller falls back to sprite frames).
        /// </summary>
        public static bool Play(string key, Vector3 worldPosition, float scale)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!Recipes.TryGetValue(key, out var r))
            {
                if (!Aliases.TryGetValue(key, out var baseKey) || !Recipes.TryGetValue(baseKey, out r))
                {
                    return false;
                }
            }

            var go = new GameObject($"pfx_{key}");
            go.transform.position = worldPosition;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = r.Life;
            main.startSpeed = r.Speed * Mathf.Max(0.2f, scale);
            main.startSize = r.Size * Mathf.Max(0.2f, scale);
            main.gravityModifier = r.Gravity;
            main.maxParticles = r.Count + 16;
            main.startColor = new ParticleSystem.MinMaxGradient(r.A, r.B);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)r.Count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f * Mathf.Max(0.2f, scale);

            // Fade out over life.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Shrink slightly over life.
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = SharedMaterial();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = 1000;

            ps.Play();
            Object.Destroy(go, r.Life + 0.6f);
            return true;
        }

        private static Material SharedMaterial()
        {
            if (_mat != null) return _mat;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Mobile/Particles/Additive")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
            _mat = new Material(sh) { name = "BattleParticleFxMat" };
            _mat.mainTexture = DotTexture();
            if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", DotTexture());
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", Color.white);
            return _mat;
        }

        // Soft round particle: white centre fading to transparent edge.
        private static Texture2D DotTexture()
        {
            if (_dot != null) return _dot;
            const int n = 64;
            var c = (n - 1) * 0.5f;
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var dx = (x - c) / c;
                    var dy = (y - c) / c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);          // 0 centre .. 1 edge
                    var a = Mathf.Clamp01(1f - d);
                    a = a * a;                                       // soft falloff
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BattleParticleDot", wrapMode = TextureWrapMode.Clamp };
            _dot.SetPixels32(px);
            _dot.Apply(false, false);
            return _dot;
        }
    }
}
