#ifndef LK_MOON_RIM_COMMON
#define LK_MOON_RIM_COMMON

#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

float4 _LKMoonRimColor;
float _LKMoonRimStrength;
float4 _LKMoonDir;
float _LKMoonRimBoost;

float4 _RimTexel;   // xy: one camera pixel in uv
float _RimWidth;    // edge tap offset in camera pixels
float _RimSimple;   // 1 on Low: a single edge radius instead of two
float _RimEdgeWeight;
float _RimGain;     // overall amplitude (Low is a quieter version of the same look)

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

// Eye depth of a tap; sky counts as "a lot farther" relative to the centre rather than an absolute 1000 m.
float RimTapDepth(float raw, float d)
{
    return IsSky(raw) ? d * 4.0 : min(LinearEyeDepth(raw, _ZBufferParams), d * 4.0);
}

float RimEyeDepth(float raw)
{
    return LinearEyeDepth(raw, _ZBufferParams);
}

// Plane-aware silhouette measure for one radius: how much farther the far side is than the near side would predict
// for a plane through the centre, relative to depth. Also reports how "thin" the pixel is (both sides far).
float RimEdge(float dC, float dA, float dB, float dC2, float dD2, out float thin)
{
    float fx = max(dA, dB) - dC;
    float bx = max(0.0, dC - min(dA, dB));
    float fy = max(dC2, dD2) - dC;
    float by = max(0.0, dC - min(dC2, dD2));
    float disc = max(max(0.0, fx - bx), max(0.0, fy - by)) / dC;
    thin = max(min(dA, dB) - dC, min(dC2, dD2) - dC) / dC;
    return smoothstep(0.06, 0.4, disc);
}

float RimAmount(float2 uv)
{
    float raw = SampleSceneDepth(uv);
    if (IsSky(raw))
    {
        return 0.0;
    }

    float d = RimEyeDepth(raw);
    float fade = 1.0 - smoothstep(RimFadeStart, RimFadeEnd, d);
    if (fade <= 0.0)
    {
        return 0.0;
    }

    float2 o = _RimTexel.xy * _RimWidth;
    float rawR = SampleSceneDepth(uv + float2(o.x, 0));
    float rawL = SampleSceneDepth(uv - float2(o.x, 0));
    float rawU = SampleSceneDepth(uv + float2(0, o.y));
    float rawD = SampleSceneDepth(uv - float2(0, o.y));
    float dR = RimTapDepth(rawR, d);
    float dL = RimTapDepth(rawL, d);
    float dU = RimTapDepth(rawU, d);
    float dD = RimTapDepth(rawD, d);

    float thin;
    float edge = RimEdge(d, dR, dL, dU, dD, thin);
    if (_RimSimple < 0.5)
    {
        // Second, wider radius softens the band so it falls off instead of ending in a hard line.
        float2 o2 = o * 2.4;
        float thin2;
        float e2 = RimEdge(d,
            RimTapDepth(SampleSceneDepth(uv + float2(o2.x, 0)), d), RimTapDepth(SampleSceneDepth(uv - float2(o2.x, 0)), d),
            RimTapDepth(SampleSceneDepth(uv + float2(0, o2.y)), d), RimTapDepth(SampleSceneDepth(uv - float2(0, o2.y)), d), thin2);
        edge = edge * 0.6 + e2 * 0.4;
    }

    // Isolated thin geometry (leaf cards, grass blades) has far depth on both sides; it must not light up.
    edge *= 1.0 - smoothstep(0.05, 0.25, thin);

    // Normal from the smaller depth step on each axis, so a silhouette does not smear the normal.
    float3 p = RimWorldPos(uv, raw);
    bool useR = abs(dR - d) < abs(dL - d);
    float3 px = useR ? RimWorldPos(uv + float2(o.x, 0), rawR) - p : p - RimWorldPos(uv - float2(o.x, 0), rawL);
    bool useU = abs(dU - d) < abs(dD - d);
    float3 py = useU ? RimWorldPos(uv + float2(0, o.y), rawU) - p : p - RimWorldPos(uv - float2(0, o.y), rawD);

    float3 n = normalize(cross(px, py));
    float3 toCam = _WorldSpaceCameraPos - p;
    if (dot(n, toCam) < 0.0)
    {
        n = -n;
    }

    // Normals are only trusted on smooth surfaces: a large second difference means foliage or a silhouette.
    float curvature = max(abs(dR + dL - 2.0 * d), abs(dU + dD - 2.0 * d)) / d;
    float confidence = 1.0 - smoothstep(0.02, 0.08, curvature);

    // Moonlight leads: slopes that face the moon get the rim, and edges mostly appear on the moon side.
    float moonDot = dot(n, normalize(_LKMoonDir.xyz));
    float facing = smoothstep(0.55, 0.95, moonDot) * confidence;
    facing *= 1.0 - smoothstep(0.85, 1.0, n.y);
    edge *= lerp(0.15, 1.0, saturate(moonDot + 0.25));

    return (facing * 0.3 + edge * _RimEdgeWeight) * fade * _RimGain;
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
