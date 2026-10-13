using UnityEngine;

namespace LanternKeeper
{
public enum FinalePhase { Ignition = 0, BeamAndStorm = 1, Islands = 2, Sunrise = 3, Hold = 4, Credits = 5 }

// Fixed timing of the Island 5 finale. Times are unscaled seconds from the moment the lamp is lit.
//   0-6 ignition, 6-18 beam and storm break, 18-42 island flyby (flares at 21, 27, 33, 39), 42-60 sunrise, 60-66 hold, 66 credits.
public static class FinaleTimeline
{
    public const float IgnitionStart = 0f, BeamStart = 6f, IslandsStart = 18f, SunriseStart = 42f, HoldStart = 60f, Total = 66f;
    public const float SunriseLength = 18f;
    const float FlareFirst = 21f, FlareSpacing = 6f;

    public static FinalePhase PhaseAt(float t)
    {
        if (t >= Total) return FinalePhase.Credits;
        if (t >= HoldStart) return FinalePhase.Hold;
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
            case FinalePhase.Hold: return HoldStart;
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
            case FinalePhase.Sunrise: return HoldStart;
            default: return Total;
        }
    }

    public static float Phase01(float t, FinalePhase p)
    {
        float s = Start(p), e = End(p);
        if (e <= s) return t >= s ? 1f : 0f;
        return Mathf.Clamp01((t - s) / (e - s));
    }
}
}
