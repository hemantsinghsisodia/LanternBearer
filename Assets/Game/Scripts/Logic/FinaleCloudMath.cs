using UnityEngine;

namespace LanternKeeper
{
// The timing and strengths of the finale's cloud banks and god rays (FinaleClouds). Everything is derived from FinaleTimeline phases, never from
// fixed seconds, so a longer finale just stretches it.
public static class FinaleCloudMath
{
    // The storm ring and the cloud banks cross-fade over this long from the start of the break.
    public const float CrossFadeSeconds = 2f;
    public const int FullLayers = 3;
    public const int LowLayers = 1;
    // Peak god ray strength (additive alpha) before the Reduce flashing cap.
    public const float RayPeak = 0.6f;
    // The gap narrows a little at the sunrise so the banks flank the sun: 1 .. SunriseGapScale.
    public const float SunriseGapScale = 0.7f;

    public static int LayerCount(bool low)
    {
        return low ? LowLayers : FullLayers;
    }

    public static bool RaysEnabled(bool low)
    {
        return !low;
    }

    // 0 before the break starts, 1 once the banks have fully replaced the storm ring.
    public static float CrossFade(float t)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - FinaleTimeline.BeamStart) / CrossFadeSeconds));
    }

    // The banks drifting apart: 0 closed .. 1 fully parted (the end of the beam and storm phase).
    public static float Break01(float t)
    {
        return Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.BeamAndStorm));
    }

    // The sunrise light on the clouds: 0 .. 1 over the sunrise phase.
    public static float Dawn01(float t)
    {
        return Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise));
    }

    public static float GapScale(float t)
    {
        return Mathf.Lerp(1f, SunriseGapScale, Dawn01(t));
    }

    // The god rays: only in the sunrise, never on Low, and never stronger than the Reduce flashing cap allows.
    public static float RayStrength(float t, bool reduceFlashing, bool low)
    {
        if (!RaysEnabled(low))
        {
            return 0f;
        }

        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise)));
        return k * RayPeak * SettingsMath.FlashCap(reduceFlashing);
    }

    // The gap centre (bearing 0..1): the beam bearing at the start of the break, swinging the short way round to where the camera looks through the
    // break (so the window is always in the middle of the view, with banks on both sides), which pans to the dawn, then settling on the sun's own bearing through the sunrise.
    public static float GapBearing(float beamBearing01, float lookBearing01, float sunBearing01, float t)
    {
        float toLook = Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.BeamAndStorm));
        float toSun = Mathf.SmoothStep(0f, 1f, FinaleTimeline.Phase01(t, FinalePhase.Sunrise));
        float a = Round(beamBearing01, lookBearing01, toLook);
        return Round(a, sunBearing01, toSun);
    }

    static float Round(float from, float to, float k)
    {
        float delta = Mathf.Repeat(to - from + 0.5f, 1f) - 0.5f;
        return Mathf.Repeat(from + delta * k, 1f);
    }
}
}
