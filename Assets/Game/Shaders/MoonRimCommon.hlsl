#ifndef LK_MOON_RIM_COMMON
#define LK_MOON_RIM_COMMON

#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

float4 _LKMoonRimColor;
float _LKMoonRimStrength;
float4 _LKMoonDir;
float _LKMoonRimBoost;

float4 _RimTexel;   // xy: one camera pixel in uv
float _RimWidth;    // edge tap offset in camera pixels
float _RimSimple;   // 1 on Low: two edge taps and forward-difference normals

static const float RimFadeStart = 15.0;
static const float RimFadeEnd = 70.0;
static const float RimBoostGain = 1.3333;   // 0.6 * (1 + gain) = 0.6 + 0.8

bool IsSky(float raw)
{
#if UNITY_REVERSED_Z
    return raw <= 1e-6;
#else
    return raw >= 1.0 - 1e-6;
#endif
}

float3 RimWorldPos(float2 uv, float raw)
{
    return ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
}

float RimEyeDepth(float raw)
{
    return IsSky(raw) ? 1000.0 : LinearEyeDepth(raw, _ZBufferParams);
}

float RimAmount(float2 uv)
{
    float raw = SampleSceneDepth(uv);
    if (IsSky(raw))
    {
        return 0.0;
    }

    float d = RimEyeDepth(raw);
    float fade = 1.0 - saturate((d - RimFadeStart) / (RimFadeEnd - RimFadeStart));
    if (fade <= 0.0)
    {
        return 0.0;
    }

    float2 o = _RimTexel.xy * _RimWidth;
    float rawR = SampleSceneDepth(uv + float2(o.x, 0));
    float rawU = SampleSceneDepth(uv + float2(0, o.y));
    float dR = RimEyeDepth(rawR);
    float dU = RimEyeDepth(rawU);
    float rawL = rawR;
    float rawD = rawU;
    float dL = dR;
    float dD = dU;
    if (_RimSimple < 0.5)
    {
        rawL = SampleSceneDepth(uv - float2(o.x, 0));
        rawD = SampleSceneDepth(uv - float2(0, o.y));
        dL = RimEyeDepth(rawL);
        dD = RimEyeDepth(rawD);
    }

    // Edge: the neighbour is clearly farther than this surface, so this pixel is the near side of a silhouette.
    float t0 = 0.06 * d + 0.3;
    float far = max(max(dR - d, dU - d), max(dL - d, dD - d));
    float edge = saturate((far - t0) / (t0 * 2.0));

    // Normal from the smaller depth step on each axis, so a silhouette does not smear the normal.
    float3 p = RimWorldPos(uv, raw);
    float3 px;
    float3 py;
    if (_RimSimple < 0.5)
    {
        bool useR = abs(dR - d) < abs(dL - d);
        px = useR ? RimWorldPos(uv + float2(o.x, 0), rawR) - p : p - RimWorldPos(uv - float2(o.x, 0), rawL);
        bool useU = abs(dU - d) < abs(dD - d);
        py = useU ? RimWorldPos(uv + float2(0, o.y), rawU) - p : p - RimWorldPos(uv - float2(0, o.y), rawD);
    }
    else
    {
        px = RimWorldPos(uv + float2(o.x, 0), rawR) - p;
        py = RimWorldPos(uv + float2(0, o.y), rawU) - p;
    }

    float3 n = normalize(cross(px, py));
    float3 toCam = _WorldSpaceCameraPos - p;
    if (dot(n, toCam) < 0.0)
    {
        n = -n;
    }

    // Slopes that face the moon pick up a little rim; flat ground stays clean. Edges lean toward the moon side too,
    // so the rim reads as moonlight catching the form rather than a drawn outline.
    float moonDot = dot(n, normalize(_LKMoonDir.xyz));
    float facing = smoothstep(0.55, 0.95, moonDot);
    facing *= 1.0 - smoothstep(0.85, 1.0, n.y);
    edge *= lerp(0.5, 1.0, saturate(moonDot + 0.25));

    return (edge * 0.2 + facing * 0.25) * fade;
}

float3 RimColour(float amount)
{
    float strength = _LKMoonRimStrength * (1.0 + RimBoostGain * _LKMoonRimBoost);
    return _LKMoonRimColor.rgb * strength * amount;
}

float4 FragRim(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return float4(RimColour(RimAmount(input.texcoord)), 0.0);
}

float4 FragUpsample(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return float4(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0).rgb, 0.0);
}

#endif
