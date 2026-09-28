Shader "LanternKeeper/SkyBlend"
{
    Properties
    {
        _NightCube ("Night", Cube) = "" {}
        _DawnCube ("Dawn", Cube) = "" {}
        _Blend ("Blend", Range(0, 1)) = 0
        _Exposure ("Exposure", Float) = 1.5
        _DawnExposure ("Dawn Exposure", Float) = 1.2
        _Rotation ("Rotation", Float) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Haze", Color) = (0.07, 0.16, 0.2, 1)
        _Haze ("Haze", Float) = 0.48
        _MoonDir ("Moon Direction", Vector) = (0, 0.6, 0.8, 0)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off
        Pass
        {
            Fog { Mode Off }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_NightCube);
            SAMPLER(sampler_NightCube);
            TEXTURECUBE(_DawnCube);
            SAMPLER(sampler_DawnCube);
            float _Blend;
            float _Exposure;
            float _Rotation;
            float4 _Tint;
            float _Haze;
            float4 _MoonDir;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float3 CompressNight(float3 hdr)
            {
                float luma = max(dot(hdr, float3(0.2126, 0.7152, 0.0722)), 1e-4);
                float extra = max(luma - 0.35, 0.0);
                float mapped = luma + extra * (1.0 / (1.0 + extra * 0.55) - 1.0);
                hdr *= mapped / luma;
                return hdr * float3(0.58, 0.66, 0.92);
            }

            float StarLayer(float3 dir, float2 scale, float threshold)
            {
                float2 uv = float2(atan2(dir.z, dir.x) * 0.15915494 + 0.5, asin(clamp(dir.y, -1.0, 1.0)) * 0.31830989 + 0.5);
                float2 grid = uv * scale;
                float2 id = floor(grid);
                float2 cell = frac(grid) - 0.5;
                float n = Hash21(id);
                float star = step(threshold, n);
                float2 jitter = float2(Hash21(id + 19.19), Hash21(id + 47.77)) - 0.5;
                float d = length(cell - jitter * 0.72);
                float size = lerp(0.045, 0.12, Hash21(id + 3.7));
                float starPoint = smoothstep(size, size * 0.15, d);
                float twinkle = 0.45 + 0.55 * sin(_Time.y * (1.1 + n * 4.5) + n * 42.0);
                return starPoint * star * twinkle;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float rad = _Rotation * 0.017453292;
                float s = sin(rad);
                float c = cos(rad);
                dir = float3(c * dir.x + s * dir.z, dir.y, -s * dir.x + c * dir.z);
                float blend = saturate(_Blend);
                float3 night = CompressNight(SAMPLE_TEXTURECUBE_LOD(_NightCube, sampler_NightCube, dir, 0).rgb);
                float3 dawn = SAMPLE_TEXTURECUBE_LOD(_DawnCube, sampler_DawnCube, dir, 0).rgb;
                float exposure = _Exposure;
                float3 color = lerp(night, dawn, blend) * exposure * _Tint.rgb;

                float3 side = float3(dir.x, 0.0, dir.z);
                float sideLen = length(side);
                float3 horizonDir = sideLen > 0.001 ? normalize(float3(side.x, 0.03, side.z)) : float3(0.0, 1.0, 0.0);
                float3 hazeNight = CompressNight(SAMPLE_TEXTURECUBE_LOD(_NightCube, sampler_NightCube, horizonDir, 0).rgb);
                float3 hazeDawn = SAMPLE_TEXTURECUBE_LOD(_DawnCube, sampler_DawnCube, horizonDir, 0).rgb;
                float3 hazeCol = lerp(hazeNight, hazeDawn, blend) * exposure * _Tint.rgb;
                float band = smoothstep(0.07, 0.008, abs(dir.y));
                color = lerp(color, hazeCol, band * saturate(_Haze));

                float3 moonDir = _MoonDir.xyz;
                if (length(moonDir) < 0.2)
                {
                    moonDir = _MainLightPosition.xyz;
                }
                moonDir = normalize(moonDir + float3(1e-5, 0, 0));
                float moonDot = saturate(dot(dir, moonDir));
                float horizonFade = smoothstep(0.0, 0.16, dir.y);
                float moonMask = 1.0 - smoothstep(0.96, 0.992, moonDot);
                float stars = StarLayer(dir, float2(140.0, 70.0), 0.955);
                stars += StarLayer(dir, float2(260.0, 130.0), 0.978) * 0.7;
                float awayFromMoon = 1.0 - smoothstep(0.15, 0.88, moonDot);
                stars *= horizonFade * moonMask * (1.0 - blend) * lerp(0.45, 1.65, awayFromMoon);
                color += stars * float3(4.6, 4.9, 5.4);

                float disc = smoothstep(0.99995, 0.999985, moonDot);
                float halo = pow(moonDot, 520.0) * 0.08;
                float3 moonColor = max(_MainLightColor.rgb, float3(0.75, 0.82, 1.0));
                float moonFade = 1.0 - blend;
                color += moonColor * (disc * 0.85 + halo) * moonFade;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
