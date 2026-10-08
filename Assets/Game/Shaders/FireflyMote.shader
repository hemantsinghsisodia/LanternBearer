Shader "LanternKeeper/FireflyMote"
{
    // One firefly mote: a camera-facing additive quad with a white core fading to _Color at the halo edge.
    // Per instance (FireflySwarm, MaterialPropertyBlock): _Phase (blink cycle offset, MoteOffset), _Period (s), _Seed (0..1),
    // _Index (mote number), _Stream (0..1, collect stream: no drift, held lit), _Fade (0..1, overall alpha).
    // Globals: _LKFireflyLow = 1 on Low (motes 4 and up are hidden, 4-mote blink), _LKReduceFlashing = 1 (slower edges, 0.3 floor).
    // The blink mirrors FireflyCurve.Blink; keep them in step.
    Properties
    {
        _Color ("Halo Colour", Color) = (0.722, 1, 0.69, 1)
        _Gain ("Gain", Float) = 1.0
        _Phase ("Phase", Float) = 0
        _Period ("Period", Float) = 3
        _Seed ("Seed", Float) = 0
        _Index ("Index", Float) = 0
        _Stream ("Stream", Float) = 0
        _Fade ("Fade", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "FireflyMote"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Gain;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(FireflyProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _Phase)
                UNITY_DEFINE_INSTANCED_PROP(float, _Period)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Index)
                UNITY_DEFINE_INSTANCED_PROP(float, _Stream)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fade)
            UNITY_INSTANCING_BUFFER_END(FireflyProps)

            float _LKFireflyLow;
            float _LKReduceFlashing;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash11(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            // Same envelope as FireflyCurve.Blink: rise, hold, fall, dark.
            float Blink(float time, float period, float offset, float count)
            {
                float u = time / period + offset;
                u -= floor(u);
                float edge = _LKReduceFlashing > 0.5 ? 0.2 : 0.1;
                float hold = max(0.0, 2.6 / max(1.0, count) - edge);
                float r = 0.0;
                if (u < edge)
                {
                    r = u / edge;
                }
                else if (u < edge + hold)
                {
                    r = 1.0;
                }
                else if (u < edge + hold + edge)
                {
                    r = 1.0 - (u - edge - hold) / edge;
                }

                return _LKReduceFlashing > 0.5 ? max(r, 0.3) : r;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float phase = UNITY_ACCESS_INSTANCED_PROP(FireflyProps, _Phase);
                float period = UNITY_ACCESS_INSTANCED_PROP(FireflyProps, _Period);
                float seed = UNITY_ACCESS_INSTANCED_PROP(FireflyProps, _Seed);
                float index = UNITY_ACCESS_INSTANCED_PROP(FireflyProps, _Index);
                float stream = UNITY_ACCESS_INSTANCED_PROP(FireflyProps, _Stream);
                float fade = UNITY_ACCESS_INSTANCED_PROP(FireflyProps, _Fade);

                bool low = _LKFireflyLow > 0.5;
                float count = low ? 4.0 : 6.0;

                // Lissajous drift (mirrored on the CPU by FireflyCurve.Drift: keep them in step), 0.45 m, 0.2 to 0.5 Hz per axis, gone while streaming.
                float k = seed * 91.7 + index * 13.3;
                float3 freq = 0.2 + 0.3 * float3(Hash11(k + 1.0), Hash11(k + 2.0), Hash11(k + 3.0));
                float3 ph = 6.2831853 * float3(Hash11(k + 4.0), Hash11(k + 5.0), Hash11(k + 6.0));
                float3 drift = sin(_Time.y * 6.2831853 * freq + ph) * 0.45 * float3(1.0, 0.6, 1.0) * (1.0 - stream);

                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0)) + drift;
                float size = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                // Minimum on-screen size: never smaller than about 10 px across, so a far mote stays a visible green point.
                float depth = max(TransformWorldToHClip(centre).w, 0.01);
                float minSize = 10.0 * 2.0 * depth / (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y);
                minSize *= saturate(size / 0.075); // a mote scaled to nothing (collect stream) still shrinks away
                size = max(size, minSize);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 world = centre + (right * input.positionOS.x + up * input.positionOS.y) * size;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;

                float blink = stream > 0.0 ? 1.0 : Blink(_Time.y, period, phase, count);
                float hidden = (low && index > 3.5) ? 0.0 : 1.0;
                output.alpha = blink * fade * hidden;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // rr: 0 at the centre, 0.5 at the quad edge (the quad is 0.15 m, so the core is about 3 cm).
                float rr = length(input.uv - 0.5);
                float core = exp(-(rr * rr) / (0.08 * 0.08));
                float halo = exp(-(rr * rr) / (0.17 * 0.17)) * (1.0 - smoothstep(0.28, 0.5, rr));
                float3 coreColour = lerp(_Color.rgb, float3(1.0, 1.0, 1.0), 0.4);
                // Core peak about 1.5 (plus the halo underneath), halo peak 0.55: bloom adds a gentle green glow.
                float3 colour = coreColour * (core * 1.5) + _Color.rgb * (halo * 0.55);
                return half4(colour * input.alpha * _Gain, 1.0);
            }
            ENDHLSL
        }
    }
}
