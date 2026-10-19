Shader "LanternKeeper/AdditiveUnlit"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.55, 0.15, 0.45)
        _BaseMap ("Texture", 2D) = "white" {}
        // 0 = purely additive (everything that uses this shader today). Towards 1 the card also darkens what is behind it (premultiplied alpha), so it
        // tints a bright sky the same warm colour it tints a dark sea. The finale beacon columns use it: pure additive amber on the pale sunrise sky
        // comes out nearly white and reads as a different, much fainter shaft above the horizon than over the sea.
        _AlphaMix ("Alpha Mix (0 = additive)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _AlphaMix;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 c = tex * _BaseColor * input.color;
                // Premultiplied: rgb * a added, and the background kept by (1 - a * _AlphaMix); _AlphaMix 0 is exactly the old SrcAlpha One.
                return half4(c.rgb * c.a, c.a * _AlphaMix);
            }
            ENDHLSL
        }
    }
}
