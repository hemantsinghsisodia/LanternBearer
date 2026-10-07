Shader "LanternKeeper/Shade"
{
    // Smoke wraith for the Island 4 Shade: an ink dark, near opaque body with a ragged smoke hem, a swaying tatter, an optional
    // lightning outline and a freeze burn. Opaque dithered alpha-cutout: no transparency, so depth and sorting stay correct.
    // Object space is the imported FBX space of Body (Z up, hem near _HeightMin, head near _HeightMin + _HeightRange).
    // Per renderer (MaterialPropertyBlock, written by ShadeVisual and Shade.cs): _BaseColor (a = body alpha only; its rgb is ignored),
    // _Stunned (0..1, eased by ShadeVisual from ShadeState.Stunned: a stunned Shade keeps a dark cold navy ink and the crisp outline shows it),
    // _Outline (0..1, follows the capped lightning flash), _Burn (0..1 while frozen in the lantern light), _Speed (m/s).
    // Hem breakup: the bottom _HemFrac of the height is eaten by animated noise; the same function runs in every pass so the depth,
    // depth normals and shadow passes clip exactly like the colour pass.
    // Burn: the edge of the erosion glows a dim ash ember (_BurnColor). It is deliberately NOT amber: amber means safe warmth.
    // Low: _LKShadeLow = 1 uses one noise octave. The outline and the hem breakup are unchanged. A uniform branch, never a keyword.
    // Stencil bit 8 is written (the writers are KeeperLit, BeaconGlass, BeaconIron and this shader) so the screen-space moon rim skips the Shade: its slope lift washed the ink body pale grey on
    // faces turned to the moon. The Shade keeps a dim moon rim of its own (_RimColor, _RimStrength) so the silhouette still reads at the edges.
    Properties
    {
        _BaseColor ("Ink And Alpha", Color) = (0.02, 0.02, 0.04, 0.92)
        _InkColor ("Ink", Color) = (0.02, 0.02, 0.04, 1)
        _StunInkColor ("Stunned Ink", Color) = (0.04, 0.055, 0.11, 1)
        _BodyAlphaRef ("Full Body Alpha", Range(0.1, 1)) = 0.92
        _SkyTint ("Sky Tint Strength", Range(0, 1)) = 0.08
        _RimColor ("Moon Rim", Color) = (0.58, 0.72, 0.95, 1)
        _RimStrength ("Moon Rim Strength", Range(0, 1)) = 0.3
        _OutlineColor ("Outline Colour", Color) = (0.737, 0.824, 1, 1)
        _OutlineGain ("Outline Gain", Range(0, 8)) = 3
        _BurnColor ("Burn Edge Colour", Color) = (0.659, 0.416, 0.333, 1)
        _BurnGain ("Burn Edge Gain", Range(0, 4)) = 1.0
        _HeightMin ("Hem Height (object Z)", Float) = 0.12
        _HeightRange ("Body Height", Float) = 2.2
        _HemFrac ("Hem Fraction", Range(0.05, 0.6)) = 0.3
        _NoiseScale ("Noise Scale", Float) = 4.5
        _SwayAmount ("Tatter Sway (m)", Range(0, 0.5)) = 0.07
        _Outline ("Outline", Range(0, 1)) = 0
        _Burn ("Burn", Range(0, 1)) = 0
        _Speed ("Speed (m/s)", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _InkColor;
            float4 _StunInkColor;
            float _BodyAlphaRef;
            float _SkyTint;
            float4 _RimColor;
            float _RimStrength;
            float4 _OutlineColor;
            float _OutlineGain;
            float4 _BurnColor;
            float _BurnGain;
            float _HeightMin;
            float _HeightRange;
            float _HemFrac;
            float _NoiseScale;
            float _SwayAmount;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(ShadeProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _Outline)
            UNITY_DEFINE_INSTANCED_PROP(float, _Burn)
            UNITY_DEFINE_INSTANCED_PROP(float, _Speed)
            UNITY_DEFINE_INSTANCED_PROP(float, _Stunned)
        UNITY_INSTANCING_BUFFER_END(ShadeProps)

        // 1 on the Low preset. Set by ShadeVisual.
        float _LKShadeLow;
        // Set by Wind: (dirX, dirZ, strength01, 0). Zero on windless islands.
        float4 _LKWind;

        static const float ShadeMaxSpeed = 5.0;

        float ShHash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float ShNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(lerp(ShHash(i), ShHash(i + float3(1, 0, 0)), f.x), lerp(ShHash(i + float3(0, 1, 0)), ShHash(i + float3(1, 1, 0)), f.x), f.y);
            float b = lerp(lerp(ShHash(i + float3(0, 0, 1)), ShHash(i + float3(1, 0, 1)), f.x), lerp(ShHash(i + float3(0, 1, 1)), ShHash(i + float3(1, 1, 1)), f.x), f.y);
            return lerp(a, b, f.z);
        }

        // Three octaves on Ultra, one on Low. The result stays centred near 0.5 either way.
        float ShFbm(float3 p)
        {
            float n = ShNoise(p);
            if (_LKShadeLow < 0.5)
            {
                n = n * 0.6 + ShNoise(p * 2.1 + 3.7) * 0.28 + ShNoise(p * 4.3 + 9.1) * 0.12;
            }

            return n;
        }

        float ShadeHeight01(float3 positionOS)
        {
            return saturate((positionOS.z - _HeightMin) / max(_HeightRange, 0.01));
        }

        // Tatter sway in world space, weighted toward the hem. Wind direction and gusts push it; speed adds a trailing flutter.
        float3 ShadeSway(float3 positionWS, float3 positionOS, float speed01)
        {
            float h01 = ShadeHeight01(positionOS);
            float w = (1.0 - h01);
            w *= w;
            float gust = _LKWind.z;
            float2 dir = _LKWind.xy;
            float dirLen = length(dir);
            dir = dirLen > 0.001 ? dir / dirLen : float2(0.7, 0.3);
            float amp = (_SwayAmount + 0.12 * gust + 0.10 * speed01) * w;
            float4x4 m = GetObjectToWorldMatrix();
            float phase = _Time.y * (1.5 + speed01 * 1.5) + m._m03 * 0.7 + m._m23 * 0.5 + positionOS.x * 3.0 + positionOS.y * 2.0;
            positionWS.xz += dir * amp * (0.6 + 0.4 * sin(phase));
            positionWS.xz += float2(-dir.y, dir.x) * amp * 0.6 * sin(phase * 1.7 + h01 * 6.0);
            return positionWS;
        }

        // 1 where the body stays, 0 where the hem or the burn has eaten it. edge is the burn edge amount (0..1).
        float ShadeVisibility(float3 positionOS, float appear, float burn, float speed01, out float edge)
        {
            float h01 = ShadeHeight01(positionOS);
            float hem = 1.0 - smoothstep(0.0, _HemFrac, h01);
            float rate = 1.0 + 3.0 * burn + 0.5 * speed01;
            float t = _Time.y;
            float3 p = positionOS * _NoiseScale;
            p.z -= t * 0.8 * rate;
            float n = ShFbm(p);
            float vis = saturate((n - hem * 1.1 + 0.15) / 0.2);
            // Above the hem the body is solid: a low noise dip must not punch holes in it.
            vis = lerp(1.0, vis, saturate(hem * 4.0));

            edge = 0.0;
            if (burn > 0.001)
            {
                float n2 = ShFbm(positionOS * float3(6.0, 6.0, 4.0) + float3(11.3, 4.7, -t * 0.5 * rate));
                float thr = burn * 0.42;
                float alive = saturate((n2 - thr) / 0.02);
                float band = 1.0 - saturate((n2 - thr) / 0.07);
                float flicker = 0.7 + 0.3 * ShNoise(positionOS * 9.0 + float3(0, 0, t * 14.0));
                edge = band * alive * flicker * saturate(burn * 4.0);
                vis *= alive;
            }

            return vis * appear;
        }

        // Interleaved gradient noise on the pixel, the same in every pass.
        float ShadeDither(float4 positionCS)
        {
            return frac(52.9829189 * frac(dot(positionCS.xy, float2(0.06711056, 0.00583715))));
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            Stencil
            {
                Ref 8
                WriteMask 8
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                float3 positionWS = ShadeSway(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.xyz, speed01);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _BaseColor);
                float outline = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Outline);
                float burn = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Burn);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                float appear = saturate(baseColor.a / _BodyAlphaRef);
                float edge;
                float vis = ShadeVisibility(input.positionOS, appear, burn, speed01, edge);
                clip(vis - ShadeDither(input.positionCS));

                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float h01 = ShadeHeight01(input.positionOS);

                // Ink, lifted by the island's sky ambient so the hue follows the island. A stunned Shade (_Stunned, set by ShadeVisual)
                // keeps a dark cold navy ink: the lightning outline is what shows it.
                float stunned = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Stunned));
                float3 ink = lerp(_InkColor.rgb, _StunInkColor.rgb, stunned);
                float3 sky = SampleSH(normalWS);
                float3 color = ink * lerp(0.7, 1.0, h01) + sky * _SkyTint;

                float fresnel = saturate(1.0 - abs(dot(normalWS, viewDir)));
                color += _RimColor.rgb * pow(fresnel, 3.0) * _RimStrength;
                // Lightning outline: a pale blue rim that follows the capped flash.
                float edgeBand = smoothstep(0.5, 0.85, fresnel);
                color += _OutlineColor.rgb * (outline * _OutlineGain) * (edgeBand + 0.12 * pow(fresnel, 2.0));
                // Freeze burn: the dim ash ember edge of the erosion.
                color += _BurnColor.rgb * (edge * _BurnGain);
                color = MixFog(color, input.fogFactor);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                float3 positionWS = ShadeSway(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.xyz, speed01);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _BaseColor);
                float burn = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Burn);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                float edge;
                float vis = ShadeVisibility(input.positionOS, saturate(baseColor.a / _BodyAlphaRef), burn, speed01, edge);
                clip(vis - ShadeDither(input.positionCS));
                return 0;
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
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                output.positionCS = TransformWorldToHClip(ShadeSway(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.xyz, speed01));
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _BaseColor);
                float burn = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Burn);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                float edge;
                float vis = ShadeVisibility(input.positionOS, saturate(baseColor.a / _BodyAlphaRef), burn, speed01, edge);
                clip(vis - ShadeDither(input.positionCS));
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
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                output.positionCS = TransformWorldToHClip(ShadeSway(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.xyz, speed01));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _BaseColor);
                float burn = UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Burn);
                float speed01 = saturate(UNITY_ACCESS_INSTANCED_PROP(ShadeProps, _Speed) / ShadeMaxSpeed);
                float edge;
                float vis = ShadeVisibility(input.positionOS, saturate(baseColor.a / _BodyAlphaRef), burn, speed01, edge);
                clip(vis - ShadeDither(input.positionCS));
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
