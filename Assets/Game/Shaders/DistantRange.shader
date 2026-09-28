Shader "LanternKeeper/DistantRange"
{
    Properties
    {
        _RockMap ("Rock", 2D) = "white" {}
        _RockNormal ("Rock Normal", 2D) = "bump" {}
        _GroundMap ("Ground", 2D) = "white" {}
        _GroundNormal ("Ground Normal", 2D) = "bump" {}
        _SkyCube ("Sky", Cube) = "" {}
        _DawnCube ("Dawn", Cube) = "" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _SkyExposure ("Sky Exposure", Float) = 1.5
        _DawnExposure ("Dawn Exposure", Float) = 1.2
        _AerialStart ("Aerial Start", Float) = 320
        _AerialEnd ("Aerial End", Float) = 760
        _MapScale ("Map Scale", Float) = 0.016
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _Tint;
            float _SkyExposure;
            float _DawnExposure;
            float _AerialStart;
            float _AerialEnd;
            float _MapScale;
            float _LanternSkyBlend;
            float4 _SilhouetteTint;

            TEXTURE2D(_RockMap);
            SAMPLER(sampler_RockMap);
            TEXTURE2D(_RockNormal);
            SAMPLER(sampler_RockNormal);
            TEXTURE2D(_GroundMap);
            SAMPLER(sampler_GroundMap);
            TEXTURE2D(_GroundNormal);
            SAMPLER(sampler_GroundNormal);
            TEXTURECUBE(_SkyCube);
            SAMPLER(sampler_SkyCube);
            TEXTURECUBE(_DawnCube);
            SAMPLER(sampler_DawnCube);

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

            float3 TintOrWhite(float3 tint)
            {
                float peak = max(tint.r, max(tint.g, tint.b));
                return lerp(float3(1, 1, 1), tint, saturate(peak * 8));
            }

            float3 TriplanarWeights(float3 n)
            {
                float3 w = abs(n);
                w = pow(max(w, 1e-4), 4.0);
                return w / (w.x + w.y + w.z);
            }

            float3 TriplanarAlbedo(TEXTURE2D_PARAM(tex, samp), float3 pos, float3 w, float scale)
            {
                float3 x = SAMPLE_TEXTURE2D(tex, samp, pos.zy * scale).rgb;
                float3 y = SAMPLE_TEXTURE2D(tex, samp, pos.xz * scale).rgb;
                float3 z = SAMPLE_TEXTURE2D(tex, samp, pos.xy * scale).rgb;
                return x * w.x + y * w.y + z * w.z;
            }

            float3 TriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 pos, float3 geom, float3 w, float scale)
            {
                float3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, pos.zy * scale), 0.85);
                float3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, pos.xz * scale), 0.85);
                float3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, pos.xy * scale), 0.85);
                tx = float3(tx.zy, tx.x);
                ty = float3(ty.x, ty.z, ty.y);
                tz = float3(tz.x, tz.y, tz.z);
                float3 bent = normalize(tx * w.x + ty * w.y + tz * w.z);
                return normalize(geom + bent * 0.65);
            }

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
                float3 geom = normalize(input.normalWS);
                float3 pos = GetAbsolutePositionWS(input.positionWS);
                float3 weights = TriplanarWeights(geom);
                float scale = max(_MapScale, 0.001);
                float steep = smoothstep(0.22, 0.58, 1.0 - saturate(geom.y));
                float3 rock = TriplanarAlbedo(TEXTURE2D_ARGS(_RockMap, sampler_RockMap), pos, weights, scale);
                float3 ground = TriplanarAlbedo(TEXTURE2D_ARGS(_GroundMap, sampler_GroundMap), pos, weights, scale * 0.85);
                float3 albedo = lerp(ground, rock, steep);
                float3 normalWS = steep > 0.5
                    ? TriplanarNormal(TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal), pos, geom, weights, scale)
                    : TriplanarNormal(TEXTURE2D_ARGS(_GroundNormal, sampler_GroundNormal), pos, geom, weights, scale * 0.85);

                float crest = saturate(input.crest);
                float baseBand = 1.0 - smoothstep(0.0, 0.33, crest);
                albedo = lerp(albedo, rock * float3(0.42, 0.4, 0.38), baseBand);
                albedo *= lerp(1.05, 0.32, baseBand);
                albedo *= _Tint.rgb;
                if (dot(normalWS, geom) < 0.0)
                {
                    normalWS = reflect(normalWS, geom);
                }
                normalWS = normalize(lerp(geom, normalWS, 0.45));

                Light mainLight = GetMainLight();
                float ndotl = dot(geom, mainLight.direction);
                float wrap = lerp(0.48, 1.05, saturate(ndotl * 0.5 + 0.5));
                wrap = lerp(wrap, 0.16, baseBand);
                float3 lightColor = mainLight.color.rgb;
                if (dot(lightColor, float3(1, 1, 1)) < 0.04)
                {
                    lightColor = float3(0.72, 0.8, 0.95);
                }
                float3 ambient = unity_AmbientSky.rgb * 0.35 + float3(0.04, 0.045, 0.05);
                float3 color = albedo * (lightColor * wrap + ambient);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(saturate(1.0 - dot(geom, viewDir)), 4.0);
                float moon = saturate(ndotl);
                color += albedo * float3(0.7, 0.78, 0.9) * rim * moon * crest * 0.55;

                float3 tint = TintOrWhite(_SilhouetteTint.rgb);
                float tintLuma = dot(tint, float3(0.2126, 0.7152, 0.0722));
                if (tintLuma > 0.001)
                {
                    tint *= 0.9 / tintLuma;
                }
                color *= tint;

                float3 look = -viewDir;
                float blend = saturate(_LanternSkyBlend);
                float3 skyNight = SAMPLE_TEXTURECUBE_LOD(_SkyCube, sampler_SkyCube, look, 0).rgb;
                float3 skyDawn = SAMPLE_TEXTURECUBE_LOD(_DawnCube, sampler_DawnCube, look, 0).rgb;
                float exposure = lerp(_SkyExposure, _DawnExposure, blend);
                float3 sky = lerp(skyNight, skyDawn, blend) * exposure;
                float dist = length(GetAbsolutePositionWS(input.positionWS) - _WorldSpaceCameraPos.xyz);
                float aerial = smoothstep(_AerialStart, max(_AerialEnd, _AerialStart + 1.0), dist);
                aerial *= smoothstep(0.28, 0.62, crest);
                color = lerp(color, sky, aerial);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
