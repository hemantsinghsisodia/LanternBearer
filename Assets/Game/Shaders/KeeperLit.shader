Shader "LanternKeeper/KeeperLit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Desaturate ("Desaturate", Range(0, 1)) = 0
        _RimColor ("Rim", Color) = (0.58, 0.72, 0.95, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.4
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.7
        _SwayStrength ("Sway Strength (m)", Range(0, 0.3)) = 0
        _VertexAO ("Vertex AO", Range(0, 1)) = 0
        _AOFloor ("AO Floor", Range(0, 0.5)) = 0.15
        _AOContrast ("AO Contrast", Range(1, 4)) = 1
        _LightCap ("Near Light Cap", Range(0.1, 1000)) = 1000
        _EdgeLighten ("Edge Lighten", Range(0, 0.5)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            // Marks the keeper (and its lantern iron) for the screen-space moon rim, which skips stencil bit 32 so the keeper keeps only its own rim.
            Stencil
            {
                Ref 32
                WriteMask 32
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_local _ _LK_KEEPER_NO_RIM
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
                float _Smoothness;
                float _Metallic;
                float _Desaturate;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float _SwayStrength;
                float _VertexAO;
                float _AOFloor;
                float _AOContrast;
                float _LightCap;
                float _EdgeLighten;
            CBUFFER_END
            // x = speed01, y = wind01, zw = world wind direction xz. Written by KeeperAnimator.
            float4 _LKKeeperSway;
            // World-space backward of the keeper (horizontal, normalised). Zero when unset: no backward push.
            float4 _LKKeeperBack;

            float3 KeeperSway(float3 positionWS, float4 vertexColor)
            {
                float3 back = _LKKeeperBack.xyz;
                float3 wind = float3(_LKKeeperSway.z, 0, _LKKeeperSway.w);
                float3 push = back * _LKKeeperSway.x + wind * _LKKeeperSway.y;
                float flutter = sin(_Time.y * 3.0 + positionWS.y * 4.0) * 0.25;
                float3 sway = push + back * (flutter * saturate(_LKKeeperSway.x + _LKKeeperSway.y));
                return sway * (vertexColor.r * _SwayStrength);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
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
                float4 color : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS += KeeperSway(output.positionWS, input.color);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 albedo = albedoSample.rgb * _BaseColor.rgb;
                float luma = dot(albedo, float3(0.2126, 0.7152, 0.0722));
                albedo = lerp(albedo, luma.xxx, saturate(_Desaturate));
                // Vertex AO: the floor and the contrast curve are per material, so skin can go nearly black in the hood's hollow.
                float ao = lerp(1.0, lerp(_AOFloor, 1.0, pow(saturate(input.color.g), _AOContrast)), _VertexAO);
                albedo *= ao;
                albedo *= 1.0 + _EdgeLighten * input.color.b;
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 normalWS = normalize(mul(normalTS, float3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float wrap = saturate(dot(normalWS, mainLight.direction) * 0.55 + 0.45);
                float3 ambient = max(SampleSH(normalWS), float3(0.07, 0.08, 0.10));
                float3 color = albedo * (ambient + mainLight.color * (wrap * mainLight.shadowAttenuation));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    float addDot = saturate(dot(normalWS, addLight.direction));
                    // The lantern sits a hand's width from the cloth, so 1/d^2 alone blows it out: cap the attenuation.
                    float addAtten = min(addLight.distanceAttenuation, _LightCap);
                    color += albedo * addLight.color * (addAtten * addLight.shadowAttenuation * addDot);
                LIGHT_LOOP_END
                #endif

                #if !defined(_LK_KEEPER_NO_RIM)
                float fresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDir))), _RimPower);
                color += _RimColor.rgb * fresnel * _RimStrength * ao;
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _Desaturate;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float _SwayStrength;
                float _VertexAO;
                float _AOFloor;
                float _AOContrast;
                float _LightCap;
                float _EdgeLighten;
            CBUFFER_END
            float4 _LKKeeperSway;
            // World-space backward of the keeper (horizontal, normalised). Zero when unset: no backward push.
            float4 _LKKeeperBack;

            float3 KeeperSway(float3 positionWS, float4 vertexColor)
            {
                float3 back = _LKKeeperBack.xyz;
                float3 wind = float3(_LKKeeperSway.z, 0, _LKKeeperSway.w);
                float3 push = back * _LKKeeperSway.x + wind * _LKKeeperSway.y;
                float flutter = sin(_Time.y * 3.0 + positionWS.y * 4.0) * 0.25;
                float3 sway = push + back * (flutter * saturate(_LKKeeperSway.x + _LKKeeperSway.y));
                return sway * (vertexColor.r * _SwayStrength);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += KeeperSway(positionWS, input.color);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
