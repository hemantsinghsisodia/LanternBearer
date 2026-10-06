using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Applies the player's settings to the running game: mixer volumes, screen resolution and window mode.
// Lives under Systems in every scene; the first instance survives scene loads and later duplicates remove themselves.
// Brightness is published as UserEv, which the look volumes and Lightning add to their exposure.
public class UserSettingsApplier : MonoBehaviour
{
    public const string MasterParam = "MasterVol";
    public const string MusicParam = "MusicVol";
    public const string SfxParam = "SfxVol";
    public const string AmbienceParam = "AmbienceVol";

    [SerializeField] private AudioMixer mixer;

    // The Ducked snapshot lowers music by 8 dB. Once a script sets MusicVol the snapshot no longer drives it,
    // so the duck is applied here, on top of the user's volume.
    public const float DuckDb = -8f;
    private const float DuckDbPerSecond = DuckDb / -0.35f;

    private bool ducked;
    private float duckOffset;
    // Brightness without post-processing: scale the ambient light and the moon by 2^EV from cached authored values.
    // Whenever another script rewrites a value (LookApplier on a preset change, a scene load) the cache is retaken from it.
    private Light moon;
    private bool haveMoon;
    private bool searchedDirectional;
    private float moonAuthored;
    private float moonWritten;
    private bool haveAmbient;
    private float ambientIntensityAuthored;
    private float ambientIntensityWritten;
    private Color[] ambientAuthored = new Color[3];
    private Color[] ambientWritten = new Color[3];

    private bool haveAppliedScreen;
    private int appliedWidth;
    private int appliedHeight;
    private int appliedHz;
    private FullScreenMode appliedMode;

    public static UserSettingsApplier Instance { get; private set; }

    public static float CurrentDuckOffset { get { return Instance != null ? Instance.duckOffset : 0f; } }

    public static void SetDucked(bool on)
    {
        if (Instance != null)
        {
            Instance.ducked = on;
        }
    }

    public static AudioMixer Mixer { get { return Instance != null ? Instance.ResolveMixer() : null; } }

    // Exposure offset in EV from the Brightness setting (-1..+1).
    public static float UserEv { get { return SettingsMath.BrightnessToEv(UserSettings.Brightness); } }

