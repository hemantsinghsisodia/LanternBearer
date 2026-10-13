using UnityEngine;

namespace LanternKeeper
{
public enum FinalePhase { Ignition = 0, BeamAndStorm = 1, Islands = 2, Sunrise = 3, Credits = 4 }

// Fixed timing of the Island 5 finale. Times are unscaled seconds from the moment the lamp is lit.
public static class FinaleTimeline
{
    public const float IgnitionStart = 0f, BeamStart = 6f, IslandsStart = 18f, SunriseStart = 36f, Total = 52f;
    public const float SunriseLength = 16f;
    public const float CameraStartHeight = 6f, CameraTopHeight = 50f;
    const float FlareFirst = 20f, FlareSpacing = 4f;

    public static FinalePhase PhaseAt(float t)
    {
        if (t >= Total) return FinalePhase.Credits;
        if (t >= SunriseStart) return FinalePhase.Sunrise;
        if (t >= IslandsStart) return FinalePhase.Islands;
        if (t >= BeamStart) return FinalePhase.BeamAndStorm;
        return FinalePhase.Ignition;
    }

    // island 0..3 = Islands 1..4.
    public static float IslandFlareTime(int island)
    {
        return FlareFirst + FlareSpacing * Mathf.Clamp(island, 0, 3);
    }

    static float Start(FinalePhase p)
    {
        switch (p)
        {
            case FinalePhase.Ignition: return IgnitionStart;
            case FinalePhase.BeamAndStorm: return BeamStart;
            case FinalePhase.Islands: return IslandsStart;
            case FinalePhase.Sunrise: return SunriseStart;
            default: return Total;
        }
    }

    static float End(FinalePhase p)
    {
        switch (p)
        {
            case FinalePhase.Ignition: return BeamStart;
            case FinalePhase.BeamAndStorm: return IslandsStart;
            case FinalePhase.Islands: return SunriseStart;
            default: return Total;
        }
    }

    public static float Phase01(float t, FinalePhase p)
    {
        float s = Start(p), e = End(p);
        if (e <= s) return t >= s ? 1f : 0f;
        return Mathf.Clamp01((t - s) / (e - s));
    }

    public static float CameraHeight(float t)
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - IgnitionStart) / (IslandsStart - IgnitionStart)));
        return Mathf.Lerp(CameraStartHeight, CameraTopHeight, k);
    }
}
}
