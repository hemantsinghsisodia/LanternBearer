using UnityEngine;

namespace LanternKeeper
{
// Wing flap and glide timing for moths. Deterministic per phase, visual only.
public static class MothFlap
{
    public const float MinHz = 14f;
    public const float MaxHz = 20f;
    public const float MaxAngle = 55f;

    const float GlideLength = 1f;
    const float GlideBlend = 0.15f;

    // Wing angle in degrees, within [-55, 55]. glide01 = 1 folds the flap to a small tremble.
    public static float Angle(float time, float phase, float hz, float glide01)
    {
        float flap = Mathf.Sin(2f * Mathf.PI * hz * time + phase);
        float amp = 1f - 0.85f * Mathf.Clamp01(glide01);
        return Mathf.Clamp(MaxAngle * amp * flap, -MaxAngle, MaxAngle);
    }

    // About one second of glide every 3-6 s, with a period and offset taken from the phase.
    public static float GlideFactor(float time, float phase)
    {
        float period = 3f + 3f * Hash01(phase);
        float offset = period * Hash01(phase + 17.31f);
        float c = Mathf.Repeat(time + offset, period);
        if (c >= GlideLength)
        {
            return 0f;
        }

        float rise = Smooth(c / GlideBlend);
        float fall = 1f - Smooth((c - (GlideLength - GlideBlend)) / GlideBlend);
        return Mathf.Clamp01(Mathf.Min(rise, fall));
    }

    static float Hash01(float x)
    {
        float s = Mathf.Sin(x * 12.9898f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
}
