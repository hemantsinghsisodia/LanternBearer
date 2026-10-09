using UnityEngine;

namespace LanternKeeper
{
// Pure maths behind the user settings (no PlayerPrefs, no scene access).
public static class SettingsMath
{
    // The PlayerPrefs switch for the FPS readout. GraphicsMenu (sets it) and Hud/FpsReadout (reads it) both use this one key.
    public const string FpsPrefsKey = "LanternKeeperFps";

    public static readonly float[] TextScales = { 1f, 1.15f, 1.3f };

    // Render scale (supersampling) choices, multiplied onto the graphics preset's own scale.
    public static readonly float[] RenderScales = { 1f, 1.25f, 1.5f, 2f };
    public const float MinEffectiveRenderScale = 0.5f;
    public const float MaxEffectiveRenderScale = 2f;

    public static int RenderScaleIndex(float scale)
    {
        int best = 0;
        for (int i = 1; i < RenderScales.Length; i++)
        {
            if (Mathf.Abs(RenderScales[i] - scale) < Mathf.Abs(RenderScales[best] - scale))
            {
                best = i;
            }
        }
        return best;
    }

    public static float SnapRenderScale(float scale)
    {
        return RenderScales[RenderScaleIndex(scale)];
    }

    public static string RenderScaleLabel(float scale)
    {
        float snapped = SnapRenderScale(scale);
        string text = Mathf.RoundToInt(snapped * 100f) + "%";
        return snapped >= 2f ? text + " (4K at 1080p)" : text;
    }

    public static string[] RenderScaleLabels()
    {
        string[] labels = new string[RenderScales.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = RenderScaleLabel(RenderScales[i]);
        }
        return labels;
    }

    // What the pipeline asset gets: the preset's scale times the player's, kept within 0.5..2.
    public static float EffectiveRenderScale(float presetScale, float userScale)
    {
        return Mathf.Clamp(presetScale * userScale, MinEffectiveRenderScale, MaxEffectiveRenderScale);
    }

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
