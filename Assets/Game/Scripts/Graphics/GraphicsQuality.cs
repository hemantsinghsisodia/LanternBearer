using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
public static class GraphicsQuality
{
    public const string PrefsKey = "LanternKeeperGraphics";
    public const string VSyncKey = "LanternKeeperVSync";
    const string QualityArgument = "-lkquality";

    public static event Action<GraphicsProfile> QualityChanged;

    static GraphicsProfileSet profiles;
    static bool commandLineChecked;
    static bool commandLineOverride;
    static GraphicsLevel commandLineLevel;
    static bool missingLogged;

    // Mobile device or touch browser at startup. Fixed for the session so the default does not flip when the player switches input.
    static bool IsMobile
    {
        get { return Application.isMobilePlatform || TouchMode.StartedAsTouch; }
    }

    public static GraphicsLevel Current
    {
        get
        {
            EnsureCommandLine();
            if (commandLineOverride)
            {
                return commandLineLevel;
            }

            int value = PlayerPrefs.GetInt(PrefsKey, GraphicsDefaults.For(Application.platform, IsMobile));
            if (value < 0 || value > (int)GraphicsLevel.Ultra)
            {
                return GraphicsLevel.Medium;
            }

            return (GraphicsLevel)value;
        }
    }

    public static GraphicsProfile Profile
    {
        get
        {
            GraphicsProfileSet set = Profiles;
            if (set == null)
            {
                return null;
            }

            return set.Get(Current);
        }
    }

    // Today's quality levels use vSyncCount 0, so the default is off until the player turns it on.
    public static bool VSync
    {
        get { return PlayerPrefs.GetInt(VSyncKey, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyVSync();
        }
    }

    public static void Set(GraphicsLevel level)
    {
        commandLineOverride = false;
        level = Clamp(level);
        PlayerPrefs.SetInt(PrefsKey, (int)level);
        PlayerPrefs.Save();
        Apply(level, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        EnsureCommandLine();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        UserSettings.Changed -= ApplyRenderScale;
        UserSettings.Changed += ApplyRenderScale;
        Application.quitting -= RestorePresetScales;
        Application.quitting += RestorePresetScales;
        Apply(Current, true);
    }

    // Effective render scale of the active preset: preset scale x the player's Render scale setting, clamped to 0.5..2.
    public static float EffectiveRenderScale
    {
        get
        {
            GraphicsProfile profile = Profile;
            return SettingsMath.EffectiveRenderScale(profile != null ? profile.renderScale : 1f, UserSettings.RenderScale);
        }
    }

    // Writes the effective scale to the active URP asset (in memory; the .asset on disk is never saved with it, see RestorePresetScales).
    public static void ApplyRenderScale()
    {
        UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null || Profile == null)
        {
            return;
        }

        float wanted = EffectiveRenderScale;
        if (!Mathf.Approximately(asset.renderScale, wanted))
        {
            asset.renderScale = wanted;
        }
    }

    // Puts every quality level's pipeline asset back to its preset's own scale, so the editor never keeps a supersampled
    // value in a .asset that a later save could write to disk.
    public static void RestorePresetScales()
    {
        GraphicsProfileSet set = Profiles;
        if (set == null)
        {
            return;
        }

        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            UniversalRenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
            GraphicsProfile profile = set.Get(LevelOfIndex(i));
            if (asset != null && profile != null && !Mathf.Approximately(asset.renderScale, profile.renderScale))
            {
                asset.renderScale = profile.renderScale;
            }
        }
    }

    // The browser build has its own four quality levels ("Web Low" .. "Web Ultra"; see WebBuildSetup). On a player the other
    // platform's levels are stripped, so the index is the level itself; looking the level up by name also keeps the editor right,
    // where every level is listed.
    static string QualityName(GraphicsLevel level)
    {
        return (GraphicsDefaults.IsWeb(Application.platform) ? "Web " : "") + level;
    }

    static int IndexOfLevel(GraphicsLevel level)
    {
        string wanted = QualityName(level);
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == wanted)
            {
                return i;
            }
        }

        return (int)level;
    }

    static GraphicsLevel LevelOfIndex(int index)
    {
        string[] names = QualitySettings.names;
        if (index >= 0 && index < names.Length)
        {
            string name = names[index].StartsWith("Web ", StringComparison.Ordinal) ? names[index].Substring(4) : names[index];
            GraphicsLevel parsed;
            if (Enum.TryParse(name, out parsed))
            {
                return parsed;
            }
        }

        return Clamp(index);
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCamera();
        Raise();
    }

    static void Apply(GraphicsLevel level, bool raise)
    {
        int index = IndexOfLevel(Clamp(level));
        if (index >= 0 && index < QualitySettings.names.Length)
        {
            QualitySettings.SetQualityLevel(index, true);
        }

        ApplyVSync();
        ApplyRenderScale();
        ApplyCamera();
        if (raise)
        {
            Raise();
        }
    }

    static void ApplyVSync()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
    }

    static bool ApplyCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
        GraphicsProfile profile = Profile;
        if (data == null || profile == null)
        {
            return false;
        }

        data.antialiasing = profile.cameraAntialiasing;
        return true;
    }

    static void Raise()
    {
        GraphicsProfile profile = Profile;
        if (profile != null && QualityChanged != null)
        {
            QualityChanged(profile);
        }
    }

    static GraphicsProfileSet Profiles
    {
        get
        {
            if (profiles == null)
            {
                profiles = Resources.Load<GraphicsProfileSet>("GraphicsProfileSet");
            }

            if (profiles == null && !missingLogged)
            {
                missingLogged = true;
                Debug.LogError("GraphicsProfileSet is missing from a Resources folder.");
            }

            return profiles;
        }
    }

    static void EnsureCommandLine()
    {
        if (commandLineChecked)
        {
            return;
        }

        commandLineChecked = true;
#if UNITY_WEBGL
        string[] args = new string[0];
#else
        string[] args = Environment.GetCommandLineArgs();
#endif
        for (int i = 0; i < args.Length; i++)
        {
            string value = null;
            string arg = args[i];
            if (arg.StartsWith(QualityArgument + "=", StringComparison.Ordinal))
            {
                value = arg.Substring(QualityArgument.Length + 1);
            }
            else if (arg == QualityArgument && i + 1 < args.Length)
            {
                value = args[i + 1];
            }

            if (value == null)
            {
                continue;
            }

            int parsed;
            if (int.TryParse(value, out parsed))
            {
                commandLineOverride = true;
                commandLineLevel = Clamp(parsed);
            }
        }
    }

    static GraphicsLevel Clamp(GraphicsLevel level)
    {
        return Clamp((int)level);
    }

    static GraphicsLevel Clamp(int value)
    {
        if (value < 0 || value > (int)GraphicsLevel.Ultra)
        {
            return GraphicsLevel.Medium;
        }

        return (GraphicsLevel)value;
    }
}
}
