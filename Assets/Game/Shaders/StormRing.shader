Shader "LanternKeeper/StormRing"
{
    // The storm wall that circles Island 5 far out at sea: a ring of soft cloud cards (one mesh, one draw call), lit from within by distant lightning.
    // The cards face the island. Vertex stage: the angular distance of each vertex from the gap centre (_GapCenter, a bearing 0..1 from +Z, clockwise)
    // and from the last strike (_FlashAngle). Fragment stage: a tiling cloud noise (R), eaten away by a dissolve threshold that grows from the gap
    // centre outwards as _Open goes 0 -> 1 (finale: the storm tears open from the beam bearing).
    // Per frame values (StormRing.cs): _Open, _GapCenter, _Flash, _FlashAngle, _Density, _Warm.
    Properties
    {
        _CloudTex ("Cloud noise (R)", 2D) = "white" {}
        _DarkColor ("Dark cloud", Color) = (0.035, 0.04, 0.075, 1)
        _LitColor ("Lit from within", Color) = (0.55, 0.62, 1, 1)
        _WarmColor ("Sunrise tint", Color) = (1, 0.62, 0.4, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.85
        _Density ("Density", Range(0, 1)) = 1
        _Open ("Open", Range(0, 1)) = 0
        _GapCenter ("Gap centre (bearing 0..1)", Float) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashAngle ("Flash bearing (0..1)", Float) = 0
        _Warm ("Warm", Range(0, 1)) = 0
        _UTime ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
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
                float4 _DarkColor;
                float4 _LitColor;
                float4 _WarmColor;
                float _Alpha;
                float _Density;
                float _Open;
                float _GapCenter;
                float _Flash;
                float _FlashAngle;
                float _Warm;
                float _UTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 card : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 card : TEXCOORD1;
                float2 dist : TEXCOORD2;
            };

            // 0 at the same bearing, 1 on the opposite side.
            float AngleDistance(float a, float b)
            {
                return abs(frac(a - b + 0.5) - 0.5) * 2.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.card = input.card;
                float bearing = frac(atan2(input.positionOS.x, input.positionOS.z) / 6.2831853);
                output.dist = float2(AngleDistance(bearing, _GapCenter), AngleDistance(bearing, _FlashAngle));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Continuous ribbons: uv.x runs round the circle (the noise tiles), uv.y is 0 at the base and 1 at the top; card.y is the layer (0 lowest).
                float2 uv = input.uv;
                float v = uv.y;
                float2 scroll = float2(_UTime * (0.004 + 0.006 * input.card.x) + input.card.x * 7.0, input.card.y * 0.37);
                float shape = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv + scroll).r;
                // Dense and dark at the base, wispy at the top: the threshold climbs with height, and the top fades out.
                float baseFade = smoothstep(0.0, 0.14, v);
                float topFade = 1.0 - smoothstep(0.5 + 0.25 * input.card.y, 1.0, v);
                float dens = saturate((shape * lerp(1.8, 1.0, v) - lerp(0.1, 0.52, v)) * 3.0);
                float body = dens * baseFade * topFade;

                // The dissolve threshold grows outwards from the gap centre; the dense cores last longest.
                float dissolve = saturate((_Open * 1.5 - input.dist.x + (shape - 0.35) * 0.5) * 2.5);
                float alpha = saturate(body - dissolve) / max(1.0 - dissolve, 0.001);
                alpha *= _Alpha * _Density * lerp(1.0, 0.7, input.card.y);

                // Lit from within: a strike lights the clouds near it (the denser the brighter), plus a faint idle flicker.
                float near = 1.0 - smoothstep(0.0, 0.4, input.dist.y);
                float idle = 0.5 + 0.5 * sin(_UTime * 1.7 + input.card.x * 31.0 + shape * 6.0);
                float lit = saturate(_Flash * near * (0.35 + 0.65 * shape) + 0.05 * idle * shape);
                float3 colour = lerp(_DarkColor.rgb * lerp(0.55, 1.2, v), _LitColor.rgb, lit);
                colour = lerp(colour, _WarmColor.rgb * (0.4 + 0.6 * shape), _Warm);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
