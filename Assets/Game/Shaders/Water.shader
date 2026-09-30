Shader "LanternKeeper/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.105, 0.344, 0.402, 0.42)
        _DeepColor ("Deep", Color) = (0.016, 0.055, 0.064, 0.9)
        _FoamColor ("Foam", Color) = (0.78, 0.86, 0.84, 0.8)
        _NormalA ("Normal A", 2D) = "bump" {}
        _NormalB ("Normal B", 2D) = "bump" {}
        _FoamNoise ("Foam Noise", 2D) = "white" {}
        _SkyCube ("Sky", Cube) = "" {}
        _DawnCube ("Dawn", Cube) = "" {}
        _NormalScale ("Normal Scale", Float) = 0.28
        _DepthFade ("Depth Fade", Float) = 2.4
        _Refraction ("Refraction", Float) = 0.03
        _FoamDepth ("Foam Depth", Float) = 1.6
        _Glitter ("Glitter", Float) = 0.55
        _SkyExposure ("Sky Exposure", Float) = 1.5
        _DawnExposure ("Dawn Exposure", Float) = 1.2
        _WaveAmp ("Wave Amp", Float) = 8
        _FadeStart ("Ring Start", Float) = 520
        _FadeEnd ("Ring End", Float) = 630
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _LK_WATER_LOW _LK_WATER_HIGH
            #define _SCREENSPACEREFLECTIONS_OFF
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            float4 _ShallowColor;
            float4 _DeepColor;
            float4 _FoamColor;
            float _NormalScale;
            float _DepthFade;
            float _Refraction;
            float _FoamDepth;
            float _Glitter;
            float _SkyExposure;
            float _DawnExposure;
            float _WaveAmp;
            float _FadeStart;
            float _FadeEnd;
            float _LanternSkyBlend;
            float4 _WaterTint;
            float _LK_RippleRange;
            float _LK_FoamReach;
            float _LK_DetailStrength;

            TEXTURE2D(_NormalA);
            SAMPLER(sampler_NormalA);
            TEXTURE2D(_NormalB);
            SAMPLER(sampler_NormalB);
            TEXTURE2D(_FoamNoise);
            SAMPLER(sampler_FoamNoise);
            TEXTURECUBE(_SkyCube);
            SAMPLER(sampler_SkyCube);
            TEXTURECUBE(_DawnCube);
            SAMPLER(sampler_DawnCube);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 absoluteWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            void AccumulateGerstner(float3 p, float2 dir, float amplitude, float wavelength, float steepness, float speed, float time, inout float3 offset, inout float3 normal)
            {
                float k = 6.2831853 / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(dir);
                float f = k * (dot(d, p.xz) - c * speed * time);
                float s = sin(f);
                float co = cos(f);
                offset.x += d.x * (steepness * amplitude) * co;
                offset.y += amplitude * s;
                offset.z += d.y * (steepness * amplitude) * co;
                normal.x -= d.x * k * amplitude * co;
                normal.z -= d.y * k * amplitude * co;
                normal.y -= steepness * k * amplitude * s;
            }

            void Waves(float3 p, float time, out float3 offset, out float3 normal)
            {
                offset = 0;
                normal = float3(0, 1, 0);
                float amp = _WaveAmp;
                AccumulateGerstner(p, float2(1.0, 0.08), 0.112 * amp, 42.0, 0.2, 0.35, time, offset, normal);
                AccumulateGerstner(p, float2(1.0, 0.22), 0.028 * amp, 34.0, 0.26, 0.7, time, offset, normal);
                AccumulateGerstner(p, float2(-0.82, -0.36), 0.016 * amp, 22.0, 0.24, 0.75, time, offset, normal);
                AccumulateGerstner(p, float2(-0.32, 0.95), 0.011 * amp, 14.0, 0.22, 0.9, time, offset, normal);
                normal = normalize(normal);
            }

            float3 CompressNight(float3 hdr)
            {
                float luma = max(dot(hdr, float3(0.2126, 0.7152, 0.0722)), 1e-4);
                float extra = max(luma - 0.35, 0.0);
                float mapped = luma + extra * (1.0 / (1.0 + extra * 0.55) - 1.0);
                hdr *= mapped / luma;
                return hdr * float3(0.58, 0.66, 0.92);
            }

            float3 SampleSky(float3 dir)
            {
                float3 night = CompressNight(SAMPLE_TEXTURECUBE_LOD(_SkyCube, sampler_SkyCube, dir, 0).rgb);
                float3 dawn = SAMPLE_TEXTURECUBE_LOD(_DawnCube, sampler_DawnCube, dir, 0).rgb;
                float blend = saturate(_LanternSkyBlend);
                float exposure = lerp(_SkyExposure, _DawnExposure, blend);
                return lerp(night, dawn, blend) * exposure;
            }

            float3 TintOrWhite(float3 tint)
            {
                float peak = max(tint.r, max(tint.g, tint.b));
                return lerp(float3(1, 1, 1), tint, saturate(peak * 8));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 absoluteWS = GetAbsolutePositionWS(positionWS);
                float3 offset;
                float3 normalWS;
                Waves(absoluteWS, _Time.y, offset, normalWS);
                positionWS += offset;
                absoluteWS += offset;
                output.positionWS = positionWS;
                output.absoluteWS = absoluteWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float3 absoluteWS = input.absoluteWS;
                float eye = length(_WorldSpaceCameraPos.xyz - GetAbsolutePositionWS(input.positionWS));
                float3 normalWS;
                float depth01;
                float shore;
                float diff;
                float nearRipple;
                float ripplePatch;
                float foam;
#if defined(_LK_WATER_LOW)
                normalWS = normalize(input.normalWS);
                depth01 = 1.0;
                shore = 1.0;
                diff = 80.0;
                nearRipple = 0.0;
                ripplePatch = 0.0;
                foam = 0.0;
#else
                float ripple = lerp(1.15, 0.85, smoothstep(40.0, 240.0, eye));
                float normalLod = min(eye * 0.008, 1.6);
                float2 uv1 = absoluteWS.xz * 0.032 + float2(time * 0.014, time * 0.008);
                float2 uv2 = absoluteWS.xz * 0.048 + float2(-time * 0.011, time * 0.012);
                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalA, sampler_NormalA, uv1, normalLod));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalB, sampler_NormalB, uv2, normalLod));
                float3 nTS = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
