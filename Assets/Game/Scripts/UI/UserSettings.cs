using System;
using UnityEngine;

namespace LanternKeeper
{
// Player-facing settings, persisted in PlayerPrefs. Every setter raises Changed.
public static class UserSettings
{
    const string Prefix = "LanternKeeper";
    const string MasterKey = Prefix + "MasterVolume";
    const string MusicKey = Prefix + "MusicVolume";
    const string EffectsKey = Prefix + "EffectsVolume";
    const string AmbienceKey = Prefix + "AmbienceVolume";
    const string BrightnessKey = Prefix + "Brightness";
    const string TextScaleKey = Prefix + "TextScale";
    const string ReduceFlashingKey = Prefix + "ReduceFlashing";
    const string WindowModeKey = Prefix + "WindowMode";
    const string ResWidthKey = Prefix + "ResWidth";
    const string ResHeightKey = Prefix + "ResHeight";
    const string RefreshKey = Prefix + "RefreshHz";

    // Copied from MusicPlayer.MutePref (Assembly-CSharp, not referenceable from here).
    const string LegacyMusicMutedKey = "LanternKeeperMusicMuted";

    const float DefaultVolume = 0.8f;

    public static event Action Changed;

    public static float MasterVolume
    {
        get { return PlayerPrefs.GetFloat(MasterKey, DefaultVolume); }
        set { SetFloat(MasterKey, Mathf.Clamp01(value)); }
    }

    public static float MusicVolume
    {
        get { return PlayerPrefs.GetFloat(MusicKey, DefaultVolume); }
        set { SetFloat(MusicKey, Mathf.Clamp01(value)); }
    }

    public static float EffectsVolume
    {
        get { return PlayerPrefs.GetFloat(EffectsKey, DefaultVolume); }
        set { SetFloat(EffectsKey, Mathf.Clamp01(value)); }
    }

    public static float AmbienceVolume
    {
        get { return PlayerPrefs.GetFloat(AmbienceKey, DefaultVolume); }
        set { SetFloat(AmbienceKey, Mathf.Clamp01(value)); }
    }

    public static float Brightness
    {
        get { return PlayerPrefs.GetFloat(BrightnessKey, 0f); }
        set { SetFloat(BrightnessKey, Mathf.Clamp(value, -1f, 1f)); }
    }

    public static float TextScale
    {
        get { return Snap(PlayerPrefs.GetFloat(TextScaleKey, 1f)); }
        set { SetFloat(TextScaleKey, Snap(value)); }
    }

    public static bool ReduceFlashing
    {
        get { return PlayerPrefs.GetInt(ReduceFlashingKey, 0) == 1; }
        set { SetInt(ReduceFlashingKey, value ? 1 : 0); }
    }

    public static FullScreenMode WindowMode
    {
        get { return (FullScreenMode)PlayerPrefs.GetInt(WindowModeKey, (int)FullScreenMode.FullScreenWindow); }
        set { SetInt(WindowModeKey, (int)value); }
    }

    public static int ResolutionWidth
    {
        get { return PlayerPrefs.GetInt(ResWidthKey, 0); }
        set { SetInt(ResWidthKey, value); }
    }

    public static int ResolutionHeight
    {
        get { return PlayerPrefs.GetInt(ResHeightKey, 0); }
        set { SetInt(ResHeightKey, value); }
    }

    public static int RefreshRateHz
    {
        get { return PlayerPrefs.GetInt(RefreshKey, 0); }
        set { SetInt(RefreshKey, value); }
    }

    // Returns the saved resolution if the display supports it, otherwise the desktop resolution.
    public static Resolution ResolveResolution(Resolution[] supported, Resolution desktop)
    {
        int w = ResolutionWidth;
        int h = ResolutionHeight;
        int hz = RefreshRateHz;
        Resolution sizeMatch = desktop;
        bool haveSize = false;
        if (supported != null)
        {
            for (int i = 0; i < supported.Length; i++)
            {
                if (supported[i].width != w || supported[i].height != h)
                {
                    continue;
                }
                if (hz > 0 && Mathf.RoundToInt((float)supported[i].refreshRateRatio.value) == hz)
                {
                    return supported[i];
                }
                if (!haveSize)
                {
                    sizeMatch = supported[i];
                    haveSize = true;
                }
            }
        }
        return haveSize ? sizeMatch : desktop;
    }

    // Older builds stored a music mute flag. Carry it over once, if no volume was ever saved.
    public static void MigrateLegacy()
    {
        if (PlayerPrefs.GetInt(LegacyMusicMutedKey, 0) == 1 && !PlayerPrefs.HasKey(MusicKey))
        {
            MusicVolume = 0f;
        }
    }

    static float Snap(float value)
    {
        float[] scales = SettingsMath.TextScales;
        float best = scales[0];
        for (int i = 1; i < scales.Length; i++)
        {
            if (Mathf.Abs(scales[i] - value) < Mathf.Abs(best - value))
            {
                best = scales[i];
            }
        }
        return best;
    }

    static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        Raise();
    }

    static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        Raise();
    }

    static void Raise()
    {
        PlayerPrefs.Save();
        Action handler = Changed;
        if (handler != null)
        {
            handler();
        }
    }
}
}
