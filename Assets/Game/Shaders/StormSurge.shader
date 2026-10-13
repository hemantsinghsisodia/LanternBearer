Shader "LanternKeeper/StormSurge"
{
    // Island 5's rising storm surge: a dark, roiling layer on a large disc at the surge height (StormSurge.cs builds the mesh: a swell surface
    // and a wispy mist sheet a little above it, one mesh, one draw call). Two scrolling samples of the cloud noise, the second warped by the
    // first, make it boil; the vertex stage heaves the surface. Lightning lights it from within (_Flash). The disc fades out towards its rim.
    // Vertex colour: r = layer (0 surface, 1 mist), g = radius 0..1.
    // Per frame values (StormSurge.cs): _Alpha, _Flash, _UTime. _Detail 1 adds the second octave and the warp (0 on the Low graphics level).
    Properties
    {
        _CloudTex ("Cloud noise (R)", 2D) = "white" {}
        _DeepColor ("Deep", Color) = (0.012, 0.014, 0.035, 1)
        _DarkColor ("Dark cloud", Color) = (0.065, 0.07, 0.15, 1)
        _LitColor ("Lit from within", Color) = (0.55, 0.62, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Flash ("Flash", Range(0, 1)) = 0
        _Detail ("Detail", Range(0, 1)) = 1
        _Tiling ("World tiling", Float) = 0.018
        _UTime ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _DarkColor;
                float4 _LitColor;
                float _Alpha;
                float _Flash;
                float _Detail;
                float _Tiling;
                float _UTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world : TEXCOORD0;
                float2 layerRadius : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float layer = input.color.r;
                // Slow heaving swells; the mist sheet heaves a little more.
                float swell = sin(p.x * 0.07 + _UTime * 0.9) * cos(p.z * 0.06 - _UTime * 0.7)
                            + 0.5 * sin((p.x + p.z) * 0.15 + _UTime * 1.7);
                p.y += swell * lerp(0.35, 0.6, layer) * (1.0 - input.color.g * 0.6);
                float3 world = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(world);
                output.world = world.xz;
                output.layerRadius = input.color.rg;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float layer = input.layerRadius.x;
                float radius01 = input.layerRadius.y;
                float2 uv = input.world * _Tiling;
                float t = _UTime;
                float n1 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv + float2(t * 0.011, t * 0.007)).r;
                float roil = n1;
                if (_Detail > 0.5)
                {
                    float2 warp = (n1 - 0.5) * 0.35;
                    float n2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv * 2.7 + warp + float2(-t * 0.02, t * 0.013)).r;
                    roil = n1 * 0.6 + n2 * 0.4;
                }

                // Surface: near opaque and dark; the mist sheet is wispy and thins where the noise is low.
                float surfaceAlpha = lerp(0.9, 0.97, roil);
                float mistAlpha = saturate((roil - 0.38) * 2.4) * 0.5;
                float alpha = lerp(surfaceAlpha, mistAlpha, layer);
                alpha *= 1.0 - smoothstep(0.55, 1.0, radius01);
                alpha *= _Alpha;

                // Lit from within: a flash shows the boiling ridges (the denser the noise the brighter), plus a faint cold shimmer.
                float ridge = smoothstep(0.45, 0.85, roil);
                float lit = saturate(_Flash * (0.05 + 0.75 * ridge * ridge));
                float3 colour = lerp(_DeepColor.rgb, _DarkColor.rgb, roil);
                // The boiling cloud tops on the mist sheet catch a little cold light all the time, so the surge reads against the dark sea.
                colour += _LitColor.rgb * (0.035 * ridge + 0.16 * layer * smoothstep(0.42, 0.8, roil));
                colour = lerp(colour, _LitColor.rgb, lit);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
