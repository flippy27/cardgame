// Flippy/CardEffects — mobile-friendly URP Unlit shader that draws the card's composite
// (art+frame) texture and layers ONE animated card-surface effect on top.
//
// Designed for CardEffectController.cs: the controller swaps a card's material to this shader
// while an effect is active (keeping _BaseMap + its 180deg flip tiling/offset), selects the
// effect with a single shader_feature keyword, and animates the per-card params
// (_FxColor / _FxIntensity / _FxPhase) via a MaterialPropertyBlock (no per-card material churn,
// no GC). When the effect ends the controller restores the card's original URP/Lit shader, so
// the normal card look is untouched (this is purely additive).
//
// PERF NOTES (old devices):
//  - URP Unlit, single pass, no lighting, no shadows, no GrabPass, no loops.
//  - One texture sample for the card + at most one tiny procedural sample per effect.
//  - Only ONE effect keyword variant compiles at a time (shader_feature), ~8 cheap variants.
//  - Alpha-clip keeps overdraw to the card silhouette; transparent blend only where needed.
Shader "Flippy/CardEffects"
{
    Properties
    {
        [MainTexture] _BaseMap ("Card Composite", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        // --- shared effect params (driven per-card via MaterialPropertyBlock) ---
        _FxColor ("Effect Color", Color) = (1, 0.85, 0.3, 1)
        _FxIntensity ("Effect Intensity", Range(0,1)) = 1
        _FxPhase ("Effect Phase 0..1 (one-shots)", Range(0,1)) = 0
        _FxSpeed ("Effect Anim Speed", Float) = 1
        _FxNoiseTex ("Noise (optional, dissolve/burn)", 2D) = "gray" {}

        // Selects which effect this material runs. The controller sets the matching keyword;
        // this enum is only for hand-tweaking in the inspector.
        [KeywordEnum(None, Shield, Glow, Dissolve, Freeze, Burn, Holo, Outline)]
        _FX ("Effect", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "CardEffect"
            Tags { "LightMode"="UniversalForward" }

            // Transparent-safe: blend so glow/shimmer/foil add over the card and dissolve fades
            // edges, while the card body itself stays opaque via alpha-clip in the shader.
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite On
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            // ONE effect active at a time -> single cheap keyword dimension.
            #pragma shader_feature_local _FX_NONE _FX_SHIELD _FX_GLOW _FX_DISSOLVE _FX_FREEZE _FX_BURN _FX_HOLO _FX_OUTLINE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FxNoiseTex);   SAMPLER(sampler_FxNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Cutoff;
                float4 _FxColor;
                float  _FxIntensity;
                float  _FxPhase;
                float  _FxSpeed;
                float4 _FxNoiseTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;   // card UV (with _BaseMap_ST flip applied)
                float2 uvRaw : TEXCOORD1; // unflipped 0..1 UV for screen-stable effects
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // cheap value noise (no texture needed) for dissolve / burn / frost edges
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uvRaw = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 card = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Card silhouette: clip transparent (outside-frame) pixels so the quad keeps its shape.
                clip(card.a - _Cutoff);

                half3 rgb = card.rgb;
                half  alpha = card.a;

                float t = _Time.y * _FxSpeed;
                float intensity = _FxIntensity;
                float3 fx = _FxColor.rgb;

                // Distance from card centre / to nearest edge in the UNFLIPPED UV (so rims and
                // sweeps are orientation-stable regardless of the _BaseMap flip).
                float2 uvC = IN.uvRaw;
                float2 d2 = min(uvC, 1.0 - uvC);
                float edge = min(d2.x, d2.y);          // 0 at border .. 0.5 at centre
                float rim = saturate(1.0 - edge * 8.0); // bright band along the card border

            #if defined(_FX_SHIELD)
                // Divine-shield: animated golden rim + a diagonal holo sweep across the card.
                float sweep = sin((uvC.x + uvC.y) * 6.2831 - t * 3.0) * 0.5 + 0.5;
                sweep = pow(sweep, 6.0);                       // tight moving band
                float rimPulse = rim * (0.6 + 0.4 * (sin(t * 4.0) * 0.5 + 0.5));
                float add = saturate(rimPulse + sweep * 0.5) * intensity;
                rgb += fx * add;
                alpha = saturate(alpha + add * 0.35);
            #elif defined(_FX_GLOW)
                // Glow / pulse highlight: rim glow that breathes; good for on-play flash + status.
                float pulse = (sin(t * 3.0) * 0.5 + 0.5);
                float add = rim * (0.35 + 0.65 * pulse) * intensity;
                rgb += fx * add;
                alpha = saturate(alpha + add * 0.3);
            #elif defined(_FX_DISSOLVE)
                // Edge-burn dissolve for death/destroy. _FxPhase 0..1 = amount dissolved.
                float n = ValueNoise(uvC * 9.0);
                float threshold = _FxPhase;
                // burning ember edge just above the cut line
                float burn = smoothstep(threshold, threshold + 0.12, n) - smoothstep(threshold + 0.12, threshold + 0.18, n);
                rgb += fx * saturate(burn) * (1.5 * max(intensity, 0.001));
                // clip away already-dissolved pixels
                alpha *= step(threshold, n);
                clip(alpha - 0.01);
            #elif defined(_FX_FREEZE)
                // Icy overlay: desaturate slightly, tint cold, frost crystals at the edges.
                float luma = dot(rgb, float3(0.299, 0.587, 0.114));
                float3 cold = lerp(rgb, half3(luma, luma, luma), 0.4 * intensity);
                cold = lerp(cold, fx, 0.25 * intensity);
                float frost = ValueNoise(uvC * 14.0) * rim;
                cold += fx * frost * 0.6 * intensity;
                // subtle shimmer on the ice
                cold += fx * (sin(t * 2.0 + uvC.y * 20.0) * 0.5 + 0.5) * 0.05 * intensity;
                rgb = cold;
            #elif defined(_FX_BURN)
                // Warm ember/heat shimmer crawling up from the edges.
                float heat = ValueNoise(float2(uvC.x * 10.0, uvC.y * 10.0 - t * 1.5));
                float embers = saturate(heat * rim * 2.0);
                rgb = lerp(rgb, rgb * half3(1.2, 0.9, 0.7), 0.3 * intensity); // warm tint
                rgb += fx * embers * intensity;
                alpha = saturate(alpha + embers * 0.25 * intensity);
            #elif defined(_FX_HOLO)
                // Subtle rainbow foil sweep for rarity flair (legendary). Cheap palette via sin().
                float band = (uvC.x + uvC.y) * 3.0 - t;
                float3 rainbow = 0.5 + 0.5 * cos(6.2831 * (band + float3(0.0, 0.33, 0.66)));
                float strength = (sin((uvC.x - uvC.y) * 6.2831 - t * 2.0) * 0.5 + 0.5);
                rgb += rainbow * strength * 0.18 * intensity;
            #elif defined(_FX_OUTLINE)
                // Cheap selection/target ring: solid tinted band along the card border.
                float ring = saturate(1.0 - edge * 14.0);
                rgb = lerp(rgb, fx, ring * intensity);
                alpha = saturate(alpha + ring * 0.4 * intensity);
            #endif

                return half4(rgb, alpha * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
