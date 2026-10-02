using UnityEngine;

namespace LanternKeeper
{
// Pure smoothing step for the HUD fuel gauge. Always returns the value to display, so the
// caller can write it whenever it differs from what is on screen (slow drain included).
public static class FuelGauge
{
    public static float Step(float displayed, float target, ref float velocity, float smoothTime, float dt, float snapEpsilon)
    {
        if (dt <= 0f)
        {
            return displayed;
        }

        float smoothed = Mathf.SmoothDamp(displayed, target, ref velocity, smoothTime, Mathf.Infinity, dt);
        if (Mathf.Abs(target - smoothed) <= snapEpsilon)
        {
            velocity = 0f;
            return target;
        }

        return smoothed;
    }
}
}
