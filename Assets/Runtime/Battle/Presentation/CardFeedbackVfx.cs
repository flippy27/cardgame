using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Lightweight, code-built particle bursts for card game-feel (no prefabs/assets required).
    /// Each call spawns a one-shot ParticleSystem that auto-destroys. Material/texture are generated
    /// once so there are no missing references (no magenta particles).
    /// </summary>
    public static class CardFeedbackVfx
    {
        private static Material _material;
        private static Texture2D _texture;

        private static Material ParticleMaterial
        {
            get
            {
                if (_material != null)
                {
                    return _material;
                }

                _texture = BuildSoftDot(32);
                // Sprites/Default exists in both built-in and URP and alpha-blends particle quads.
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
                _material = new Material(shader) { mainTexture = _texture };
                _material.hideFlags = HideFlags.HideAndDontSave;
                return _material;
            }
        }

        private static Texture2D BuildSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var c = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    var a = Mathf.Clamp01(1f - d);
                    a *= a; // softer falloff
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Card vanishing into a big cloud of dispersing particles that briefly covers the card.</summary>
        public static void Disperse(Vector3 position, Color tint)
        {
            Spawn(position, Quaternion.identity, tint, count: 46, speed: 2.6f, size: 1.5f,
                life: 0.55f, gravity: -0.15f, radius: 1.6f, ParticleSystemShapeType.Sphere);
        }

        /// <summary>Burst when a card is picked up from hand (ghost appears) — large enough to cover the card.</summary>
        public static void PickupSparkle(Vector3 position, Color tint)
        {
            Spawn(position, Quaternion.identity, tint, count: 36, speed: 1.8f, size: 1.2f,
                life: 0.45f, gravity: -0.25f, radius: 1.3f, ParticleSystemShapeType.Sphere);
        }

        /// <summary>Dirt/dust kicked up when a card lands on the board.</summary>
        public static void LandImpact(Vector3 position)
        {
            // Cone pointing up (+Y): emits outward-and-up, gravity drags it back down like kicked terrain.
            Spawn(position, Quaternion.Euler(-90f, 0f, 0f), new Color(0.62f, 0.5f, 0.36f, 1f),
                count: 24, speed: 1.9f, size: 0.16f, life: 0.45f, gravity: 1.4f, radius: 0.2f,
                ParticleSystemShapeType.Cone);
        }

        private static void Spawn(Vector3 position, Quaternion rotation, Color tint, int count, float speed,
            float size, float life, float gravity, float radius, ParticleSystemShapeType shapeType)
        {
            var go = new GameObject("CardFeedbackVfx");
            go.transform.SetPositionAndRotation(position, rotation);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = tint;
            main.gravityModifier = gravity;
            main.maxParticles = count + 4;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Cone)
            {
                shape.angle = 28f;
            }

            // Fade + shrink over life so the burst dissolves cleanly.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 100;

            ps.Play();
            Object.Destroy(go, life + 0.4f);
        }
    }
}
