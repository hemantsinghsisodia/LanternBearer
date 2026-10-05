Shader "LanternKeeper/DistantRange"
{
    Properties
    {
        // Flat silhouette colour for this layer. LookApplier writes it from the island palette.
        _LayerColor ("Layer Colour", Color) = (0.1, 0.14, 0.18, 1)
        // Rim strength is scaled by the global moon rim (_LKMoonRimStrength), which fades out at dawn.
        _RimStrength ("Rim Strength", Float) = 0.25
        _AerialStart ("Aerial Start", Float) = 320
        _AerialEnd ("Aerial End", Float) = 760
        // At full dawn every layer reaches horizon colour * _DawnShade, so no night colour is left. Near layers stay darker than far ones.
        _DawnShade ("Dawn Shade", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Back
        ZWrite On
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _LayerColor;
            float _RimStrength;
            float _AerialStart;
            float _AerialEnd;
            float _DawnShade;
            // Brightness of the crest line on a lightning flash (the water uses 0.10 over its whole surface; this is a thin line, so it is higher).
            static const float LightningRim = 0.35;

            // Globals written by LookApplier, DawnSequence and Lightning.
            float4 _LKMoonRimColor;
            float4 _LKMoonDir;
            float _LKMoonRimStrength;
            float _LKLightningFlash;
            float _LanternSkyBlend;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float crest : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.crest = input.uv.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // unity_FogColor is the profile fog colour at night and DawnSequence's dawn horizon colour at dawn.
                float3 horizon = unity_FogColor.rgb;
                float3 color = lerp(_LayerColor.rgb, horizon * _DawnShade, saturate(_LanternSkyBlend));

                float dist = length(GetAbsolutePositionWS(input.positionWS) - _WorldSpaceCameraPos.xyz);
                float aerial = smoothstep(_AerialStart, max(_AerialEnd, _AerialStart + 1.0), dist);
                color = lerp(color, horizon, aerial * 0.5);

                // Top-line rim: a thin band at the crest, brighter on the side facing the moon.
                float crest = saturate(input.crest);
                float topLine = smoothstep(0.8, 1.0, crest);
                float3 n = normalize(input.normalWS);
                float2 flat = n.xz;
                float facing = saturate(dot(flat / max(length(flat), 1e-4), normalize(_LKMoonDir.xz + 1e-5)) * 0.5 + 0.5);
                float rim = topLine * (0.35 + 0.65 * facing);
                color += _LKMoonRimColor.rgb * rim * _RimStrength * _LKMoonRimStrength;

                // Lightning lights the crest only, and is 0 again after the flash.
                color += float3(0.75, 0.85, 1.0) * topLine * saturate(_LKLightningFlash) * LightningRim;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
