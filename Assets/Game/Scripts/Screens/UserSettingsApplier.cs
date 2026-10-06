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

    // Calls Screen.SetResolution only when the wanted size, refresh rate or window mode differs from the last request.
    private void ApplyScreen()
    {
        Resolution desktop = Screen.currentResolution;
        Resolution wanted = UserSettings.ResolveResolution(Screen.resolutions, desktop);
        FullScreenMode mode = UserSettings.WindowMode;
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
