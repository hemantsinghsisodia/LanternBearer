Shader "LanternKeeper/Silhouette"
{
    Properties
    {
        _NearColor ("Near", Color) = (0.05, 0.07, 0.1, 1)
        _FarColor ("Far", Color) = (0.2, 0.28, 0.32, 1)
        _Band ("Band", Range(0, 1)) = 0
        _RimColor ("Rim", Color) = (0.62, 0.7, 0.82, 1)
        _RimStrength ("Rim Strength", Float) = 0.4
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Fog { Mode Off }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _NearColor;
            float4 _FarColor;
            float _Band;
            float4 _RimColor;
            float _RimStrength;
            float4 _SilhouetteTint;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float localY : TEXCOORD2;
            };

            float3 TintOrWhite(float3 tint)
            {
                float peak = max(tint.r, max(tint.g, tint.b));
                return lerp(float3(1, 1, 1), tint, saturate(peak * 8));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.localY = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 tint = TintOrWhite(_SilhouetteTint.rgb);
                float3 color = lerp(_NearColor.rgb, _FarColor.rgb, saturate(_Band));
                float dist = length(GetAbsolutePositionWS(input.positionWS) - _WorldSpaceCameraPos.xyz);
                float aerial = saturate((dist - 140.0) / 360.0);
                color = lerp(color, _FarColor.rgb, aerial * 0.45);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight();
                float h = smoothstep(0.0, 1.0, saturate((input.localY + 10.0) / 22.0));
                color *= tint;
                float moon = saturate(dot(normalWS, mainLight.direction));
                float edge = pow(saturate(1.0 - abs(dot(normalWS, viewDir))), 2.4);
                color += _RimColor.rgb * moon * edge * _RimStrength * h;
                return half4(color, h);
            }
            ENDHLSL
        }
    }
}

