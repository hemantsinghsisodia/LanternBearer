Shader "LanternKeeper/ShadeEye"
{
    // The Shade's two pinprick eyes: cool pale, unlit, a slight flicker. Never amber: amber means safe warmth.
    // The eye meshes are the small child quads of Shade.prefab (Eyes/EyeLeft, Eyes/EyeRight); this shader only supplies the look.
    Properties
    {
        _Color ("Eye Colour", Color) = (0.78, 0.88, 1, 1)
        _Gain ("Gain", Range(0, 8)) = 2.4
        _Flicker ("Flicker", Range(0, 0.5)) = 0.18
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+5"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Gain;
                float _Flicker;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float phase : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float4x4 m = GetObjectToWorldMatrix();
                output.phase = frac(sin(dot(float2(m._m03, m._m23), float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float flicker = 0.5 + 0.5 * sin(t * 9.0 + input.phase) * sin(t * 2.3 + input.phase * 1.7);
                float gain = _Gain * (1.0 - _Flicker * flicker);
                return half4(_Color.rgb * gain, 1);
            }
            ENDHLSL
        }
    }
}
