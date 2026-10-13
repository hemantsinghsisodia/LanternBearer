Shader "LanternKeeper/FinaleGodRays"
{
    // The finale sunrise's god rays (FinaleClouds.cs): tapered shafts of light that slant down from the sun's side of the sky over the sea,
    // all in one mesh. Each card is a ribbon along the light direction (-_SunDir): the vertex stage turns it to face the camera round its own axis.
    // uv0.x is the side (0/1), uv0.y runs 0 at the top of the shaft to 1 at the sea; uv1.x is the start height above the object, uv1.y the half
    // width at the start (it widens towards the sea), colour R a random. Additive. _Strength is the capped strength (Reduce flashing).
    Properties
    {
        _CloudTex ("Streak noise (R)", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 0.78, 0.5, 1)
        _Strength ("Strength", Range(0, 1)) = 0
        _SunDir ("Sun direction", Vector) = (0, 0.1, 1, 0)
        _UTime ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
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

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SunDir;
                float _Strength;
                float _UTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 shaft : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 data : TEXCOORD0;   // x across (-1..1), y along (0..1), z random
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 sun = normalize(_SunDir.xyz);
                float3 axis = -sun;
                float3 start = TransformObjectToWorld(input.positionOS.xyz);
                float height = max(input.shaft.x, 1.0);
                float run = min(height / max(sun.y, 0.06), 650.0);
                float3 centre = start + axis * run * input.uv.y;
                float3 side = cross(axis, normalize(centre - _WorldSpaceCameraPos));
                side = normalize(side + float3(0.0, 0.0001, 0.0));
                float across = input.uv.x * 2.0 - 1.0;
                float width = input.shaft.y * lerp(1.0, 2.2, input.uv.y);
                output.positionCS = TransformWorldToHClip(centre + side * across * width);
                output.data = float3(across, input.uv.y, input.color.r);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float lateral = 1.0 - abs(input.data.x);
                lateral = lateral * lateral * (3.0 - 2.0 * lateral);
                float along = smoothstep(0.0, 0.18, input.data.y) * (1.0 - smoothstep(0.4, 0.95, input.data.y));
                float2 uv = float2(input.data.x * 0.35 + input.data.z * 9.0, input.data.y * 0.5 + _UTime * 0.004);
                float streak = 0.55 + 0.9 * SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).r;
                float shimmer = 0.9 + 0.1 * sin(_UTime * 0.6 + input.data.z * 40.0);
                float a = lateral * along * streak * shimmer * _Strength;
                return half4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
