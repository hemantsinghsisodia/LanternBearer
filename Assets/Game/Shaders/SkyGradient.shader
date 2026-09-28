Shader "LanternKeeper/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.015, 0.03, 0.09, 1)
        _Horizon ("Horizon", Color) = (0.07, 0.22, 0.26, 1)
        _Ground ("Ground", Color) = (0.005, 0.006, 0.01, 1)
        _Exponent ("Exponent", Float) = 1.6
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

            float4 _Top;
            float4 _Horizon;
            float4 _Ground;
            float _Exponent;

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

            half4 frag(Varyings input) : SV_Target
            {
                float y = normalize(input.direction).y;
                float up = saturate(y);
                float down = saturate(-y);
                float3 color = _Horizon.rgb;
                color = lerp(color, _Top.rgb, pow(up, _Exponent));
                color = lerp(color, _Ground.rgb, pow(down, _Exponent));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
