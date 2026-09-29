#ifndef LK_REVEAL_DITHER_INCLUDED
#define LK_REVEAL_DITHER_INCLUDED

// Must match LanternKeeper.RevealMath: reveal = 1 - smoothstep(radius * inner, radius, distance).
float4 _LKLanternPos;
float _LKLanternRadius;
float _LKRevealInner;

float LKRevealAt(float3 pointWS)
{
    float radius = _LKLanternRadius;
    if (radius < 0.2)
    {
        return 0.0;
    }

    float inner = radius * _LKRevealInner;
    float dist = distance(pointWS, _LKLanternPos.xyz);
    return 1.0 - smoothstep(inner, max(radius, inner + 1e-4), dist);
}

float LKBayer4(float2 pixel)
{
    int x = int(pixel.x) & 3;
    int y = int(pixel.y) & 3;
    const float4x4 bayer = float4x4(
        0, 8, 2, 10,
        12, 4, 14, 6,
        3, 11, 1, 9,
        15, 7, 13, 5);
    return bayer[y][x] * (1.0 / 16.0);
}

float3 LKPivotWS()
{
    return float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
}

// Screen-space dither. Returns 1 at the dissolve edge and 0 when the pixel is solidly inside the reveal.
float LKDissolveRim(float2 pixel)
{
    float reveal = LKRevealAt(LKPivotWS());
    float gap = reveal - LKBayer4(pixel);
    clip(gap - 0.001);
    return saturate(1.0 - gap * 12.0);
}

#endif
