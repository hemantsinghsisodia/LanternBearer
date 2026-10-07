Shader "LanternKeeper/MothWing"
{
    // Moth wings: a flat, double sided, alpha clipped card pair flapped about the body axis in the vertex stage.
    // The flap is a port of MothFlap.cs (Assets/Game/Scripts/Logic/MothFlap.cs): keep the two in step.
    // Per instance: _Phase (radians) and _FlapHz, set by MothVisual through a MaterialPropertyBlock.
    // Low preset: _LKMothLow = 1 is a plain sine (no glide), picked by a uniform branch, never a new keyword.
    // Vertex colour R is the span fraction (0 at the root, 1 at the tip); the tips bend a little more than the roots.
    // Lit with wrap lighting and a thin translucency so the lantern warms the wings. No stencil write: bit 8 belongs to the keeper.
    Properties
    {
        _BaseMap ("Wing Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        _RimColor ("Rim", Color) = (0.58, 0.72, 0.95, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.6
        _MoonLift ("Moon And Ambient Lift", Range(0.5, 4)) = 1
        _LightCap ("Near Light Cap", Range(0.1, 1000)) = 0.6
        _Phase ("Phase", Float) = 0
        _FlapHz ("Flap Hz", Float) = 17
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float _MoonLift;
            float _LightCap;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(MothProps)
            UNITY_DEFINE_INSTANCED_PROP(float, _Phase)
            UNITY_DEFINE_INSTANCED_PROP(float, _FlapHz)
        UNITY_INSTANCING_BUFFER_END(MothProps)

        // 1 on the Low preset. Set by MothVisual.
        float _LKMothLow;

        static const float MothMaxAngle = 55.0;

        float MothHash01(float x)
        {
            float s = sin(x * 12.9898) * 43758.5453;
            return s - floor(s);
        }

        float MothSmooth(float x)
        {
            x = saturate(x);
            return x * x * (3.0 - 2.0 * x);
        }

        // MothFlap.GlideFactor
        float MothGlide(float time, float phase)
        {
            float period = 3.0 + 3.0 * MothHash01(phase);
            float offset = period * MothHash01(phase + 17.31);
            float t = time + offset;
            float c = t - floor(t / period) * period;
            if (c >= 1.0)
            {
                return 0.0;
            }

            float rise = MothSmooth(c / 0.15);
            float fall = 1.0 - MothSmooth((c - 0.85) / 0.15);
            return saturate(min(rise, fall));
        }

        // MothFlap.Angle, in radians. Low: a plain sine without the glide.
        float MothAngle(float time, float phase, float hz)
        {
            float flap = sin(6.28318530718 * hz * time + phase);
            float glide = (_LKMothLow > 0.5) ? 0.0 : MothGlide(time, phase);
            float amp = 1.0 - 0.85 * saturate(glide);
            return radians(clamp(MothMaxAngle * amp * flap, -MothMaxAngle, MothMaxAngle));
        }

        // Rotates a wing vertex about the body axis (object Z). Each side lifts the same way.
        // Object space is the imported FBX space (X across the wings, Y up, Z along the body).
        void MothFlapVertex(float3 positionOS, float3 normalOS, float span, float angle, out float3 outPos, out float3 outNormal)
        {
            float side = positionOS.x >= 0.0 ? 1.0 : -1.0;
            float a = angle * side * lerp(0.35, 1.0, saturate(span));
            float sn, cs;
            sincos(a, sn, cs);
            outPos = float3(positionOS.x * cs - positionOS.y * sn, positionOS.x * sn + positionOS.y * cs, positionOS.z);
            outNormal = float3(normalOS.x * cs - normalOS.y * sn, normalOS.x * sn + normalOS.y * cs, normalOS.z);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float angle = MothAngle(_Time.y, UNITY_ACCESS_INSTANCED_PROP(MothProps, _Phase), UNITY_ACCESS_INSTANCED_PROP(MothProps, _FlapHz));
                float3 pos;
                float3 nrm;
                MothFlapVertex(input.positionOS.xyz, input.normalOS, input.color.r, angle, pos, nrm);
                output.positionWS = TransformObjectToWorld(pos);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(nrm);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, bool isFront : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float alpha = tex.a * _BaseColor.a;
                clip(alpha - _Cutoff);
                float3 albedo = tex.rgb * _BaseColor.rgb;
                float3 normalWS = normalize(input.normalWS);
                normalWS = isFront ? normalWS : -normalWS;
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                // Wrap lighting, and a thin wing passes light through: the back lit side is only a little darker.
                float wrap = saturate(abs(dot(normalWS, mainLight.direction)) * 0.55 + 0.45);
                float3 ambient = max(SampleSH(normalWS), float3(0.07, 0.08, 0.10));
                float3 color = albedo * _MoonLift * (ambient + mainLight.color * (wrap * mainLight.shadowAttenuation));

                // The clustered light loop macros read inputData.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    float addWrap = saturate(abs(dot(normalWS, addLight.direction)) * 0.7 + 0.3);
                    color += albedo * addLight.color * (min(addLight.distanceAttenuation, _LightCap) * addLight.shadowAttenuation * addWrap);
                LIGHT_LOOP_END
                #endif

                // A thin moon rim: the wings are flat, so this reads at the edges and when seen near edge-on.
                float fresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDir))), _RimPower);
                color += _RimColor.rgb * fresnel * _RimStrength;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
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
                float angle = MothAngle(_Time.y, UNITY_ACCESS_INSTANCED_PROP(MothProps, _Phase), UNITY_ACCESS_INSTANCED_PROP(MothProps, _FlapHz));
                float3 pos;
                float3 nrm;
                MothFlapVertex(input.positionOS.xyz, input.normalOS, input.color.r, angle, pos, nrm);
                float3 positionWS = TransformObjectToWorld(pos);
                float3 normalWS = TransformObjectToWorldNormal(nrm);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
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
                float angle = MothAngle(_Time.y, UNITY_ACCESS_INSTANCED_PROP(MothProps, _Phase), UNITY_ACCESS_INSTANCED_PROP(MothProps, _FlapHz));
                float3 pos;
                float3 nrm;
                MothFlapVertex(input.positionOS.xyz, input.normalOS, input.color.r, angle, pos, nrm);
                output.positionCS = TransformObjectToHClip(pos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
