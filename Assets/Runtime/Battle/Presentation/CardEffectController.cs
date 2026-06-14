using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Drives the animated card-surface effects (divine-shield shimmer, summon glow, dissolve,
    /// freeze, burn, holo/foil, outline) on a single card's quad. Attach to a card visual
    /// (<see cref="Card3DView"/> / <see cref="Card3DPlayed"/>) — or let one of those add it on demand
    /// via <see cref="GetOrAdd"/>.
    ///
    /// HOW IT HITS THE CARD WITHOUT BREAKING THE COMPOSITE
    /// ---------------------------------------------------
    /// The card view already clones its quad's material into a per-card instance and bakes the
    /// composite (art+frame) onto _BaseMap/_MainTex (with a 180deg flip in tiling/offset). This
    /// controller takes that SAME instance and, while an effect is active, swaps its shader to
    /// <c>Flippy/CardEffects</c> — carrying _BaseMap + _BaseMap_ST (the flip) + the alpha cutoff
    /// across — so the card keeps drawing exactly as before, plus the effect. When the effect ends
    /// the original shader/queue is restored. Nothing is baked, the composite is untouched, and the
    /// stat badges (separate world-space overlay UI) are never affected.
    ///
    /// PERF (old devices): one material instance per card (same as today — no extra instances), the
    /// effect shader is URP Unlit single-pass with no lighting/shadows/grabpass, and the animated
    /// params are pushed through a <see cref="MaterialPropertyBlock"/> each frame (no GC, no
    /// per-frame material mutation). Only ONE surface effect renders at a time.
    /// </summary>
    public sealed class CardEffectController : MonoBehaviour
    {
        public enum CardEffect
        {
            None,
            Shield,    // divine-shield shimmer (sustained while a Shield status is up)
            Glow,      // pulse highlight: on-play summon flash + generic status highlight
            Dissolve,  // edge-burn dissolve for death/destroy (one-shot, phase-driven)
            Freeze,    // icy overlay + slight desaturation + frost edges
            Burn,      // ember/heat shimmer at edges, warm tint
            Holo,      // subtle rainbow foil sweep (rarity flair)
            Outline    // selection/target ring (cheap)
        }

        private const string EffectShaderName = "Flippy/CardEffects";

        // Keyword per effect — must match the shader_feature set in CardEffects.shader.
        private static readonly Dictionary<CardEffect, string> Keywords = new()
        {
            { CardEffect.None,     "_FX_NONE" },
            { CardEffect.Shield,   "_FX_SHIELD" },
            { CardEffect.Glow,     "_FX_GLOW" },
            { CardEffect.Dissolve, "_FX_DISSOLVE" },
            { CardEffect.Freeze,   "_FX_FREEZE" },
            { CardEffect.Burn,     "_FX_BURN" },
            { CardEffect.Holo,     "_FX_HOLO" },
            { CardEffect.Outline,  "_FX_OUTLINE" },
        };

        // Default tints so callers can Play(effect) without picking a colour.
        private static readonly Dictionary<CardEffect, Color> DefaultColors = new()
        {
            { CardEffect.Shield,   new Color(1.00f, 0.88f, 0.45f, 1f) }, // gold
            { CardEffect.Glow,     new Color(0.65f, 0.85f, 1.00f, 1f) }, // soft blue-white
            { CardEffect.Dissolve, new Color(1.00f, 0.45f, 0.10f, 1f) }, // ember orange
            { CardEffect.Freeze,   new Color(0.55f, 0.80f, 1.00f, 1f) }, // ice blue
            { CardEffect.Burn,     new Color(1.00f, 0.40f, 0.10f, 1f) }, // fire orange
            { CardEffect.Holo,     new Color(1.00f, 1.00f, 1.00f, 1f) }, // rainbow handled in-shader
            { CardEffect.Outline,  new Color(0.30f, 0.95f, 1.00f, 1f) }, // cyan selection ring
        };

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int FxColorId = Shader.PropertyToID("_FxColor");
        private static readonly int FxIntensityId = Shader.PropertyToID("_FxIntensity");
        private static readonly int FxPhaseId = Shader.PropertyToID("_FxPhase");
        private static readonly int FxSpeedId = Shader.PropertyToID("_FxSpeed");

        [SerializeField] private Renderer targetRenderer;

        private Shader _effectShader;
        private Shader _originalShader;
        private int _originalRenderQueue = -1;
        private MaterialPropertyBlock _mpb;
        private Material _material;

        // The sustained effect (e.g. Shield) is reasserted after any one-shot finishes.
        private CardEffect _sustained = CardEffect.None;
        private Color _sustainedColor = Color.white;
        private float _sustainedIntensity = 1f;

        // The effect currently bound to the material's keyword/shader.
        private CardEffect _active = CardEffect.None;

        // One-shot animation state.
        private CardEffect _oneShot = CardEffect.None;
        private Color _oneShotColor = Color.white;
        private float _oneShotDuration;
        private float _oneShotElapsed;
        private bool _oneShotPhaseDriven; // dissolve drives _FxPhase 0..1 over the duration

        /// <summary>Finds (or adds) the controller on a card's GameObject and binds its quad renderer.</summary>
        public static CardEffectController GetOrAdd(GameObject cardRoot, Renderer cardRenderer = null)
        {
            if (cardRoot == null)
            {
                return null;
            }
            var controller = cardRoot.GetComponent<CardEffectController>() ?? cardRoot.AddComponent<CardEffectController>();
            if (cardRenderer != null)
            {
                controller.Bind(cardRenderer);
            }
            return controller;
        }

        /// <summary>Binds the quad renderer whose material the effects are applied to.</summary>
        public void Bind(Renderer cardRenderer)
        {
            if (cardRenderer != null)
            {
                targetRenderer = cardRenderer;
                _material = null; // re-resolve on next use
            }
        }

        private void Awake()
        {
            _mpb ??= new MaterialPropertyBlock();
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
            {
                return true;
            }
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>(true);
            }
            if (targetRenderer == null)
            {
                return false;
            }
            // .material is the per-card instance the card view already created; reuse it (no churn).
            _material = targetRenderer.material;
            return _material != null;
        }

        private bool EnsureEffectShader()
        {
            if (_effectShader == null)
            {
                _effectShader = Shader.Find(EffectShaderName);
            }
            return _effectShader != null;
        }

        // -------------------- public API --------------------

        /// <summary>
        /// Plays a one-shot effect for <paramref name="duration"/> seconds (e.g. the on-play summon
        /// glow, or a dissolve). When it finishes, the sustained effect (if any) is restored.
        /// Pass a null colour to use the effect's default tint.
        /// </summary>
        public void Play(CardEffect effect, Color? color = null, float duration = 0.6f, float intensity = 1f)
        {
            if (effect == CardEffect.None)
            {
                return;
            }

            _oneShot = effect;
            _oneShotColor = color ?? DefaultTint(effect);
            _oneShotColor.a = Mathf.Clamp01(intensity);
            _oneShotDuration = Mathf.Max(0.01f, duration);
            _oneShotElapsed = 0f;
            _oneShotPhaseDriven = effect == CardEffect.Dissolve;

            ApplyEffect(effect);
            PushParams(_oneShotColor, intensity, _oneShotPhaseDriven ? 0f : 1f);
            enabled = true;
        }

        /// <summary>
        /// Turns a sustained effect on or off (e.g. the divine-shield shimmer while a Shield status
        /// is active, or a persistent Freeze/Burn/Holo). A running one-shot still plays on top and
        /// the sustained effect resumes when it ends. Pass a null colour to use the default tint.
        /// </summary>
        public void SetSustained(CardEffect effect, bool on, Color? color = null, float intensity = 1f)
        {
            if (on && effect != CardEffect.None)
            {
                // No-op when already running this sustained effect (called every snapshot refresh).
                if (_sustained == effect)
                {
                    return;
                }
                _sustained = effect;
                _sustainedColor = color ?? DefaultTint(effect);
                _sustainedIntensity = Mathf.Clamp01(intensity);
                if (_oneShot == CardEffect.None)
                {
                    ApplyEffect(_sustained);
                    PushParams(_sustainedColor, _sustainedIntensity, 1f);
                    enabled = true; // let Update settle then disable (sustained = no per-frame work)
                }
            }
            else
            {
                // Clear only if THIS effect (or any, when None passed) is the sustained one.
                if (effect == CardEffect.None || _sustained == effect)
                {
                    _sustained = CardEffect.None;
                    if (_oneShot == CardEffect.None)
                    {
                        ClearEffect();
                    }
                }
            }
        }

        /// <summary>Stops everything and restores the card's original shader/look.</summary>
        public void ClearAll()
        {
            _oneShot = CardEffect.None;
            _sustained = CardEffect.None;
            ClearEffect();
        }

        // Convenience wrappers the battle code can hook to events:
        public void PlaySummonGlow(Color? color = null, float duration = 0.6f) => Play(CardEffect.Glow, color, duration);
        public void PlayDissolve(Color? color = null, float duration = 0.7f) => Play(CardEffect.Dissolve, color, duration);
        public void SetShield(bool on) => SetSustained(CardEffect.Shield, on);
        public void SetFreeze(bool on) => SetSustained(CardEffect.Freeze, on);
        public void SetBurn(bool on) => SetSustained(CardEffect.Burn, on);
        public void SetHolo(bool on) => SetSustained(CardEffect.Holo, on);
        public void SetOutline(bool on, Color? color = null) => SetSustained(CardEffect.Outline, on, color);

        // -------------------- internals --------------------

        private static Color DefaultTint(CardEffect effect)
            => DefaultColors.TryGetValue(effect, out var c) ? c : Color.white;

        private void Update()
        {
            if (_oneShot != CardEffect.None)
            {
                _oneShotElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(_oneShotElapsed / _oneShotDuration);

                if (_oneShotPhaseDriven)
                {
                    // Dissolve: drive _FxPhase 0..1; keep colour/intensity steady.
                    PushParams(_oneShotColor, _oneShotColor.a, t);
                }
                else
                {
                    // Glow/flash: ease intensity up then down (0 -> 1 -> 0) over the duration.
                    var env = Mathf.Sin(t * Mathf.PI);
                    PushParams(_oneShotColor, _oneShotColor.a * env, 1f);
                }

                if (t >= 1f)
                {
                    _oneShot = CardEffect.None;
                    // Resume the sustained effect, or clear back to the plain card.
                    if (_sustained != CardEffect.None)
                    {
                        ApplyEffect(_sustained);
                        PushParams(_sustainedColor, _sustainedIntensity, 1f);
                    }
                    else
                    {
                        ClearEffect();
                    }
                }
            }
            else
            {
                // No one-shot running. Sustained, time-based effects (shield/freeze/burn/holo) animate
                // purely from _Time in the shader and had their params pushed once when set, so there's
                // nothing to do per frame — stop ticking to save CPU until the next Play/SetSustained.
                enabled = false;
            }
        }

        // Swaps the material to the effect shader (preserving the card texture + flip + cutoff) and
        // sets the effect keyword. Idempotent for the same effect.
        private void ApplyEffect(CardEffect effect)
        {
            if (effect == CardEffect.None)
            {
                ClearEffect();
                return;
            }
            if (!EnsureMaterial() || !EnsureEffectShader())
            {
                return;
            }

            if (_originalShader == null || _material.shader != _effectShader)
            {
                // Remember the original look so ClearEffect can fully restore it.
                if (_material.shader != _effectShader)
                {
                    _originalShader = _material.shader;
                    _originalRenderQueue = _material.renderQueue;
                }

                // Carry the card texture + its 180deg flip (tiling/offset) + cutoff across.
                var tex = _material.HasProperty(BaseMapId) ? _material.GetTexture(BaseMapId) : null;
                if (tex == null && _material.HasProperty(MainTexId))
                {
                    tex = _material.GetTexture(MainTexId);
                }
                var st = _material.HasProperty(BaseMapStId)
                    ? _material.GetVector(BaseMapStId)
                    : new Vector4(-1f, -1f, 1f, 1f); // matches the card path's flip default
                var cutoff = _material.HasProperty(CutoffId) ? _material.GetFloat(CutoffId) : 0.5f;

                _material.shader = _effectShader;
                if (tex != null)
                {
                    _material.SetTexture(BaseMapId, tex);
                }
                _material.SetVector(BaseMapStId, st);
                _material.SetFloat(CutoffId, cutoff);
            }

            // Single active keyword (clear the rest).
            foreach (var kw in Keywords.Values)
            {
                _material.DisableKeyword(kw);
            }
            if (Keywords.TryGetValue(effect, out var keyword))
            {
                _material.EnableKeyword(keyword);
            }

            _active = effect;
        }

        // Restores the card's original shader/queue so it looks exactly as before the effect.
        private void ClearEffect()
        {
            _active = CardEffect.None;
            if (_material == null)
            {
                return;
            }

            // Clear the MPB overrides so leftover params don't bleed onto the restored material.
            if (_mpb != null && targetRenderer != null)
            {
                _mpb.Clear();
                targetRenderer.SetPropertyBlock(_mpb);
            }

            if (_originalShader != null && _material.shader != _originalShader)
            {
                _material.shader = _originalShader;
                if (_originalRenderQueue >= 0)
                {
                    _material.renderQueue = _originalRenderQueue;
                }
            }
            _originalShader = null;
        }

        // Pushes per-card animated params via the MaterialPropertyBlock (no GC, no material mutation).
        private void PushParams(Color color, float intensity, float phase)
        {
            if (targetRenderer == null || _mpb == null)
            {
                return;
            }
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(FxColorId, color);
            _mpb.SetFloat(FxIntensityId, Mathf.Clamp01(intensity));
            _mpb.SetFloat(FxPhaseId, Mathf.Clamp01(phase));
            _mpb.SetFloat(FxSpeedId, 1f);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        private void OnDisable()
        {
            // Don't tear the effect down on disable; the card may just be pooled/inactive.
        }
    }
}