    // The last screen request, for tests and diagnostics. Set even in the editor, where nothing is actually resized.
    public static int ScreenApplyCount { get; private set; }
    public static Resolution LastRequestedResolution { get; private set; }
    public static FullScreenMode LastRequestedMode { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }
        DontDestroyOnLoad(gameObject);
        UserSettings.MigrateLegacy();
    }

    private void OnEnable()
    {
        if (Instance != this)
        {
            return;
        }
        UserSettings.Changed += Apply;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    private void Start()
    {
        if (Instance == this)
        {
            Apply();
        }
    }

    private void Update()
    {
        float target = ducked ? DuckDb : 0f;
        if (!Mathf.Approximately(duckOffset, target))
        {
            duckOffset = Mathf.MoveTowards(duckOffset, target, DuckDbPerSecond * Time.unscaledDeltaTime);
            ApplyAudio();
        }
    }

    private void LateUpdate()
    {
        ApplyLightBrightness();
    }

    private static bool Same(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.0001f;
    }

    private void ApplyLightBrightness()
    {
        float factor = LookVolumeQuality.ExposureActive ? 1f : Mathf.Pow(2f, UserEv);

        Light sun = RenderSettings.sun;
        if (sun == null && moon == null && !searchedDirectional)
        {
            searchedDirectional = true;
            sun = FindDirectional();
        }
        if (sun != null && sun != moon)
        {
            moon = sun;
            haveMoon = false;
        }
        if (moon != null)
        {
            if (!haveMoon || !Mathf.Approximately(moon.intensity, moonWritten))
            {
                moonAuthored = moon.intensity;
                haveMoon = true;
            }
            moonWritten = moonAuthored * factor;
            moon.intensity = moonWritten;
        }

        Color[] now = { RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor };
        bool changed = !haveAmbient || !Mathf.Approximately(RenderSettings.ambientIntensity, ambientIntensityWritten);
        for (int i = 0; i < 3 && !changed; i++)
        {
            changed = !Same(now[i], ambientWritten[i]);
        }
        if (changed)
        {
            ambientIntensityAuthored = RenderSettings.ambientIntensity;
            for (int i = 0; i < 3; i++)
            {
                ambientAuthored[i] = now[i];
            }
            haveAmbient = true;
        }
        // Trilight and flat ambient read the colours, skybox ambient reads the intensity: scale both.
        ambientIntensityWritten = ambientIntensityAuthored * factor;
        RenderSettings.ambientIntensity = ambientIntensityWritten;
        ambientWritten[0] = ambientAuthored[0] * factor;
        ambientWritten[1] = ambientAuthored[1] * factor;
        ambientWritten[2] = ambientAuthored[2] * factor;
        RenderSettings.ambientSkyColor = ambientWritten[0];
        RenderSettings.ambientEquatorColor = ambientWritten[1];
        RenderSettings.ambientGroundColor = ambientWritten[2];
    }

    private static Light FindDirectional()
    {
        Light[] lights = FindObjectsByType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional && lights[i].enabled)
            {
                return lights[i];
            }
        }
        return null;
    }

    private void OnDisable()
    {
        UserSettings.Changed -= Apply;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A duck belongs to the scene it started in (win, loss, retry, back to the menu).
        ducked = false;
        duckOffset = 0f;
        haveMoon = false;
        haveAmbient = false;
        searchedDirectional = false;
        moon = null;
        ApplyAudio();
    }

    public void Apply()
    {
        ApplyAudio();
        ApplyScreen();
    }

    private AudioMixer ResolveMixer()
    {
        if (mixer == null && AudioManager.Instance != null)
        {
            mixer = AudioManager.Instance.Mixer;
        }
        return mixer;
    }

    private void ApplyAudio()
    {
        AudioMixer target = ResolveMixer();
        if (target == null)
        {
            return;
        }
        target.SetFloat(MasterParam, SettingsMath.VolumeToDb(UserSettings.MasterVolume));
        target.SetFloat(MusicParam, SettingsMath.VolumeToDb(UserSettings.MusicVolume) + duckOffset);
        target.SetFloat(SfxParam, SettingsMath.VolumeToDb(UserSettings.EffectsVolume));
        target.SetFloat(AmbienceParam, SettingsMath.VolumeToDb(UserSettings.AmbienceVolume));
    }

    // Exclusive fullscreen exists on Windows and macOS only.
    private static bool ExclusiveSupported()
    {
        RuntimePlatform p = Application.platform;
        return p == RuntimePlatform.WindowsPlayer || p == RuntimePlatform.WindowsEditor
            || p == RuntimePlatform.OSXPlayer || p == RuntimePlatform.OSXEditor;
    }

    // Calls Screen.SetResolution only when the wanted size, refresh rate or window mode differs from the last request.
    private void ApplyScreen()
    {
        // Until the player has chosen a size or window mode the game leaves the screen alone.
        if (!UserSettings.HasSavedDisplay)
        {
            return;
        }
        Resolution desktop = Screen.currentResolution;
        Resolution wanted = UserSettings.ResolveResolution(Screen.resolutions, desktop);
        FullScreenMode mode = UserSettings.WindowMode;
        if (mode == FullScreenMode.ExclusiveFullScreen && !ExclusiveSupported())
        {
            mode = FullScreenMode.FullScreenWindow;
        }
        int hz = Mathf.RoundToInt((float)wanted.refreshRateRatio.value);
        if (haveAppliedScreen && appliedWidth == wanted.width && appliedHeight == wanted.height && appliedHz == hz && appliedMode == mode)
        {
            return;
        }
        haveAppliedScreen = true;
        appliedWidth = wanted.width;
        appliedHeight = wanted.height;
        appliedHz = hz;
        appliedMode = mode;
        if (wanted.width <= 0 || wanted.height <= 0)
        {
            return;
        }
        ScreenApplyCount++;
        LastRequestedResolution = wanted;
        LastRequestedMode = mode;
        // The editor's Game view is not a real window, so only a player build resizes.
        if (!Application.isEditor)
        {
            Screen.SetResolution(wanted.width, wanted.height, mode, wanted.refreshRateRatio);
        }
    }
}
}
