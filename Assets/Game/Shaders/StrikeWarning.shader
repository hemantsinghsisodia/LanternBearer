Shader "LanternKeeper/StrikeWarning"
{
    // The ground ring that warns of a lightning strike (LightningHunter): a soft cold-blue ring on a disc mesh that hugs the terrain,
    // pulsing for the 1.0 s warning while a second ring closes in on the centre. Never amber (amber means fire and safety).
    // uv.xy = the position on the disc, -1..1 (length 1 at the hit radius). Per strike values (LightningHunter.cs):
    //   _Progress 0..1 over the warning, _Pulse the pulse depth (1 normal, smaller with Reduce flashing), _Alpha overall.
    Properties
    {
        _Color ("Ring colour", Color) = (0.737, 0.824, 1, 1)
        _Progress ("Warning progress", Range(0, 1)) = 0
        _Pulse ("Pulse depth", Range(0, 1)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Progress;
                float _Pulse;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);
                // The outer ring at the hit radius.
                float outer = smoothstep(0.78, 0.9, r) * (1.0 - smoothstep(0.96, 1.0, r));
                // A second ring closes in from the edge to the centre over the warning.
                float closing = 1.0 - _Progress;
                float inner = (1.0 - smoothstep(0.0, 0.1, abs(r - closing * 0.8))) * step(0.05, closing);
                // A faint fill.
                float fill = (1.0 - smoothstep(0.0, 0.95, r)) * 0.12;
                // Three pulses over the 1.0 s warning (one soft pulse with Reduce flashing: the depth shrinks).
                float wave = 0.5 + 0.5 * sin(_Progress * 6.2831853 * 3.0 - 1.5707963);
                float pulse = lerp(1.0 - 0.55 * _Pulse, 1.0, wave);
                float fadeIn = smoothstep(0.0, 0.12, _Progress);
                float a = saturate(outer + inner * 0.8 + fill) * pulse * fadeIn * _Alpha;
                return half4(_Color.rgb * (0.7 + 0.6 * wave * _Pulse), a);
            }
            ENDHLSL
        }
    }
}
