Shader "LanternKeeper/GrassBend"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        _TipHeight ("Tip Height", Float) = 0.30
        [HideInInspector] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _LK_GRASS_NO_BEND
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Cutoff;
                float _TipHeight;
                float _Cull;
            CBUFFER_END

            float4 _LKPlayerPos;
            float _LKPlayerRadius;
            // Set per island by LookApplier. The texture only supplies the blade shape.
            float4 _LKGrassRoot;
            float4 _LKGrassTip;
            float4 _LKMoonRimColor;
            float _LKMoonRimStrength;
            float4 _LKMoonDir;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 BendGrass(float3 positionOS, float3 positionWS)
            {
#if defined(_LK_GRASS_NO_BEND)
                return positionWS;
#else
                float mask = saturate(positionOS.y / max(_TipHeight, 0.05));
                float radius = _LKPlayerRadius;
                if (radius > 0.05)
                {
                    float3 away = positionWS - _LKPlayerPos.xyz;
                    away.y = 0.0;
                    float dist = length(away);
                    if (dist > 0.0001)
                    {
                        float influence = saturate(1.0 - dist / radius);
                        influence *= influence;
                        positionWS.xz += (away.xz / dist) * influence * 0.5 * mask;
                        positionWS.y -= influence * 0.16 * mask;
                    }
                }

                float sway = sin(_Time.y * 1.2 + positionWS.x * 0.7 + positionWS.z * 0.45);
                positionWS.x += sway * 0.03 * mask;
                positionWS.z += cos(_Time.y * 0.95 + positionWS.z * 0.6) * 0.02 * mask;
                return positionWS;
#endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = BendGrass(input.positionOS.xyz, positionWS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, float facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedoSample.a - _Cutoff);
                // LookApplier sets alpha 1 on both. Unset (main menu, or after leaving an island) falls back to the island 1 tones.
                float3 grassRoot = _LKGrassTip.a > 0.5 ? _LKGrassRoot.rgb : float3(0.060, 0.132, 0.107);
                float3 grassTip = _LKGrassTip.a > 0.5 ? _LKGrassTip.rgb : float3(0.262, 0.396, 0.405);
                float3 albedo = lerp(grassRoot, grassTip, saturate(input.uv.y));
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 normalWS = normalize(mul(normalTS, float3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
                if (facing < 0.0)
                {
                    normalWS = -normalWS;
                }

                // Faint moon-rim tint on moon-facing tips. It goes through the lighting below, so it is never emissive.
                float moonFacing = saturate(dot(normalWS, normalize(_LKMoonDir.xyz)));
                albedo += 0.15 * _LKMoonRimColor.rgb * _LKMoonRimStrength * moonFacing * saturate(input.uv.y);

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 color = albedo * (SampleSH(normalWS) + mainLight.color * ndotl);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    float addDot = saturate(dot(normalWS, addLight.direction));
                    color += albedo * addLight.color * (addLight.distanceAttenuation * addDot);
                LIGHT_LOOP_END
                #endif

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
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _LK_GRASS_NO_BEND
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Cutoff;
                float _TipHeight;
                float _Cull;
            CBUFFER_END

            float4 _LKPlayerPos;
            float _LKPlayerRadius;
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 BendGrass(float3 positionOS, float3 positionWS)
            {
#if defined(_LK_GRASS_NO_BEND)
                return positionWS;
#else
                float mask = saturate(positionOS.y / max(_TipHeight, 0.05));
                float radius = _LKPlayerRadius;
                if (radius > 0.05)
                {
                    float3 away = positionWS - _LKPlayerPos.xyz;
                    away.y = 0.0;
                    float dist = length(away);
                    if (dist > 0.0001)
                    {
                        float influence = saturate(1.0 - dist / radius);
                        influence *= influence;
                        positionWS.xz += (away.xz / dist) * influence * 0.5 * mask;
                        positionWS.y -= influence * 0.16 * mask;
                    }
                }

                float sway = sin(_Time.y * 1.2 + positionWS.x * 0.7 + positionWS.z * 0.45);
                positionWS.x += sway * 0.03 * mask;
                positionWS.z += cos(_Time.y * 0.95 + positionWS.z * 0.6) * 0.02 * mask;
                return positionWS;
#endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = BendGrass(input.positionOS.xyz, TransformObjectToWorld(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
