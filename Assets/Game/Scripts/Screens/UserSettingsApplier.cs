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
    // Each value is tracked on its own: it is re-cached as "authored" only when its own current value differs from what
    // this class last wrote (LookApplier on a preset change, DawnSequence every frame, a scene load). Fields nobody else
    // touches keep their authored value, so nothing is ever scaled twice.
    // DawnSequence reads its night baseline from the Authored* getters, so dawn fades authored night to authored dawn and
    // this class keeps scaling the result: brightness applies through dawn with no step at either end.
    private sealed class Tracked<T>
    {
        public bool Have;
        public T Authored;
        public T Written;
    }

    private Light moon;
    private bool searchedDirectional;
    private readonly Tracked<float> moonField = new Tracked<float>();
    private readonly Tracked<float> ambientIntensityField = new Tracked<float>();
    private readonly Tracked<Color> skyField = new Tracked<Color>();
    private readonly Tracked<Color> equatorField = new Tracked<Color>();
    private readonly Tracked<Color> groundField = new Tracked<Color>();

    private bool haveAppliedScreen;
    private int appliedWidth;
    private int appliedHeight;
    private int appliedHz;
    private FullScreenMode appliedMode;
    private int savedWidth;
    private int savedHeight;
    private int savedHz;
    private FullScreenMode savedModeSeen;

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
        MusicPlayer.ReloadMute();
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

    private const float Tolerance = 0.0001f;

    private static bool Same(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) <= Tolerance && Mathf.Abs(a.g - b.g) <= Tolerance && Mathf.Abs(a.b - b.b) <= Tolerance;
    }

    private static Color Scale(Color c, float f)
    {
        return new Color(c.r * f, c.g * f, c.b * f, c.a);
    }

    // Returns the value to write: authored x factor, re-caching authored first if someone else changed the value.
    private static float Track(Tracked<float> field, float current, float factor)
    {
        if (!field.Have || Mathf.Abs(current - field.Written) > Tolerance)
        {
            field.Authored = current;
            field.Have = true;
        }
        field.Written = field.Authored * factor;
        return field.Written;
    }

    private static Color Track(Tracked<Color> field, Color current, float factor)
    {
        if (!field.Have || !Same(current, field.Written))
        {
            field.Authored = current;
            field.Have = true;
        }
        field.Written = Scale(field.Authored, factor);
        return field.Written;
    }

    // The unscaled values, for scripts that fade lighting themselves (DawnSequence). Without a cached value they
    // return the live one.
    public static float AuthoredAmbientIntensity
    {
        get { return Instance != null && Instance.ambientIntensityField.Have ? Instance.ambientIntensityField.Authored : RenderSettings.ambientIntensity; }
    }

    public static Color AuthoredSky
    {
        get { return Instance != null && Instance.skyField.Have ? Instance.skyField.Authored : RenderSettings.ambientSkyColor; }
    }

    public static Color AuthoredEquator
    {
        get { return Instance != null && Instance.equatorField.Have ? Instance.equatorField.Authored : RenderSettings.ambientEquatorColor; }
    }

    public static Color AuthoredGround
    {
        get { return Instance != null && Instance.groundField.Have ? Instance.groundField.Authored : RenderSettings.ambientGroundColor; }
    }

    public static float AuthoredMoon(Light light)
    {
        if (light == null)
        {
            return 0f;
        }
        return Instance != null && Instance.moon == light && Instance.moonField.Have ? Instance.moonField.Authored : light.intensity;
    }

    // Number of RenderSettings / light assignments made by ApplyLightBrightness (for tests: zero in steady state).
    public static int LightWriteCount { get; private set; }

    private bool AnyScaled()
    {
        return Scaled(moonField) || Scaled(ambientIntensityField) || Scaled(skyField) || Scaled(equatorField) || Scaled(groundField);
    }

    private static bool Scaled(Tracked<float> f)
    {
        return f.Have && Mathf.Abs(f.Authored - f.Written) > Tolerance;
    }

    private static bool Scaled(Tracked<Color> f)
    {
        return f.Have && !Same(f.Authored, f.Written);
    }

    private void ApplyLightBrightness()
    {
        float factor = LookVolumeQuality.ExposureActive ? 1f : Mathf.Pow(2f, UserEv);

        // Nothing to scale and nothing left scaled: leave the lighting alone. Drop the cached authored values so the
        // Authored* getters return the live ones (DawnSequence and LookApplier own the lighting while unscaled).
        if (Mathf.Abs(factor - 1f) <= Tolerance && !AnyScaled())
        {
            moonField.Have = false;
            ambientIntensityField.Have = false;
            skyField.Have = false;
            equatorField.Have = false;
            groundField.Have = false;
            return;
        }

        Light sun = RenderSettings.sun;
        if (sun == null && moon == null && !searchedDirectional)
        {
            searchedDirectional = true;
            sun = FindDirectional();
        }
        if (sun != null && sun != moon)
        {
            moon = sun;
            moonField.Have = false;
        }
        if (moon != null)
        {
            float current = moon.intensity;
            float wanted = Track(moonField, current, factor);
            if (Mathf.Abs(wanted - current) > Tolerance)
            {
                moon.intensity = wanted;
                LightWriteCount++;
            }
        }

        float ambient = RenderSettings.ambientIntensity;
        float wantedAmbient = Track(ambientIntensityField, ambient, factor);
        if (Mathf.Abs(wantedAmbient - ambient) > Tolerance)
        {
            RenderSettings.ambientIntensity = wantedAmbient;
            LightWriteCount++;
        }

        Color sky = RenderSettings.ambientSkyColor;
        Color wantedSky = Track(skyField, sky, factor);
        if (!Same(wantedSky, sky))
        {
            RenderSettings.ambientSkyColor = wantedSky;
            LightWriteCount++;
        }

        Color equator = RenderSettings.ambientEquatorColor;
        Color wantedEquator = Track(equatorField, equator, factor);
        if (!Same(wantedEquator, equator))
        {
            RenderSettings.ambientEquatorColor = wantedEquator;
            LightWriteCount++;
        }

        Color ground = RenderSettings.ambientGroundColor;
        Color wantedGround = Track(groundField, ground, factor);
        if (!Same(wantedGround, ground))
        {
            RenderSettings.ambientGroundColor = wantedGround;
            LightWriteCount++;
        }
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
        moonField.Have = false;
        ambientIntensityField.Have = false;
        skyField.Have = false;
        equatorField.Have = false;
        groundField.Have = false;
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
        bool ok = target.SetFloat(MasterParam, SettingsMath.VolumeToDb(UserSettings.MasterVolume));
        ok &= target.SetFloat(MusicParam, SettingsMath.VolumeToDb(UserSettings.MusicVolume) + duckOffset);
        ok &= target.SetFloat(SfxParam, SettingsMath.VolumeToDb(UserSettings.EffectsVolume));
        ok &= target.SetFloat(AmbienceParam, SettingsMath.VolumeToDb(UserSettings.AmbienceVolume));
        if (!ok && !warnedMixerParam)
        {
            warnedMixerParam = true;
            Debug.LogWarning("UserSettingsApplier: the mixer has no exposed volume parameter for one of Master/Music/Sfx/Ambience; that setting will have no effect.", this);
        }
    }

    private bool warnedMixerParam;

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
        // Volume and brightness ticks come through here too. Skip Screen.resolutions (it allocates) when the saved
        // values are the ones already applied.
        FullScreenMode savedMode = UserSettings.WindowMode;
        if (haveAppliedScreen && savedWidth == UserSettings.ResolutionWidth && savedHeight == UserSettings.ResolutionHeight
            && savedHz == UserSettings.RefreshRateHz && savedMode == savedModeSeen)
        {
            return;
        }
        savedWidth = UserSettings.ResolutionWidth;
        savedHeight = UserSettings.ResolutionHeight;
        savedHz = UserSettings.RefreshRateHz;
        savedModeSeen = savedMode;
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
