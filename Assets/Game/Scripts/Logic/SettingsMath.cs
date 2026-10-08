using UnityEngine;

namespace LanternKeeper
{
// Pure maths behind the user settings (no PlayerPrefs, no scene access).
public static class SettingsMath
{
    // The PlayerPrefs switch for the FPS readout. GraphicsMenu (sets it) and Hud/FpsReadout (reads it) both use this one key.
    public const string FpsPrefsKey = "LanternKeeperFps";

    public static readonly float[] TextScales = { 1f, 1.15f, 1.3f };

    public static float VolumeToDb(float linear)
    {
        return linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
    }

    // Brightness -1..+1 maps to +-1 EV.
    public static float BrightnessToEv(float brightness)
    {
        return Mathf.Clamp(brightness, -1f, 1f);
    }

    public static float FlashCap(bool reduce)
    {
        return reduce ? 0.35f : 1f;
    }

    // Seconds between moth drain pulses on the screen edge: once a second, every other second with Reduce flashing.
    public static float MothPulsePeriod(bool reduce)
    {
        return reduce ? 2f : 1f;
    }

    public static float LowFuelPulseRate(float rate, bool reduce)
    {
        return reduce ? rate * 0.5f : rate;
    }
}
}
