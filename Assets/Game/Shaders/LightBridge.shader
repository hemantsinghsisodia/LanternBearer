Shader "LanternKeeper/LightBridge"
{
    // The deck of an Island 5 light bridge: additive amber planks with a bright core line, glowing edge lines, a soft under-glow
    // and a few drifting sparks. Additive, unlit and GPU-instanced; no shadow pass and no DepthOnly pass on purpose (a translucent
    // glow must not write depth or normals, and a depth entry would make the MoonRim post pass draw an edge round it).
    // Mesh UV (LightBridge.BuildMesh): x runs 0..1 from the near end to the far end, y runs -0.4..1.4 across, the deck being 0..1
    // and the rest the under-glow.
    // Per instance (LightBridge, MaterialPropertyBlock): _Grow (0..1, planks appear from both ends and meet in the middle; mirrors
    // BridgeTimings.PlankVisible), _Kindle (overall brightness 0..1) and _PlankCount (planks along the deck).
    // Global: _LKReduceFlashing = 1 damps the shimmer.
    Properties
    {
        _BaseColor ("Amber", Color) = (1, 0.694, 0.361, 1)
        _CoreColor ("Core", Color) = (1, 0.827, 0.541, 1)
        _Strength ("Strength", Range(0, 3)) = 1.2
        _Grow ("Grow", Float) = 1
        _Kindle ("Kindle", Float) = 1
        _PlankCount ("Plank Count", Float) = 14
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CoreColor;
                float _Strength;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(BridgeProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _Grow)
                UNITY_DEFINE_INSTANCED_PROP(float, _Kindle)
                UNITY_DEFINE_INSTANCED_PROP(float, _PlankCount)
            UNITY_INSTANCING_BUFFER_END(BridgeProps)

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float grow = UNITY_ACCESS_INSTANCED_PROP(BridgeProps, _Grow);
                float kindle = UNITY_ACCESS_INSTANCED_PROP(BridgeProps, _Kindle);
                float planks = UNITY_ACCESS_INSTANCED_PROP(BridgeProps, _PlankCount);

                float u = input.uv.x;
                float across = abs(input.uv.y - 0.5) * 2.0;

                // Planks appear from both ends and meet in the middle; the front of the growth flares.
                float fromEnd = min(u, 1.0 - u) * 2.0;
                float g = grow * 1.06;
                float reveal = 1.0 - smoothstep(g - 0.06, g, fromEnd);
                float front = exp(-abs(fromEnd - g + 0.03) * 16.0) * step(grow, 0.995) * step(0.001, grow);
                float endFade = smoothstep(0.0, 0.025, u) * smoothstep(0.0, 0.025, 1.0 - u);

                float pu = u * planks;
                float f = frac(pu);
                float seam = smoothstep(0.0, 0.07, f) * smoothstep(1.0, 0.93, f);
                float tone = lerp(0.72, 1.0, Hash(float2(floor(pu), 3.0)));
                float deck = 1.0 - smoothstep(0.9, 1.0, across);
                float plank = deck * seam * tone * 0.5;

                float core = pow(saturate(1.0 - across * 5.0), 2.0);
                float edgeLine = exp(-pow((across - 0.93) * 20.0, 2.0)) * 0.7;
                float outer = across > 1.0 ? exp(-(across - 1.0) * 3.4) * 0.38 * saturate(1.0 - (across - 1.0) / 0.8) : 0.0;

                float pulse = 0.9 + 0.1 * sin(_Time.y * 2.1 + u * 13.0);
                pulse = lerp(pulse, 0.95, saturate(_LKReduceFlashing));

                // A few sparks rising off the deck.
                float2 cell = float2(floor(pu * 2.0), floor(input.uv.y * 7.0));
                float2 local = float2(frac(pu * 2.0), frac(input.uv.y * 7.0)) - 0.5;
                float blink = step(0.97, Hash(cell + floor(_Time.y * 1.7 + Hash(cell) * 5.0)));
                float spark = blink * saturate(1.0 - length(local) * 3.2) * deck * (1.0 - _LKReduceFlashing * 0.7);

                float body = (plank + core * 0.85 + edgeLine + outer + spark * 0.8) * pulse;
                float3 colour = lerp(_BaseColor.rgb, _CoreColor.rgb, saturate(core * 1.4 + spark + front * 0.6));
                float intensity = (body * reveal + front * (deck * 0.9 + outer)) * endFade * kindle * _Strength;
                return half4(colour * intensity, 0.0);
            }
            ENDHLSL
        }
    }
}
