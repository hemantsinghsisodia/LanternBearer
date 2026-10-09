using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
public class MusicPlayer : MonoBehaviour
{
    // Music is 6 dB louder than the original 0.35 level (x1.995). One constant lifts the island track, the tension loop
    // (it follows BaseVolume) and the menu music. Stingers use the same boost: the director caps the source volume at 1.0
    // and the remainder goes through the Stinger mixer group.
    public const float MusicBoost = 1.99526f;
    public const float BaseVolume = 0.35f * MusicBoost;
    // 20 * log10(0.8 * MusicBoost): what a stinger needs on top of a source volume of 1.0 (it was 0.8).
    public const float StingerMixerTrimDb = 4.062f;
    const float FadeInDuration = 3f;
    const float CrossfadeDuration = 2f;

    // Muted is derived from UserSettings.MusicVolume (the single source of truth), read at most once per frame.
    static int muteFrame = -1;
    static bool muted;

    AudioSource sourceA;
    AudioSource sourceB;
    AudioSource active;
    AudioSource fadingOut;
    MusicLibrary library;
    Lantern lantern;
    DawnSequence dawn;
    float fadeDuration = 1f;
    float fadeElapsed;
    bool fading;
    bool sceneRefsResolved;
    bool mixerRouted;

    public static MusicPlayer Instance { get; private set; }
    public AudioSource FirstMusic => sourceA;
    public AudioSource SecondMusic => sourceB;

    public static bool IsMuted
    {
        get
        {
            if (!Application.isPlaying || muteFrame != Time.frameCount)
            {
                muteFrame = Application.isPlaying ? Time.frameCount : -1;
                muted = UserSettings.MusicMuted;
            }

            return muted;
        }
    }

    // Drops the cached mute state, so the next IsMuted read looks at the saved volume again.
    public static void ReloadMute()
    {
        muteFrame = -1;
        if (Instance != null)
        {
            Instance.ApplyVolumes();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        muteFrame = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<MusicPlayer>() != null)
        {
            return;
        }

        GameObject player = new GameObject("MusicPlayer");
        player.AddComponent<MusicPlayer>();
    }

    public static void ToggleMute()
    {
        UserSettings.ToggleMusicMute();
        ReloadMute();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate MusicPlayer destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        sourceA = AddSource();
        sourceB = AddSource();
        library = Resources.Load<MusicLibrary>("MusicLibrary");
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
        {
            ToggleMute();
        }

        if (!sceneRefsResolved)
        {
            ResolveSceneRefs();
        }

        TickFade();
        ApplyVolumes();
        RouteMixer();
    }

    void RouteMixer()
    {
        if (mixerRouted || AudioManager.Instance == null || AudioManager.Instance.MusicGroup == null)
        {
            return;
        }

        mixerRouted = true;
        if (sourceA != null)
        {
            sourceA.outputAudioMixerGroup = AudioManager.Instance.MusicGroup;
        }

        if (sourceB != null)
        {
            sourceB.outputAudioMixerGroup = AudioManager.Instance.MusicGroup;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lantern = null;
        dawn = null;
        sceneRefsResolved = false;
        ApplyScene(scene.name);
    }

    void ResolveSceneRefs()
    {
        sceneRefsResolved = true;
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (dawn == null)
        {
            dawn = FindAnyObjectByType<DawnSequence>();
        }
    }

    void ApplyScene(string sceneName)
    {
        if (library == null)
        {
            library = Resources.Load<MusicLibrary>("MusicLibrary");
        }

        if (library == null)
        {
            return;
        }

        if (sceneName.StartsWith("Island") && FindAnyObjectByType<MusicDirector>() != null)
        {
            // The scene's MusicDirector owns island music; fade the menu track out.
            HandOver();
            return;
        }

        AudioClip clip = null;
        if (sceneName == "MainMenu")
        {
            clip = library.menuTrack;
        }
        else
        {
            return;
        }

        if (clip == null)
        {
            return;
        }

        if (active != null && active.clip == clip)
        {
            return;
        }

        CrossfadeTo(clip);
    }

    void HandOver()
    {
        if (active == null || active.clip == null)
        {
            return;
        }

        if (fadingOut != null && fadingOut != active)
        {
            fadingOut.Stop();
            fadingOut.clip = null;
            fadingOut.volume = 0f;
        }

        fadingOut = active;
        active = null;
        fadeElapsed = 0f;
        fadeDuration = CrossfadeDuration;
        fading = true;
    }

    void CrossfadeTo(AudioClip clip)
    {
        AudioSource incoming = active == sourceA ? sourceB : sourceA;
        if (fadingOut == incoming)
        {
            fadingOut = null;
        }

        incoming.Stop();
        incoming.clip = clip;
        incoming.loop = true;
        incoming.volume = 0f;
        incoming.Play();

        fadingOut = active;
        active = incoming;
        fadeElapsed = 0f;
        fadeDuration = fadingOut == null || fadingOut.clip == null ? FadeInDuration : CrossfadeDuration;
        fading = true;
        ApplyVolumes();
    }

    void TickFade()
    {
        if (!fading)
        {
            return;
        }

        fadeElapsed += Time.unscaledDeltaTime;
        if (fadeElapsed < fadeDuration)
        {
            return;
        }

        fading = false;
        fadeElapsed = fadeDuration;
        if (fadingOut != null)
        {
            fadingOut.Stop();
            fadingOut.clip = null;
            fadingOut.volume = 0f;
            fadingOut = null;
        }
    }

    void ApplyVolumes()
    {
        float target = BaseVolume * MoodGain();
        float blend = fading ? Mathf.Clamp01(fadeElapsed / Mathf.Max(0.01f, fadeDuration)) : 1f;
        if (active != null && active.clip != null)
        {
            active.volume = target * blend;
        }

        if (fadingOut != null && fadingOut.clip != null)
        {
            fadingOut.volume = target * (1f - blend);
        }
    }

    float MoodGain()
    {
        return Mood(lantern, dawn);
    }

    // 0 when muted, 0.5 on low fuel, else 1. Shared with the MusicDirector.
    public static float Mood(Lantern lantern, DawnSequence dawn)
    {
        if (IsMuted)
        {
            return 0f;
        }

        bool dawning = (GameManager.Instance != null && GameManager.Instance.DawnPlaying) || (dawn != null && dawn.IsPlaying);
        if (dawning)
        {
            return 1f;
        }

        bool low = lantern != null && lantern.FuelNormalized > 0f && lantern.FuelNormalized < 0.2f
            && (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
        return low ? 0.5f : 1f;
    }

    AudioSource AddSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        return source;
    }
}
}
