using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
public class MusicPlayer : MonoBehaviour
{
    const string MutePref = "LanternKeeperMusicMuted";
    const float BaseVolume = 0.35f;
    const float FadeInDuration = 3f;
    const float CrossfadeDuration = 2f;

    static bool muteLoaded;
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
            EnsureMuteLoaded();
            return muted;
        }
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
        EnsureMuteLoaded();
        muted = !muted;
        PlayerPrefs.SetInt(MutePref, muted ? 1 : 0);
        PlayerPrefs.Save();
        if (Instance != null)
        {
            Instance.ApplyVolumes();
        }
    }

    static void EnsureMuteLoaded()
    {
        if (muteLoaded)
        {
            return;
        }

        muteLoaded = true;
        muted = PlayerPrefs.GetInt(MutePref, 0) == 1;
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

        AudioClip clip = null;
        if (sceneName == "MainMenu")
        {
            clip = library.menuTrack;
        }
        else if (sceneName == "Island1" || sceneName == "Island2")
        {
            clip = library.islandTrack;
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
        if (IsMuted)
        {
            return 0f;
        }

        if (DawnActive())
        {
            return 1f;
        }

        if (LowFuel())
        {
            return 0.5f;
        }

        return 1f;
    }

    bool DawnActive()
    {
        if (GameManager.Instance != null && GameManager.Instance.DawnPlaying)
        {
            return true;
        }

        return dawn != null && dawn.IsPlaying;
    }

    bool LowFuel()
    {
        return lantern != null && lantern.FuelNormalized > 0f && lantern.FuelNormalized < 0.2f
            && (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
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
