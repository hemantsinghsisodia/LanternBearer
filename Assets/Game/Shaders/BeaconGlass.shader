Shader "LanternKeeper/BeaconGlass"
{
    // Storm lantern panes of the beacon. Opaque (it writes depth, so the MoonRim post pass and the depth/normals consumers see it),
    // dark glass with a faint cold reflection when unlit; when lit the pane blazes: _AmberColor towards _CoreColor in the middle of the pane.
    // _Glow is per instance (set by BeaconVisual through a MaterialPropertyBlock: flicker x moment curve x flare). 0 = unlit, 1 = full steady glow.
    // _AmberColor and _CoreColor are the LookPalette signal colours, set by CreatureInstaller; do not retint them.
    // No ShadowCaster pass: the panes are inside the iron frame, which casts the shadow.
    Properties
    {
        _GlassColor ("Dark Glass", Color) = (0.035, 0.045, 0.07, 1)
        _ColdColor ("Cold Reflection", Color) = (0.58, 0.72, 0.95, 1)
        _AmberColor ("Pane Amber", Color) = (1, 0.694, 0.361, 1)
        _CoreColor ("Pane Core", Color) = (1, 0.827, 0.541, 1)
        _EmissionGain ("Emission Gain", Range(0, 12)) = 4
        _Glow ("Glow", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _GlassColor;
            float4 _ColdColor;
            float4 _AmberColor;
            float4 _CoreColor;
            float _EmissionGain;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(GlassProps)
            UNITY_DEFINE_INSTANCED_PROP(float, _Glow)
        UNITY_INSTANCING_BUFFER_END(GlassProps)
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float glow = UNITY_ACCESS_INSTANCED_PROP(GlassProps, _Glow);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDir)), 3.0);

                // Unlit: dark glass, a cold sky reflection at grazing angles and a thin moon glint.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 reflected = reflect(-mainLight.direction, normalWS);
                float glint = pow(saturate(dot(reflected, viewDir)), 48.0) * mainLight.shadowAttenuation;
                float3 cold = _ColdColor.rgb * (fresnel * 0.35 + glint * 0.5 * saturate(Luminance(mainLight.color) * 2.0));
                float3 color = _GlassColor.rgb + cold;

                // Lit: amber at the pane edges, the warm core in the middle.
                float2 c = input.uv * 2.0 - 1.0;
                float2 e = c * float2(0.9, 0.75);
                float centre = saturate(1.0 - dot(e, e));
                float3 emission = lerp(_AmberColor.rgb, _CoreColor.rgb, centre * centre) * (0.65 + 0.35 * centre);
                color = lerp(color, color * 0.25, saturate(glow)) + emission * (_EmissionGain * glow);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(NormalizeNormalPerPixel(normalize(input.normalWS)), 0.0);
            }
            ENDHLSL
        }
    }
}