#if defined(_LK_WATER_HIGH)
                float2 uv3 = absoluteWS.xz * 0.091 + float2(-time * 0.021, time * 0.016);
                float2 uv4 = absoluteWS.xz * 0.137 + float2(time * 0.013, -time * 0.019);
                float3 n3 = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalA, sampler_NormalA, uv3, normalLod));
                float3 n4 = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalB, sampler_NormalB, uv4, normalLod));
                float detail = saturate(_LK_DetailStrength);
                nTS.xy += (n3.xy + n4.xy) * detail * 0.55;
                nTS = normalize(nTS);
#endif
                nTS.xy *= ripple * 1.8;
                nTS = normalize(nTS);
                float3 up = normalize(input.normalWS);
                float3 tangent = normalize(cross(float3(0, 0, 1), up));
                float3 bitangent = cross(up, tangent);
                normalWS = normalize(tangent * nTS.x + bitangent * nTS.y + up * nTS.z);
#endif

#if !defined(_LK_WATER_LOW)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                diff = sceneEye - input.eyeDepth;
                depth01 = saturate(diff / max(_DepthFade, 0.01));
                shore = saturate(diff / 0.32);
                #if UNITY_REVERSED_Z
                if (rawDepth <= 0.0001)
                #else
                if (rawDepth >= 0.9999)
                #endif
                {
                    depth01 = 1.0;
                    shore = 1.0;
                    diff = 80.0;
                }
