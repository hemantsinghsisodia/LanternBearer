Shader "LanternKeeper/NightSky"
{
    Properties
    {
        _Top ("Top", Color) = (0.015, 0.03, 0.09, 1)
        _Horizon ("Horizon", Color) = (0.07, 0.22, 0.26, 1)
        _Ground ("Ground", Color) = (0.005, 0.006, 0.01, 1)
        _Exponent ("Exponent", Float) = 1.6
        _HorizonBand ("Horizon Band", Float) = 0.12
        _Blend ("Dawn Blend", Range(0, 1)) = 0
        _DawnTop ("Dawn Top", Color) = (0.494, 0.612, 0.796, 1)
        _DawnHorizon ("Dawn Horizon", Color) = (0.957, 0.702, 0.541, 1)
        _DawnGround ("Dawn Ground", Color) = (0.788, 0.541, 0.478, 1)
        _MoonDir ("Moon Direction", Vector) = (0.3, 0.45, 0.8, 0)
        _MoonColor ("Moon Color", Color) = (0.85, 0.9, 1, 1)
        _MoonOn ("Moon On", Float) = 1
        _MoonSize ("Moon Size", Float) = 0.035
        _MoonGlow ("Moon Glow", Float) = 0.25
        _Clouds ("Clouds", Float) = 0
        _CloudColor ("Cloud Color", Color) = (0.1, 0.12, 0.17, 1)
        // DawnSequence animates these on the SkyBlend skybox; kept so it can read and write them without warnings. Not used for shading.
        [HideInInspector] _Exposure ("Exposure", Float) = 1
        [HideInInspector] _Tint ("Tint", Color) = (1, 1, 1, 1)
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
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float4 _Top;
            float4 _Horizon;
            float4 _Ground;
            float _Exponent;
            float _HorizonBand;
            float _Blend;
            float4 _DawnTop;
            float4 _DawnHorizon;
            float4 _DawnGround;
            float4 _MoonDir;
            float4 _MoonColor;
            float _MoonOn;
            float _MoonSize;
            float _MoonGlow;
            float _Clouds;
            float4 _CloudColor;

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
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
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

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float y = dir.y;
                float3 top = lerp(_Top.rgb, _DawnTop.rgb, _Blend);
                float3 horizon = lerp(_Horizon.rgb, _DawnHorizon.rgb, _Blend);
                float3 ground = lerp(_Ground.rgb, _DawnGround.rgb, _Blend);

                // Soft horizon band: the horizon colour holds over +-_HorizonBand before easing to top or ground.
                float band = max(_HorizonBand, 0.001);
                float up = saturate((y - band * 0.5) / (1.0 - band * 0.5));
                float down = saturate((-y - band * 0.5) / (1.0 - band * 0.5));
                float3 color = lerp(horizon, top, pow(up, _Exponent));
                color = lerp(color, ground, pow(down, _Exponent));

                // Slow scrolling cloud bands above the horizon.
                if (_Clouds > 0.5)
                {
                    float2 uv = dir.xz / (max(y, 0.0) + 0.35);
                    uv *= float2(1.4, 3.2);
                    uv.x += _Time.y * 0.01;
                    float n = ValueNoise(uv * 2.0) * 0.6 + ValueNoise(uv * 4.3 + 7.0) * 0.4;
                    float cloud = smoothstep(0.45, 0.8, n) * smoothstep(0.0, 0.15, y) * (1.0 - smoothstep(0.55, 0.95, y));
                    color = lerp(color, _CloudColor.rgb, cloud * 0.8);
                }

                // Moon disc plus glow.
                if (_MoonOn > 0.5)
                {
                    float3 moonDir = normalize(_MoonDir.xyz);
                    float cosAng = dot(dir, moonDir);
                    float ang = acos(clamp(cosAng, -1.0, 1.0));
                    float disc = 1.0 - smoothstep(_MoonSize * 0.9, _MoonSize, ang);
                    float glow = exp(-ang * 7.0) * _MoonGlow;
                    float fade = 1.0 - _Blend;
                    color += _MoonColor.rgb * (disc + glow) * fade;
                }

                // Dither to hide banding in the dark gradient. It targets the 8-bit sRGB output, so the +-0.5/255 step is added
                // in sRGB space and converted back; adding it in linear would be amplified several times at dark values.
                float3 srgb = LinearToSRGB(max(color, 0.0));
                srgb += (Hash21(input.positionCS.xy) - 0.5) / 255.0;
                color = SRGBToLinear(srgb);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
