using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Loads battle VFX frame sequences from the art pack (Resources/Art/vfx/{name}/vfx_{name}_NN.png,
    /// 4x 256x256 frames per effect). Cached. Returns null if the effect is missing.
    /// </summary>
    public static class BattleVfxLibrary
    {
        private const string Root = "Art/vfx";
        private static readonly Dictionary<string, Sprite[]> _cache = new();

        public static Sprite[] GetFrames(string vfxName)
        {
            if (string.IsNullOrWhiteSpace(vfxName))
            {
                return null;
            }

            if (_cache.TryGetValue(vfxName, out var cached))
            {
                return cached;
            }

            var frames = new List<Sprite>(4);
            for (var i = 1; i <= 8; i++)
            {
                var sprite = Resources.Load<Sprite>($"{Root}/{vfxName}/vfx_{vfxName}_{i:00}");
                if (sprite == null)
                {
                    break;
                }
                frames.Add(sprite);
            }

            var result = frames.Count > 0 ? frames.ToArray() : null;
            _cache[vfxName] = result;
            return result;
        }

        public static void ClearCache() => _cache.Clear();
    }

    /// <summary>
    /// Spawns a short, billboarded sprite animation at a world position for battle events
    /// (hit, heal, death, shield, status, skill cues...). Auto-creates itself on first use, so no
    /// prefab/scene wiring is required — call <c>BattleVfxPlayer.Instance.Play(name, worldPos)</c>.
    /// Each instance plays its frames once and then destroys itself.
    /// </summary>
    public sealed class BattleVfxPlayer : MonoBehaviour
    {
        private const float DefaultFps = 14f;
        private const float DefaultWorldScale = 0.6f; // 256px @100PPU ~2.56u; *0.6 ~ card-ish

        private static BattleVfxPlayer _instance;

        public static BattleVfxPlayer Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("BattleVfxPlayer");
                    _instance = go.AddComponent<BattleVfxPlayer>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Cartoon-FX (or any) PARTICLE prefabs, dropped at Resources/Art/vfx/cfx/{key}.prefab. When a
        // prefab exists for an effect key it is preferred over the legacy sprite-frame animation, so the
        // whole battle upgrades to particles just by importing the pack and placing named prefabs — one
        // mapping place (the cfx/ folder), no per-call-site changes. Cached (misses too).
        private const string CfxRoot = "Art/vfx/cfx";
        private static readonly Dictionary<string, GameObject> _prefabCache = new();
        // Per-key world scale override (CFX prefabs are authored ~1u; the board scales cards up ~4x, so
        // particles may need a bump). Tune here in ONE place.
        public static float ParticleWorldScale = 3.5f;

        private static GameObject GetParticlePrefab(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (_prefabCache.TryGetValue(key, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>($"{CfxRoot}/{key}");
            _prefabCache[key] = prefab; // cache misses too (avoid repeated I/O)
            return prefab;
        }

        /// <summary>
        /// Instantiates a particle (Cartoon FX) prefab for an effect key at a world position and
        /// auto-destroys it when the systems finish. Returns false when no prefab exists for the key
        /// (so callers can fall back to the legacy sprite frames).
        /// </summary>
        public bool PlayParticle(string key, Vector3 worldPosition, float scaleMultiplier = 1f)
        {
            var prefab = GetParticlePrefab(key);
            if (prefab == null) return false;

            var go = Instantiate(prefab, worldPosition, Quaternion.identity);
            var s = ParticleWorldScale * Mathf.Max(0.01f, scaleMultiplier);
            go.transform.localScale = go.transform.localScale * s;

            // Lifetime = longest (duration + max start lifetime) across all child systems, + buffer.
            float life = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                var l = m.duration + m.startLifetime.constantMax;
                if (l > life) life = l;
            }
            Destroy(go, life > 0f ? life + 0.5f : 2.5f);
            return true;
        }

        public void Play(string vfxName, Vector3 worldPosition, float worldScale = DefaultWorldScale, float fps = DefaultFps)
        {
            // 1) Imported CFX prefab (cfx/{key}) if present. 2) Procedural code-built particles
            // (BattleParticleFx). 3) Legacy sprite-frame animation.
            if (PlayParticle(vfxName, worldPosition)) return;
            if (BattleParticleFx.Play(vfxName, worldPosition, ParticleWorldScale)) return;

            var frames = BattleVfxLibrary.GetFrames(vfxName);
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            var go = new GameObject($"vfx_{vfxName}");
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * worldScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = 1000;

            StartCoroutine(Animate(go, sr, frames, fps));
        }

        private static IEnumerator Animate(GameObject go, SpriteRenderer sr, Sprite[] frames, float fps)
        {
            var frameTime = fps > 0f ? 1f / fps : 0.07f;
            var cam = Camera.main;

            for (var i = 0; i < frames.Length; i++)
            {
                if (go == null)
                {
                    yield break;
                }

                sr.sprite = frames[i];
                if (cam != null)
                {
                    // billboard: face the camera
                    go.transform.rotation = cam.transform.rotation;
                }

                yield return new WaitForSeconds(frameTime);
            }

            if (go != null)
            {
                Destroy(go);
            }
        }
    }
}
