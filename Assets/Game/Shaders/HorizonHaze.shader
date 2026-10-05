Shader "LanternKeeper/HorizonHaze"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.45, 0.62, 0.68, 0.16)
        _BandScale ("Band Scale", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            Fog { Mode Off }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BaseColor;
            float _LanternSkyBlend;
            float _BandScale;

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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float y = saturate(input.uv.y);
                float band = sin(y * 3.14159265);
                // 1 is today's single soft band. Above 1 adds extra soft bands. No keyword, so Medium stays one variant.
                if (_BandScale > 1.01)
                {
                    band = abs(sin(y * 3.14159265 * _BandScale));
                }
                // _BaseColor is the island fog colour; at dawn it follows the fog colour DawnSequence blends to the dawn horizon.
                float3 color = lerp(_BaseColor.rgb, unity_FogColor.rgb, saturate(_LanternSkyBlend));
                float alpha = _BaseColor.a * band;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
