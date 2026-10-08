using UnityEngine;

namespace LanternKeeper
{
// Timing curves for firefly swarms: blink, stream into the lantern, linger and fade-in. Pure functions, visual only.
public static class FireflyCurve
{
    public const int Motes = 6, MotesLow = 4, Linger = 2, LingerLow = 1;
    public const float StreamTime = 0.5f, LingerEnd = 2.0f, FadeInTime = 1.0f;
    public const float ReduceFlashingFloor = 0.3f, LitThreshold = 0.5f;

    // Blink cycle length in seconds: 2.6 to 3.4, 1.3x slower on Low.
    public static float Period(float swarmSeed01, bool low)
    {
        float p = Mathf.Lerp(2.6f, 3.4f, Mathf.Clamp01(swarmSeed01));
        return low ? p * 1.3f : p;
    }

    // Cycle offset of a mote: evenly spread, with a small jitter.
    public static float MoteOffset(int index, int count, float jitter01)
    {
        return (float)index / Mathf.Max(1, count) + (jitter01 - 0.5f) * 0.06f;
    }

    // Brightness 0..1. Short rise, hold, short fall, then dark for the rest of the cycle.
    public static float Blink(float time, float period, float offset, int count, bool reduceFlashing)
    {
        float u = time / period + offset;
        u -= Mathf.Floor(u);

        float edge = reduceFlashing ? 0.2f : 0.1f;
        float hold = Mathf.Max(0f, 2.6f / Mathf.Max(1, count) - edge);
        float result;
        if (u < edge)
        {
            result = u / edge;
        }
        else if (u < edge + hold)
        {
            result = 1f;
        }
        else if (u < edge + hold + edge)
        {
            result = 1f - (u - edge - hold) / edge;
        }
        else
        {
            result = 0f;
        }

        return reduceFlashing ? Mathf.Max(result, ReduceFlashingFloor) : result;
    }

    // How many motes are lit right now (Blink at or above LitThreshold).
    public static int LitCount(float time, float period, float swarmSeed01, int count, bool reduceFlashing)
    {
        int lit = 0;
        for (int i = 0; i < count; i++)
        {
            float offset = MoteOffset(i, count, MoteJitter01(swarmSeed01, i));
            if (Blink(time, period, offset, count, reduceFlashing) >= LitThreshold)
            {
                lit++;
            }
        }

        return lit;
    }

    // Eased progress of the stream into the lantern.
    public static float Stream01(float sinceCollect)
    {
        return Smooth(sinceCollect / StreamTime);
    }

    // Quadratic arc from start to target, bowed sideways and a little up.
    public static Vector3 StreamPoint(Vector3 start, Vector3 target, float sideSign, float t01)
    {
        t01 = Mathf.Clamp01(t01);
        Vector3 d = target - start;
        Vector3 side = Vector3.Cross(d, Vector3.up);
        side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.right;
        Vector3 control = (start + target) * 0.5f + side * (sideSign * d.magnitude * 0.25f) + Vector3.up * 0.3f;
        float inv = 1f - t01;
        return inv * inv * start + 2f * inv * t01 * control + t01 * t01 * target;
    }

    // 1 until 0.7, eases to 0 at 1.
    public static float StreamScale(float t01)
    {
        if (t01 <= 0.7f)
        {
            return 1f;
        }

        return 1f - Smooth((t01 - 0.7f) / 0.3f);
    }

    // 1 until 1.5 s, eases to 0 at LingerEnd.
    public static float LingerAlpha(float sinceCollect)
    {
        if (sinceCollect <= 1.5f)
        {
            return 1f;
        }

        return 1f - Smooth((sinceCollect - 1.5f) / (LingerEnd - 1.5f));
    }

    // A lingering mote eases from 0 back to full size over 0.15 s after the stream arrives.
    public const float LingerGrowTime = 0.15f;

    public static float LingerScale(float sinceCollect)
    {
        return Smooth((sinceCollect - StreamTime) / LingerGrowTime);
    }

    // Horizontal orbit around the lantern, radius 0.3, about 2 rad/s.
    public static Vector3 LingerOffset(int lingerIndex, float sinceCollect)
    {
        float a = lingerIndex * Mathf.PI + 2f * sinceCollect;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.3f;
    }

    // Mote i starts at i * (0.8 / count) and fades in over 0.2 s.
    public static float FadeIn(int index, int count, float sinceRespawn)
    {
        float start = index * (0.8f / Mathf.Max(1, count));
        return Mathf.Clamp01((sinceRespawn - start) / 0.2f);
    }

    // Per-mote jitter 0..1 from the swarm seed. FireflySwarm, the shader mirror and LitCount all use this one hash.
    public static float MoteJitter01(float seed01, int index)
    {
        float v = Mathf.Sin(seed01 * 127.1f + index * 311.7f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    // Drift frequencies (Hz per axis, 0.2 to 0.5) of a mote. Computed once on the CPU and pushed to the shader as _DriftFreq,
    // so the GPU never evaluates the hash (GPU sin can differ from Mathf.Sin).
    public static Vector3 DriftFreq(float seed01, int index)
    {
        float k = seed01 * 91.7f + index * 13.3f;
        return new Vector3(0.2f + 0.3f * Hash11(k + 1f), 0.2f + 0.3f * Hash11(k + 2f), 0.2f + 0.3f * Hash11(k + 3f));
    }

    // Drift phases (radians per axis) of a mote, pushed to the shader as _DriftPhase.
    public static Vector3 DriftPhase(float seed01, int index)
    {
        float k = seed01 * 91.7f + index * 13.3f;
        return new Vector3(6.2831853f * Hash11(k + 4f), 6.2831853f * Hash11(k + 5f), 6.2831853f * Hash11(k + 6f));
    }

    // CPU mirror of the FireflyMote shader drift: a Lissajous offset (world space, metres) added to the mote's own position.
    // The shader uses the same DriftFreq and DriftPhase values (per-instance _DriftFreq and _DriftPhase), with time = _Time.y.
    public static Vector3 Drift(float seed01, int index, float time, float stream)
    {
        return Drift(DriftFreq(seed01, index), DriftPhase(seed01, index), time, stream);
    }

    public static Vector3 Drift(Vector3 freq, Vector3 phase, float time, float stream)
    {
        float gone = 1f - stream;
        return new Vector3(
            Mathf.Sin(time * 6.2831853f * freq.x + phase.x) * 0.45f,
            Mathf.Sin(time * 6.2831853f * freq.y + phase.y) * 0.45f * 0.6f,
            Mathf.Sin(time * 6.2831853f * freq.z + phase.z) * 0.45f) * gone;
    }

    static float Hash11(float x)
    {
        float v = Mathf.Sin(x * 127.1f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
}