#endif

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - GetAbsolutePositionWS(input.positionWS));
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float ndotv = saturate(dot(normalWS, viewDir));
                float fresnel = 0.02 + 0.98 * pow(1.0 - ndotv, 5.0);

                float3 shallow = _ShallowColor.rgb;
                float3 deep = _DeepColor.rgb;
                float3 water = lerp(shallow, deep, depth01);
                float shallowness = 1.0 - depth01;
#if !defined(_LK_WATER_LOW)
                float2 refractUV = screenUV + normalWS.xz * _Refraction * shallowness;
                float3 refracted = SampleSceneColor(refractUV);
                water = lerp(water, refracted, shallowness * shore * 0.45 * (1.0 - fresnel));
#endif

#if !defined(_LK_WATER_LOW)
#if defined(_LK_WATER_HIGH)
                float rippleRange = _LK_RippleRange < 1.0 ? 1.0 : _LK_RippleRange;
                nearRipple = 1.0 - smoothstep(40.0, 120.0 * rippleRange, eye);
#else
                nearRipple = 1.0 - smoothstep(40.0, 120.0, eye);
#endif
                float2 foamUv = absoluteWS.xz * 0.042 + float2(time * 0.012, time * 0.006);
                float foamField = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUv).g;
                ripplePatch = n1.x * 0.38 + n1.y * 0.28 + n2.x * 0.26 + n2.y * 0.18;
                ripplePatch += (foamField - 0.3) * 1.15;
#if defined(_LK_WATER_HIGH)
                ripplePatch += (n3.x + n4.y) * 0.12 * detail;
#endif
#endif

                float3 reflectNormal = normalize(float3(normalWS.x * 2.6, normalWS.y, normalWS.z * 2.6));
                float3 reflectDir = reflect(-viewDir, reflectNormal);
                float3 probe = GlossyEnvironmentReflection(reflectDir, 0.02, 1);
                float3 sky = SampleSky(reflectDir);
                float skyPeak = max(sky.r, max(sky.g, sky.b));
                float3 env = skyPeak > 0.02 ? sky : probe;
                float3 flatReflect = reflect(-viewDir, float3(0.0, 1.0, 0.0));
                float path = smoothstep(0.55, 0.95, saturate(dot(flatReflect, lightDir)));
                float glintAlign = saturate(dot(reflectDir, lightDir));
                float tight = pow(glintAlign, 180.0);
                float glow = pow(glintAlign, 24.0);
                float sparkle = saturate(glow * 0.75 + tight * 0.25);
                water = lerp(water, env, fresnel);
                water *= lerp(1.0, 1.0 + ripplePatch * 0.48, nearRipple);
                water += ripplePatch * 0.06 * nearRipple;
                float moonShade = saturate(dot(normalWS, lightDir));
                water += mainLight.color.rgb * moonShade * 0.22;
                water += mainLight.color.rgb * sparkle * path * _Glitter * 1.5;

#if !defined(_LK_WATER_LOW)
#if defined(_LK_WATER_HIGH)
                float foamReach = max(_LK_FoamReach, 0.01);
#else
                float foamReach = 8.0;
#endif
                foam = saturate(1.0 - diff / foamReach);
                foam *= smoothstep(0.12, 0.48, foamField);
#endif
                water = lerp(water, _FoamColor.rgb, foam * _FoamColor.a);

                float3 tint = TintOrWhite(_WaterTint.rgb);
                water *= tint;

                float fogFactor = ComputeFogFactor(input.positionCS.z);
                float3 fogged = MixFog(water, fogFactor);
                water = lerp(water, fogged, 0.32);

                float radial = length(absoluteWS.xz);
                float ring = smoothstep(_FadeStart, _FadeEnd, radial);
                float3 skyAhead = SampleSky(-viewDir);
                water = lerp(water, skyAhead, ring);

                float alpha = lerp(_ShallowColor.a, 0.92, depth01);
                alpha *= shore;
                alpha = max(alpha, foam * 0.7);
                return half4(water, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
