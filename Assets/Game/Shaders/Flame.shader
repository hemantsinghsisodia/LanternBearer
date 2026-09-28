Shader "LanternKeeper/Flame"
{
    Properties
    {
        _Speed ("Speed", Float) = 1.4
        _Distort ("Distort", Float) = 0.16
        _Intensity ("Intensity", Float) = 1.35
        _Roll ("Roll", Float) = 0
        _Core ("Core", Color) = (1, 0.96, 0.72, 1)
        _Mid ("Mid", Color) = (1, 0.42, 0.06, 1)
        _Tip ("Tip", Color) = (0.55, 0.04, 0.01, 0)
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
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Speed;
            float _Distort;
            float _Intensity;
            float _Roll;
            float4 _Core;
            float4 _Mid;
            float4 _Tip;

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

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float3 axisX = float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20);
                float3 axisY = float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21);
                float scaleX = max(length(axisX), 0.0001);
                float scaleY = max(length(axisY), 0.0001);
                float3 right = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(1, 0, 0)));
                float3 up = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(0, 1, 0)));
                float s = sin(_Roll);
                float c = cos(_Roll);
                float2 p = input.positionOS.xy;
                float2 rolled = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                float3 worldPos = center + right * rolled.x * scaleX + up * rolled.y * scaleY;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _Speed;
                float n = Noise(float2(uv.x * 4.5, uv.y * 6.5 - time * 1.5));
                float n2 = Noise(float2(uv.x * 8.0 + 2.2, uv.y * 10.0 - time * 2.3));
                float y = saturate(uv.y);
                float x = uv.x + (n - 0.5) * _Distort * y + (n2 - 0.5) * _Distort * 0.35;
                float halfWidth = lerp(0.46, 0.04, pow(y, 0.62));
                halfWidth *= lerp(0.78, 1.18, n2);
                float edge = abs(x - 0.5);
                float mask = smoothstep(halfWidth, halfWidth * 0.28, edge);
                float baseCut = smoothstep(0.0, 0.05, y);
                float tip = 1.0 - smoothstep(0.58, 1.0, y + (n - 0.5) * 0.15);
                float edgeFade = smoothstep(0.5, 0.38, abs(uv.x - 0.5)) * smoothstep(0.5, 0.3, abs(uv.y - 0.5));
                float alpha = mask * baseCut * tip * edgeFade;
                float core = saturate(1.15 - y * 1.45) * saturate(1.0 - edge * 3.4);
                float3 color = lerp(_Tip.rgb, _Mid.rgb, saturate(1.2 - y));
                color = lerp(color, _Core.rgb, core * core);
                color *= _Intensity * (0.82 + 0.28 * n);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
