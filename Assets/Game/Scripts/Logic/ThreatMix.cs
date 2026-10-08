using System;

namespace LanternKeeper
{
// How much danger the music should express, 0..1. Pure maths: the MusicDirector feeds it the game state.
public static class ThreatMix
{
    public const float ShadeNear = 4f, ShadeFar = 12f, FuelStart = 0.25f, FuelFull = 0.05f, RiseTime = 1.5f, FallTime = 4f;

    public static float Target(bool mothsDraining, float nearestShadeDistance, float fuel01, bool inSafeRing)
    {
        if (inSafeRing)
        {
            return 0f;
        }

        if (mothsDraining)
        {
            return 1f;
        }

        float distance = float.IsNaN(nearestShadeDistance) ? float.PositiveInfinity : Math.Max(0f, nearestShadeDistance);
        float shade = Clamp01(1f - (distance - ShadeNear) / (ShadeFar - ShadeNear));
        // A NaN fuel counts as full: no threat.
        float fuel = float.IsNaN(fuel01) ? 1f : fuel01;
        float low = Clamp01((FuelStart - fuel) / (FuelStart - FuelFull));
        return Math.Max(shade, low);
    }

    public static float Step(float current, float target, float dt)
    {
        if (dt <= 0f)
        {
            return current;
        }

        float next = target > current ? current + dt / RiseTime : current - dt / FallTime;
        if (target > current ? next >= target - 1e-4f : next <= target + 1e-4f)
        {
            return target;
        }

        return next;
    }

    static float Clamp01(float value)
    {
        return value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
}
