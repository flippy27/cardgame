using System;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    [Serializable]
    public sealed class CameraShakePreset
    {
        public string label = "Shake";
        public float duration = 0.12f;
        public float amplitude = 0.05f;
        public float frequency = 24f;
        public AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }

    public class BattleCameraShake : MonoBehaviour
    {
        [SerializeField] private CameraShakePreset[] shakePresets = new CameraShakePreset[5];
        [SerializeField] private bool useUnscaledTime = false;

        private Coroutine _shakeCoroutine;
        private Vector3 _restLocalPosition;

        private void Awake()
        {
            _restLocalPosition = transform.localPosition;
            EnsurePresets();
        }

        private void OnValidate()
        {
            EnsurePresets();
        }

        public void PlayLevel(int level)
        {
            if (level <= 0)
            {
                return;
            }

            EnsurePresets();
            var index = Mathf.Clamp(level - 1, 0, shakePresets.Length - 1);
            var preset = shakePresets[index];
            if (preset == null || preset.duration <= 0f || preset.amplitude <= 0f)
            {
                return;
            }

            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
            }

            _shakeCoroutine = StartCoroutine(ShakeRoutine(preset));
        }

        private System.Collections.IEnumerator ShakeRoutine(CameraShakePreset preset)
        {
            _restLocalPosition = transform.localPosition;
            var elapsed = 0f;

            while (elapsed < preset.duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / preset.duration);
                var envelope = preset.envelope != null ? preset.envelope.Evaluate(t) : 1f;

                var time = (useUnscaledTime ? Time.unscaledTime : Time.time) * preset.frequency;
                var offset = new Vector3(
                    (Mathf.PerlinNoise(time, 0.17f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(0.31f, time) - 0.5f) * 2f,
                    0f) * (preset.amplitude * envelope);

                transform.localPosition = _restLocalPosition + offset;
                yield return null;
            }

            transform.localPosition = _restLocalPosition;
            _shakeCoroutine = null;
        }

        private void EnsurePresets()
        {
            if (shakePresets == null || shakePresets.Length != 5)
            {
                Array.Resize(ref shakePresets, 5);
            }

            for (var index = 0; index < shakePresets.Length; index++)
            {
                shakePresets[index] ??= CreatePreset(index);
            }
        }

        private static CameraShakePreset CreatePreset(int index)
        {
            return index switch
            {
                0 => new CameraShakePreset { label = "Level 1", duration = 0.07f, amplitude = 0.025f, frequency = 18f },
                1 => new CameraShakePreset { label = "Level 2", duration = 0.09f, amplitude = 0.04f, frequency = 22f },
                2 => new CameraShakePreset { label = "Level 3", duration = 0.12f, amplitude = 0.06f, frequency = 26f },
                3 => new CameraShakePreset { label = "Level 4", duration = 0.16f, amplitude = 0.085f, frequency = 30f },
                _ => new CameraShakePreset { label = "Level 5", duration = 0.2f, amplitude = 0.11f, frequency = 34f }
            };
        }
    }
}
