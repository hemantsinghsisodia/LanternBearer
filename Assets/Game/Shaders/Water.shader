Shader "LanternKeeper/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.105, 0.344, 0.402, 0.42)
        _DeepColor ("Deep", Color) = (0.016, 0.055, 0.064, 0.9)
        _FoamColor ("Foam", Color) = (0.78, 0.86, 0.84, 0.55)
        _RimColor ("Water-line Rim", Color) = (0.55, 0.72, 0.85, 1)
        _TideBandColor ("Tide Band (alpha 0 = off)", Color) = (0, 0, 0, 0)
        _DepthFade ("Depth Fade", Float) = 2.4
        _FoamWidth ("Foam Width", Float) = 0.35
        _RimWidth ("Rim Width", Float) = 0.12
        _SwellScale ("Swell Scale", Float) = 0.02
        _SwellSpeed ("Swell Speed", Float) = 0.04
        _MoonDir ("Moon Dir (xz, w = on)", Vector) = (0, 0, 0, 0)
        _MoonPathStrength ("Moon Path Strength", Float) = 0.55
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
            #pragma multi_compile_local _ _LK_WATER_LOW
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            float4 _ShallowColor;
            float4 _DeepColor;
            float4 _FoamColor;
            float4 _RimColor;
            float4 _TideBandColor;
            float _DepthFade;
            float _FoamWidth;
            float _RimWidth;
            float _SwellScale;
            float _SwellSpeed;
            float4 _MoonDir;
            float _MoonPathStrength;
            float _WaveAmp;
            float _FadeStart;
            float _FadeEnd;
            float4 _WaterTint;
            float _LKLightningFlash;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 absoluteWS : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            // Vertex swell only: the shading never uses a wave normal, so the surface stays painterly.
            float3 WaveOffset(float3 p, float time)
            {
                float3 offset = 0;
                float amp = _WaveAmp;
                float4 w0 = float4(1.0, 0.08, 0.112 * amp, 42.0);
                float4 w1 = float4(1.0, 0.22, 0.028 * amp, 34.0);
                float4 w2 = float4(-0.82, -0.36, 0.016 * amp, 22.0);
                float4 w3 = float4(-0.32, 0.95, 0.011 * amp, 14.0);
                float4 steep = float4(0.2, 0.26, 0.24, 0.22);
                float4 speed = float4(0.35, 0.7, 0.75, 0.9);
                float4 waves[4] = { w0, w1, w2, w3 };
                for (int i = 0; i < 4; i++)
                {
                    float k = 6.2831853 / waves[i].w;
                    float c = sqrt(9.8 / k);
                    float2 d = normalize(waves[i].xy);
                    float f = k * (dot(d, p.xz) - c * speed[i] * time);
                    offset.x += d.x * (steep[i] * waves[i].z) * cos(f);
                    offset.y += waves[i].z * sin(f);
                    offset.z += d.y * (steep[i] * waves[i].z) * cos(f);
                }

                return offset;
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
                float3 offset = WaveOffset(absoluteWS, _Time.y);
                positionWS += offset;
                absoluteWS += offset;
                output.positionWS = positionWS;
                output.absoluteWS = absoluteWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float3 absoluteWS = input.absoluteWS;

                // Water depth = scene depth minus water depth, so the foam and rim follow the real shoreline (and the tide).
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float depth = sceneEye - input.eyeDepth;
                #if UNITY_REVERSED_Z
                if (rawDepth <= 0.0001)
                #else
                if (rawDepth >= 0.9999)
                #endif
                {
                    depth = 80.0;
                }

                depth = max(depth, 0.0);
                float depth01 = saturate(depth / max(_DepthFade, 0.01));
                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                float2 camToFrag = absoluteWS.xz - GetAbsolutePositionWS(_WorldSpaceCameraPos.xyz).xz;
                float eye = length(camToFrag);

#if !defined(_LK_WATER_LOW)
                // Painterly swell bands: two octaves of soft value noise, slowly scrolled.
                float2 sp = absoluteWS.xz * _SwellScale + float2(time * _SwellSpeed, time * _SwellSpeed * 0.6);
                float swell = ValueNoise(sp) * 0.65 + ValueNoise(sp * 2.3 + 7.1) * 0.35;
                float lift = smoothstep(0.38, 0.82, swell) * 0.12;
                water += lift * saturate(_ShallowColor.rgb * 2.5);

                // Broken moon path: a soft streak along the moon azimuth, chopped into dashes by low-frequency noise.
                float2 moonXZ = _MoonDir.xz;
                float moonLen = length(moonXZ);
                if (_MoonDir.w > 0.5 && moonLen > 0.001)
                {
                    float2 m = moonXZ / moonLen;
                    float along = dot(camToFrag, m);
                    float lateral = dot(camToFrag, float2(-m.y, m.x));
                    float width = 4.0 + 0.12 * max(along, 0.0);
                    float streak = (1.0 - smoothstep(0.0, width, abs(lateral))) * smoothstep(6.0, 40.0, along);
                    float dashes = smoothstep(0.4, 0.75, ValueNoise(float2(along * 0.2 - time * 0.15, lateral * 0.08)));
                    water += _RimColor.rgb * (streak * dashes * _MoonPathStrength * 2.4);
                }
#endif

                // Lighting: the moon and the lantern light the flat surface, so the lantern pools on the water.
                Light mainLight = GetMainLight();
                float3 up = float3(0, 1, 0);
                float3 lit = SampleSH(up) * 0.5 + mainLight.color * (0.35 * saturate(mainLight.direction.y));
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = up;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    lit += addLight.color * (addLight.distanceAttenuation * saturate(addLight.direction.y + 0.35));
                LIGHT_LOOP_END
                #endif
                water += water * lit;

                // Foam line, pulsing gently with the swell.
                float pulse = 1.0 + 0.2 * sin(time * 0.9 + absoluteWS.x * 0.05 + absoluteWS.z * 0.04);
                float foamW = max(_FoamWidth * pulse, 0.01);
                float foam = 1.0 - smoothstep(foamW * 0.35, foamW, depth);
                water = lerp(water, _FoamColor.rgb, foam * _FoamColor.a);

                // Thin moonlit rim just outside the foam.
                float rimIn = smoothstep(_FoamWidth, _FoamWidth + _RimWidth * 0.4, depth);
                float rimOut = 1.0 - smoothstep(_FoamWidth + _RimWidth * 0.6, _FoamWidth + _RimWidth, depth);
                float rim = rimIn * rimOut;
                water += _RimColor.rgb * rim * 0.6;

                // Optional tide band: a darker or tinted wet strip in the shallows.
                if (_TideBandColor.a > 0.0)
                {
                    float band = 1.0 - smoothstep(0.35, 0.6, depth);
                    water = lerp(water, _TideBandColor.rgb, band * _TideBandColor.a * 0.6);
                }

                water += _LKLightningFlash * 0.25 * _RimColor.rgb;

                water *= TintOrWhite(_WaterTint.rgb);

                float fogFactor = ComputeFogFactor(input.positionCS.z);
                float3 fogged = MixFog(water, fogFactor);
                water = lerp(water, fogged, 0.32);

                // Distant ring fades into the fog colour rather than a sky cubemap.
                float ring = smoothstep(_FadeStart, _FadeEnd, length(absoluteWS.xz));
                water = lerp(water, unity_FogColor.rgb, ring);

                float alpha = lerp(_ShallowColor.a, 0.92, depth01);
                alpha *= saturate(depth / 0.12);
                alpha = max(alpha, foam * _FoamColor.a);
                alpha = max(alpha, rim * 0.7);
                return half4(water, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
