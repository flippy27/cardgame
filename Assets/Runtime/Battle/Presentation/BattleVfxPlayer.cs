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

        public void Play(string vfxName, Vector3 worldPosition, float worldScale = DefaultWorldScale, float fps = DefaultFps)
        {
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
