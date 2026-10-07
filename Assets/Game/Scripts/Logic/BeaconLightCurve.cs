using UnityEngine;

namespace LanternKeeper
{
// Timing curves for a beacon lighting up. t is seconds since Beacon.Lit fired. Pure functions, visual only.
public static class BeaconLightCurve
{
    public const float Duration = 1.5f;

    // Short bright pop: rises to 1 at 0.1 s, gone by 0.3 s.
    public static float Flare(float t)
    {
        if (t <= 0f || t >= 0.3f)
        {
            return 0f;
        }

        if (t <= 0.1f)
        {
            return Smooth(t / 0.1f);
        }

        return 1f - Smooth((t - 0.1f) / 0.2f);
    }

    // Smooth 0 -> 1 over the first second, then held.
    public static float Intensity01(float t)
    {
        return Smooth(t);
    }

    // Safe-radius ring: nothing before 0.3 s, ease-out to maxRadius by 1.2 s.
    public static float RingRadius(float t, float maxRadius)
    {
        if (t <= 0.3f)
        {
            return 0f;
        }

        float u = Mathf.Clamp01((t - 0.3f) / 0.9f);
        float inv = 1f - u;
        return maxRadius * (1f - inv * inv * inv);
    }

    // Ember emission strength, only inside 0.1..1.5 s.
    public static float EmberRate01(float t)
    {
        if (t <= 0.1f || t >= Duration)
        {
            return 0f;
        }

        float u = (t - 0.1f) / (Duration - 0.1f);
        return 0.3f + 0.7f * Mathf.Sin(Mathf.PI * u);
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
}
