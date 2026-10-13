Shader "LanternKeeper/FinaleIsland"
{
    // The finale's miniature Islands 1-4: opaque vertex coloured geometry (terrain, trees, rocks, beacon bases), seen 300 m away.
    // Vertex colour is sRGB (baked by FinaleInstaller). Lit by the main light (the moon at night) with a half-lambert wrap plus a moon rim,
    // tinted by the global _SilhouetteTint (cold at night, warm at dawn) and brightened by _LanternSkyBlend (the dawn).
    // _Fade is a dithered fade-in (opaque, so props and terrain sort and write depth correctly).
    // _Flare / _FlareCenter (xyz world position, w radius) light the ground and props round the island's own beacons while they flare. Per renderer.
    Properties
    {
        _FarColor ("Haze", Color) = (0.07, 0.075, 0.11, 1)
        _RimColor ("Rim", Color) = (1, 0.72, 0.42, 1)
        _RimStrength ("Rim Strength", Float) = 1
        _Ambient ("Night Ambient", Range(0, 1)) = 0.22
        _DawnAmbient ("Dawn Ambient", Range(0, 1)) = 0.8
        _Wrap ("Light Wrap", Range(0, 2)) = 0.75
        _FlareColor ("Flare", Color) = (1, 0.62, 0.28, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Flare ("Flare Amount", Float) = 0
        _FlareCenter ("Flare Centre", Vector) = (0, 0, 0, 40)
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

        float4 _FarColor;
        float4 _RimColor;
        float _RimStrength;
        float _Ambient;
        float _DawnAmbient;
        float _Wrap;
        float4 _FlareColor;
        float _Fade;
        float _Flare;
        float4 _FlareCenter;
        float4 _SilhouetteTint;
        float _LanternSkyBlend;

        float Bayer4(float2 pixel)
        {
            int x = int(pixel.x) & 3;
            int y = int(pixel.y) & 3;
            const float4x4 bayer = float4x4(
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5);
            return (bayer[y][x] + 0.5) * (1.0 / 16.0);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 color : TEXCOORD2;
            };

            float3 TintOrWhite(float3 tint)
            {
                float peak = max(tint.r, max(tint.g, tint.b));
                return lerp(float3(1, 1, 1), tint, saturate(peak * 8));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color.rgb;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_Fade - Bayer4(input.positionCS.xy));

                float3 albedo = SRGBToLinear(input.color);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight();
                float dawn = saturate(_LanternSkyBlend);
                float ambient = lerp(_Ambient, _DawnAmbient, dawn);
                float wrap = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);
                float light = ambient + _Wrap * wrap * wrap * (1.0 - ambient);
                float3 tint = TintOrWhite(_SilhouetteTint.rgb);
                float3 color = albedo * tint * light;

                // The island's own beacons light the ground and trees round them.
                float3 absolutePos = GetAbsolutePositionWS(input.positionWS);
                float near = saturate(1.0 - distance(absolutePos, _FlareCenter.xyz) / max(_FlareCenter.w, 0.01));
                float flareLight = _Flare * near * near;
                color += (albedo * 1.4 + 0.04) * _FlareColor.rgb * flareLight * (0.55 + 0.45 * wrap);

                float dist = length(absolutePos - _WorldSpaceCameraPos.xyz);
                float aerial = saturate((dist - 140.0) / 360.0);
                color = lerp(color, _FarColor.rgb * tint, aerial * 0.4 * (1.0 - dawn * 0.5));

                float moon = saturate(dot(normalWS, mainLight.direction));
                float edge = pow(saturate(1.0 - abs(dot(normalWS, viewDir))), 2.4);
                color += _RimColor.rgb * moon * edge * _RimStrength * (0.12 + 0.2 * dawn);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                clip(_Fade - Bayer4(input.positionCS.xy));
                return 0;
            }
            ENDHLSL
        }
    }
}
