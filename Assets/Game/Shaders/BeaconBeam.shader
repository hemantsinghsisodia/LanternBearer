Shader "LanternKeeper/BeaconBeam"
{
    // Four soft additive light cards, one per pane of the beacon lantern (mesh: ArtSource/Blender/beacon_build.py).
    // Each card is a vertical fan whose outward direction is the object space XZ direction of its vertices (the lantern axis is the object Y axis).
    // UV.x runs from the pane (0) to the tip (1), UV.y across the card.
    // Faded by view angle: a card seen edge on (the camera in its plane) disappears, so the cross of cards never shows a hard sheet.
    // Per instance: _Intensity (the moment curve, 0.6 of it on Low) and _LenScale (1, or 0.5 on Low), set by BeaconVisual through a MaterialPropertyBlock.
    // _BaseColor / _CoreColor are the LookPalette amber and core, set by CreatureInstaller.
    // Additive and ZWrite Off: no DepthOnly or DepthNormals pass on purpose. A translucent glow card must not write depth or normals,
    // and a depth entry would make the MoonRim post pass draw an edge round the light.
    Properties
    {
        _BaseColor ("Amber", Color) = (1, 0.694, 0.361, 1)
        _CoreColor ("Core", Color) = (1, 0.827, 0.541, 1)
        _Strength ("Strength", Range(0, 2)) = 0.5
        _Intensity ("Intensity", Float) = 0
        _LenScale ("Length Scale", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CoreColor;
                float _Strength;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(BeamProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _Intensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _LenScale)
            UNITY_INSTANCING_BUFFER_END(BeamProps)

            // Distance of the pane plane from the lantern axis (BEAM_START in ArtSource/Blender/beacon_build.py).
            static const float BeamStart = 0.125;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 p = input.positionOS.xyz;
                float r = length(p.xz);
                float2 dir = p.xz / max(r, 1e-4);
                float scaled = BeamStart + max(r - BeamStart, 0.0) * UNITY_ACCESS_INSTANCED_PROP(BeamProps, _LenScale);
                p.xz = dir * scaled;
                float3 positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;

                float3 outWS = normalize(TransformObjectToWorldDir(float3(dir.x, 0.0, dir.y)));
                float3 cardNormal = normalize(cross(outWS, float3(0.0, 1.0, 0.0)));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
                output.fade = smoothstep(0.08, 0.55, abs(dot(cardNormal, viewDir)));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float intensity = UNITY_ACCESS_INSTANCED_PROP(BeamProps, _Intensity);
                float t = input.uv.x;
                float along = pow(saturate(1.0 - t), 1.7) * smoothstep(0.0, 0.05, t);
                float across = pow(saturate(1.0 - abs(input.uv.y * 2.0 - 1.0)), 1.6);
                float3 colour = lerp(_CoreColor.rgb, _BaseColor.rgb, saturate(t * 1.4));
                return half4(colour * (along * across * input.fade * intensity * _Strength), 0.0);
            }
            ENDHLSL
        }
    }
}
