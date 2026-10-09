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
    const string RenderScaleKey = Prefix + "RenderScale";

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
        set
        {
            float v = Mathf.Clamp01(value);
            if (v > MuteThreshold)
            {
                lastAudibleMusic = v;
            }
            SetFloat(MusicKey, v);
        }
    }

    // Music counts as muted at or below this volume. MusicVolume is the only mute state there is.
    public const float MuteThreshold = 0.0001f;
    private static float lastAudibleMusic = DefaultVolume;

    public static bool MusicMuted
    {
        get { return MusicVolume <= MuteThreshold; }
    }

    // The M key: mute music, or restore the last audible volume (default 0.8). Goes through MusicVolume so the
    // slider, the mixer and the storm sounds all follow.
    public static void ToggleMusicMute()
    {
        float current = MusicVolume;
        if (current > MuteThreshold)
        {
            lastAudibleMusic = current;
            MusicVolume = 0f;
        }
        else
        {
            MusicVolume = lastAudibleMusic > MuteThreshold ? lastAudibleMusic : DefaultVolume;
        }
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

    // Brightness, TextScale and ReduceFlashing are read every frame (lighting, HUD scaling, Lightning.CurrentFlash from dozens
    // of components on Island 4). PlayerPrefs reads cost about 15 microseconds each on Windows, so these three are read at most
    // once per frame in play mode. Setters and InvalidateCache() refresh them; edit mode always reads through.
    private static int hotFrame = -1;
    private static float hotBrightness;
    private static float hotTextScale;
    private static bool hotReduceFlashing;

    // Number of PlayerPrefs refreshes of the hot values (for tests).
    public static int HotReadCount { get; private set; }

    // Fast-enter-play-mode (no domain reload) keeps statics, and Time.frameCount restarts, so drop the cache on every play start.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DeferSave = false;
        PendingSave = false;
        TimeSource = () => Time.unscaledTime;
        LastDeferredChangeTime = 0f;
        SaveCount = 0;
        HotReadCount = 0;
        lastAudibleMusic = DefaultVolume;
        InvalidateCache();
    }

    public static void InvalidateCache()
    {
        hotFrame = -1;
    }

    private static void RefreshHot()
    {
        if (Application.isPlaying && hotFrame == Time.frameCount)
        {
            return;
        }
        hotFrame = Application.isPlaying ? Time.frameCount : -1;
        HotReadCount++;
        hotBrightness = PlayerPrefs.GetFloat(BrightnessKey, 0f);
        hotTextScale = Snap(PlayerPrefs.GetFloat(TextScaleKey, 1f));
        hotReduceFlashing = PlayerPrefs.GetInt(ReduceFlashingKey, 0) == 1;
    }

    public static float Brightness
    {
        get { RefreshHot(); return hotBrightness; }
        set { SetFloat(BrightnessKey, Mathf.Clamp(value, -1f, 1f)); }
    }

    public static float TextScale
    {
        get { RefreshHot(); return hotTextScale; }
        set { SetFloat(TextScaleKey, Snap(value)); }
    }

    public static bool ReduceFlashing
    {
        get { RefreshHot(); return hotReduceFlashing; }
        set { SetInt(ReduceFlashingKey, value ? 1 : 0); }
    }

    // Supersampling multiplier on top of the graphics preset's render scale: 1, 1.25, 1.5 or 2. Read only when it changes, so no hot cache.
    public static float RenderScale
    {
        get { return SettingsMath.SnapRenderScale(PlayerPrefs.GetFloat(RenderScaleKey, 1f)); }
        set { SetFloat(RenderScaleKey, SettingsMath.SnapRenderScale(value)); }
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

    // True once the player has chosen a resolution or window mode. Until then the game leaves the screen alone.
    public static bool HasSavedDisplay
    {
        get { return PlayerPrefs.HasKey(ResWidthKey) || PlayerPrefs.HasKey(WindowModeKey); }
    }

    // Stores the size and refresh rate together so listeners see one change, not three.
    public static void SetResolution(int width, int height, int refreshHz)
    {
        PlayerPrefs.SetInt(ResWidthKey, width);
        PlayerPrefs.SetInt(ResHeightKey, height);
        PlayerPrefs.SetInt(RefreshKey, refreshHz);
        Raise();
    }

    // Returns the saved resolution if the display supports it, otherwise the desktop resolution.
    // With a saved size but no exact refresh match, prefers the desktop's Hz if that size has it,
    // otherwise the highest Hz offered for that size.
    public static Resolution ResolveResolution(Resolution[] supported, Resolution desktop)
    {
        int w = ResolutionWidth;
        int h = ResolutionHeight;
        int hz = RefreshRateHz;
        int desktopHz = Mathf.RoundToInt((float)desktop.refreshRateRatio.value);
        Resolution best = desktop;
        bool haveSize = false;
        bool bestIsDesktopHz = false;
        double bestHz = -1.0;
        if (supported != null)
        {
            for (int i = 0; i < supported.Length; i++)
            {
                if (supported[i].width != w || supported[i].height != h)
                {
                    continue;
                }
                int candidateHz = Mathf.RoundToInt((float)supported[i].refreshRateRatio.value);
                if (hz > 0 && candidateHz == hz)
                {
                    return supported[i];
                }
                bool isDesktopHz = candidateHz == desktopHz;
                double value = supported[i].refreshRateRatio.value;
                bool better = !haveSize
                    || (isDesktopHz && !bestIsDesktopHz)
                    || (isDesktopHz == bestIsDesktopHz && value > bestHz);
                if (better)
                {
                    best = supported[i];
                    bestIsDesktopHz = isDesktopHz;
                    bestHz = value;
                    haveSize = true;
                }
            }
        }
        return haveSize ? best : desktop;
    }

    // Older builds stored a music mute flag. Carry it over once (as volume 0, if no volume was ever saved), then clear
    // it: MusicVolume is the only mute state, so the slider and the M key work from there.
    public static void MigrateLegacy()
    {
        if (PlayerPrefs.GetInt(LegacyMusicMutedKey, 0) != 1)
        {
            return;
        }
        bool neverSet = !PlayerPrefs.HasKey(MusicKey);
        PlayerPrefs.DeleteKey(LegacyMusicMutedKey);
        if (neverSet)
        {
            MusicVolume = 0f;
        }
        else
        {
            PlayerPrefs.Save();
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

    // Deferred saving, used by SliderRow while a slider is being dragged. While DeferSave is true the setters
    // still write PlayerPrefs and raise Changed (so effects apply live) but skip PlayerPrefs.Save().
    // Whoever turns it on is responsible for calling Flush() afterwards.
    public static bool DeferSave;

    public static bool PendingSave { get; private set; }

    // Idle debounce. One shared timestamp (not per row): it is set whenever a deferred change marks a save pending,
    // so any SliderRow's Update may call FlushIfIdle and only the last change on screen restarts the countdown.
    // TimeSource is a test hook; it defaults to unscaled time.
    public static Func<float> TimeSource = () => Time.unscaledTime;
    public static float LastDeferredChangeTime { get; private set; }

    // Number of real PlayerPrefs.Save() calls made by this class (for tests).
    public static int SaveCount { get; private set; }

    // Flushes a pending save once idleSeconds have passed since the last deferred change. Returns true if it saved.
    public static bool FlushIfIdle(float idleSeconds)
    {
        if (PendingSave && TimeSource() - LastDeferredChangeTime >= idleSeconds)
        {
            Flush();
            return true;
        }
        return false;
    }

    // Writes any deferred changes to disk. Does nothing if nothing is pending.
    public static void Flush()
    {
        if (PendingSave)
        {
            PendingSave = false;
            SaveCount++;
            PlayerPrefs.Save();
        }
    }

    static void Raise()
    {
        InvalidateCache();
        if (DeferSave)
        {
            PendingSave = true;
            LastDeferredChangeTime = TimeSource();
        }
        else
        {
            PendingSave = false;
            SaveCount++;
            PlayerPrefs.Save();
        }
        Action handler = Changed;
        if (handler != null)
        {
            handler();
        }
    }
}
}
