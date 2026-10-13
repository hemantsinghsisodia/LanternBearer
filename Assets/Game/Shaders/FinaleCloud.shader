Shader "LanternKeeper/FinaleCloud"
{
    // The Island 5 finale's cloud banks (FinaleClouds.cs): one mesh of camera-facing puffs per depth layer (near, mid, far), one material each.
    // Vertex stage: the puff centre is the vertex position; the corner (uv0), size in metres and a random (uv1) and the height in the bank (colour R)
    // build a billboard. The puffs drift round the island, and the storm breaks open: puffs near _GapCenter (a bearing 0..1 from +Z) are pushed round
    // to either side of a gap of half-width _GapHalf * _Open * _GapScale, bunching at its edges; the ones right in the gap shrink away.
    // Fragment stage: a soft noise-eaten disc (alpha), a fake sphere normal from the disc, the shade from it (dark undersides, lighter tops),
    // and the sun: gold and pink on the edges that face it, a thin bright rim where the cloud is thin and the sun is behind it, a glow near the horizon.
    Properties
    {
        _CloudTex ("Cloud noise (R)", 2D) = "white" {}
        _StormColor ("Storm shade", Color) = (0.05, 0.055, 0.1, 1)
        _StormTop ("Storm top", Color) = (0.2, 0.22, 0.36, 1)
        _ShadeColor ("Dawn undersides (violet)", Color) = (0.27, 0.19, 0.44, 1)
        _TopColor ("Dawn tops", Color) = (0.62, 0.5, 0.72, 1)
        _GoldColor ("Sun edge gold", Color) = (1, 0.827, 0.541, 1)
        _PinkColor ("Sun edge pink", Color) = (1, 0.52, 0.62, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.92
        _Fade ("Fade", Range(0, 1)) = 0
        _Dawn ("Dawn", Range(0, 1)) = 0
        _Open ("Open", Range(0, 1)) = 0
        _GapScale ("Gap scale", Range(0, 1)) = 1
        _GapHalf ("Gap half width (turns)", Float) = 0.17
        _GapCenter ("Gap centre (bearing 0..1)", Float) = 0
        _Drift ("Drift (turns per second)", Float) = 0.0005
        _Haze ("Haze", Range(0, 1)) = 0
        _HorizonGlow ("Horizon glow", Range(0, 2)) = 0
        _NoiseScale ("Noise scale", Float) = 1.4
        _FadeHeight ("Fade height above the sea (m)", Float) = 50
        _SunDir ("Sun direction", Vector) = (0, 0.1, 1, 0)
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
                float4 _StormColor;
                float4 _StormTop;
                float4 _ShadeColor;
                float4 _TopColor;
                float4 _GoldColor;
                float4 _PinkColor;
                float4 _SunDir;
                float _Alpha;
                float _Fade;
                float _Dawn;
                float _Open;
                float _GapScale;
                float _GapHalf;
                float _GapCenter;
                float _Drift;
                float _Haze;
                float _HorizonGlow;
                float _NoiseScale;
                float _FadeHeight;
                float _UTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 puff : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float4 data : TEXCOORD1;      // x random, y height in the bank, z tear (1 = whole), w elevation above the camera
                float3 sunVS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float seaHeight : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 centre = input.positionOS.xyz;
                float radius = length(centre.xz);
                float turn = atan2(centre.x, centre.z) * 0.15915494 + _Drift * _UTime;
                float signedD = frac(turn - _GapCenter + 0.5) - 0.5;
                float side = signedD >= 0.0 ? 1.0 : -1.0;
                float ad = abs(signedD);
                // The window: puffs closer than gapO (turns) to the gap centre thin out and vanish; the banks beside it slide a little way
                // outwards, and rise into towers (bigger, higher) the further they stand from the window. Everything else stays put.
                float gapO = _Open * _GapHalf * _GapScale;
                float push = _Open * 0.045 * (1.0 - smoothstep(0.0, 0.3, ad)) * (0.85 + 0.3 * input.puff.y);
                float placed = _GapCenter + side * (ad + push);
                float angle = placed * 6.2831853;
                float edgeK = smoothstep(gapO, gapO + 0.12, ad);
                float heightK = lerp(1.0, lerp(0.6, 1.3, edgeK), _Open);
                float3 world = TransformObjectToWorld(float3(sin(angle) * radius, centre.y * heightK, cos(angle) * radius));

                float tear = lerp(1.0, smoothstep(gapO * 0.7, gapO * 1.15 + 0.001, ad), saturate(_Open * 8.0));
                float size = input.puff.x * lerp(0.55, 1.0, tear) * lerp(1.0, lerp(0.75, 1.2, edgeK), _Open);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float2 c = input.uv * 2.0 - 1.0;
                float3 pos = world + (right * c.x + up * c.y * 0.72) * size * 0.5;
                output.positionCS = TransformWorldToHClip(pos);
                output.corner = c;
                output.seaHeight = pos.y - unity_ObjectToWorld._m13;
                float3 toCentre = world - _WorldSpaceCameraPos;
                output.data = float4(input.puff.y, input.color.r, tear, toCentre.y / max(length(toCentre.xz), 1.0));
                output.sunVS = mul((float3x3)UNITY_MATRIX_V, normalize(_SunDir.xyz));
                output.viewWS = normalize(toCentre);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.corner;
                float r = length(p);
                if (r >= 1.0)
                {
                    return half4(0, 0, 0, 0);
                }

                float h01 = input.data.y;
                // Two noise octaves, a different spot of the texture for each puff (the texture is 2:1, so u is halved).
                float2 spot = p * 0.5 * _NoiseScale * float2(0.5, 1.0) + input.data.x * float2(5.37, 2.91) + float2(_UTime * 0.0015, 0.0);
                float n1 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, spot).r;
                float n2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, spot * 2.7 + 0.31).r;
                float shape = n1 * 0.62 + n2 * 0.38;

                // A soft disc eaten away by the noise; the tops are wispier (a higher threshold).
                float body = 1.0 - r * r;
                float cut = lerp(0.1, 0.3, h01);
                float density = saturate((body * 1.35 + (shape - 0.5) * 1.1 - cut) * 2.6);
                float alpha = density * density * (3.0 - 2.0 * density);
                alpha *= _Alpha * _Fade * input.data.z * smoothstep(0.0, max(_FadeHeight, 1.0), input.seaHeight);
                if (alpha < 0.004)
                {
                    return half4(0, 0, 0, 0);
                }

                // A fake sphere normal, bumped by the noise, in view space (z towards the camera).
                float3 n = normalize(float3(p.x + (n2 - n1) * 0.35, p.y * 1.2 + (n1 - 0.5) * 0.25, sqrt(saturate(1.0 - r * r)) + 0.15));
                float3 sunVS = normalize(input.sunVS);
                float ndl = dot(n, sunVS);
                float3 nWS = mul(n, (float3x3)UNITY_MATRIX_V);
                float lift = lerp(0.45, 1.0, nWS.y * 0.5 + 0.5) * lerp(0.7, 1.1, h01);

                float3 dark = lerp(_StormColor.rgb, _ShadeColor.rgb, _Dawn);
                float3 top = lerp(_StormTop.rgb, _TopColor.rgb, _Dawn);
                float3 colour = lerp(dark, top, saturate(lift - 0.2) * saturate(shape * 1.3));

                // The sun: gold and pink on the edges that face it, strongest on the silhouette.
                float facing = saturate(ndl);
                float edge = smoothstep(0.25, 1.0, r);
                float3 sunColour = lerp(_PinkColor.rgb, _GoldColor.rgb, saturate(facing * 1.4));
                colour += sunColour * pow(facing, 1.4) * (0.3 + 0.9 * edge) * 1.3 * _Dawn;

                // Looking towards the sun, the thin parts of a cloud glow gold (light through the cloud).
                float toSun = saturate(dot(input.viewWS, normalize(_SunDir.xyz)));
                float thin = 1.0 - saturate(density * 1.2);
                colour += _GoldColor.rgb * pow(toSun, 3.0) * thin * 1.9 * _Dawn;

                // The far banks glow where they meet the horizon under the sun.
                float low = 1.0 - smoothstep(0.0, 0.2, input.data.w);
                colour += float3(1.0, 0.62, 0.36) * _HorizonGlow * low * (0.25 + 0.75 * pow(toSun, 1.5)) * _Dawn;

                colour = lerp(colour, unity_FogColor.rgb, _Haze);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
