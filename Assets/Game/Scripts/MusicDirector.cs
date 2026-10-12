using UnityEngine;

namespace LanternKeeper
{
// Plays an island's track and tension loop in sync, raises the tension loop with a threat value, and plays stingers.
// It only reads game state. Lives on the island's Systems object.
public class MusicDirector : MonoBehaviour
{
    const float TensionGain = 0.708f; // -3 dB
    const float FadeInDuration = 3f;
    const float HushSeconds = 3f;
    // The hush clip is normalised like the track, so it plays at 0.6x the base music volume (about -4.4 dB) to read as a hush.
    const float HushLevel = 0.6f;
    const float StartDelay = 0.1f;
    // 0.8 x MusicPlayer.MusicBoost would be 1.6, above what a source can play, so the source stays at 1.0 and the
    // Stinger mixer group (UserSettingsApplier adds MusicPlayer.StingerMixerTrimDb, +4.06 dB) carries the rest of the +6 dB.
    const float StingerVolume = 1f;
    const float StingerDuckDb = -6f;
    const float StingerReleaseDbPerSecond = 30f;
    const float AmbienceDuckDb = -3f;

    AudioSource track;
    AudioSource tension;
    AudioSource stinger;
    MusicLibrary library;
    Lantern lantern;
    DawnSequence dawn;
    GameManager subscribed;
    bool listening;
    bool routed;
    bool refsResolved;
    bool hasTension;
    float threat;
    float tensionFloor;
    bool hushed;
    float hushElapsed;
    AudioSource hushSource;
    MusicLibrary.IslandMusic entryInUse;
    float elapsed;
    // The Island 5 finale cue: seconds since PlayFinale (-1 before it), and its source.
    const float FinaleFadeSeconds = 1.5f;
    float finaleElapsed = -1f;
    AudioSource finaleSource;

    public float Threat => threat;
    // Storm tension never drops below this (Island 5 height rings). Zero on the other islands.
    public float TensionFloor => tensionFloor;
    public float EffectiveThreat => Mathf.Max(threat, tensionFloor);
    public bool Hushed => hushed;
    public AudioSource HushSource => hushSource;
    // Times a stinger was requested (muted or not); tests use it to prove a win plays exactly one.
    public int StingerRequests { get; private set; }
    public AudioSource Track => track;
    public AudioSource Tension => tension;
    public AudioSource StingerSource => stinger;
    public bool FinalePlaying => finaleElapsed >= 0f;
    public AudioSource FinaleSource => finaleSource;

    void Awake()
    {
        track = NewSource(true);
        tension = NewSource(true);
        stinger = NewSource(false);
        // The stinger is a one-shot player: it must be audible, unlike the loops whose volume the director drives.
        stinger.volume = 1f;
    }

    void Start()
    {
        Begin(Resources.Load<MusicLibrary>("MusicLibrary"));
    }

    void OnEnable()
    {
        Beacon.Lit += OnBeaconLit;
        listening = true;
    }

    void OnDisable()
    {
        listening = false;
        Beacon.Lit -= OnBeaconLit;
        Unsubscribe();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetDuckFloor("AmbienceDuck", 0f);
        }
    }

    AudioSource NewSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        return source;
    }

    // (Re)starts the island's music from `source`. Both loops are scheduled for the same DSP time so they stay in sync.
    public void Begin(MusicLibrary source)
    {
        library = source;
        track.Stop();
        tension.Stop();
        track.clip = null;
        tension.clip = null;
        threat = 0f;
        tensionFloor = 0f;
        hushed = false;
        hushElapsed = 0f;
        finaleElapsed = -1f;
        if (finaleSource != null)
        {
            finaleSource.Stop();
        }

        entryInUse = null;
        if (hushSource != null)
        {
            hushSource.Stop();
        }

        elapsed = 0f;
        hasTension = false;
        if (library == null)
        {
            return;
        }

        MusicLibrary.IslandMusic entry = library.For(gameObject.scene.name);
        entryInUse = entry;
        AudioClip trackClip = entry != null ? entry.track : null;
        AudioClip tensionClip = entry != null ? entry.tension : null;
        if (trackClip == null)
        {
            return;
        }

        track.clip = trackClip;
        // The tension loop is a separate piece of music and need not match the track's length: both start at the same DSP
        // time, then each loops on its own.
        double start = AudioSettings.dspTime + StartDelay;
        track.PlayScheduled(start);
        if (tensionClip != null)
        {
            tension.clip = tensionClip;
            tension.PlayScheduled(start);
            hasTension = true;
        }

        routed = false;
        ApplyVolumes();
    }

    void Update()
    {
        ResolveRefs();
        Route();
        float dt = Time.unscaledDeltaTime;
        elapsed += dt;
        float target = ThreatMix.Target(lantern != null && lantern.MothsDraining > 0, NearestShade(),
            lantern != null ? lantern.FuelNormalized : 1f, lantern != null && lantern.InSafeLight);
        threat = ThreatMix.Step(threat, target, dt);
        if (hushed)
        {
            hushElapsed += dt;
        }

        if (finaleElapsed >= 0f)
        {
            finaleElapsed += dt;
        }

        ApplyVolumes();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetDuckFloor("AmbienceDuck", AmbienceDuckDb * EffectiveThreat * HushScale());
        }
    }

    void ResolveRefs()
    {
        if (!refsResolved)
        {
            refsResolved = true;
            lantern = FindAnyObjectByType<Lantern>();
            dawn = FindAnyObjectByType<DawnSequence>();
        }

        if (listening && subscribed == null && GameManager.Instance != null)
        {
            subscribed = GameManager.Instance;
            subscribed.WonGame += OnWon;
            subscribed.LostGame += OnLost;
        }
    }

    void Unsubscribe()
    {
        if (subscribed != null)
        {
            subscribed.WonGame -= OnWon;
            subscribed.LostGame -= OnLost;
        }

        subscribed = null;
    }

    void Route()
    {
        AudioManager audio = AudioManager.Instance;
        if (routed || audio == null || audio.MusicGroup == null)
        {
            return;
        }

        routed = true;
        track.outputAudioMixerGroup = audio.MusicGroup;
        stinger.outputAudioMixerGroup = audio.StingerGroup != null ? audio.StingerGroup : audio.MusicGroup;
        tension.outputAudioMixerGroup = audio.TensionGroup != null ? audio.TensionGroup : audio.MusicGroup;
    }

    float NearestShade()
    {
        if (lantern == null)
        {
            return float.PositiveInfinity;
        }

        Vector3 position = lantern.transform.position;
        float best = float.PositiveInfinity;
        for (int i = 0; i < Shade.All.Count; i++)
        {
            Shade shade = Shade.All[i];
            if (shade == null || !shade.isActiveAndEnabled || shade.IsDespawning)
            {
                continue;
            }

            float distance = Vector3.Distance(position, shade.transform.position);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    void ApplyVolumes()
    {
        float fade = Mathf.Clamp01(elapsed / FadeInDuration);
        // Low fuel is itself a threat input, so the mood dip (low fuel, dawn) applies to the track only. The tension layer
        // follows the base volume, which still respects mute and the fade-in.
        float mute = MusicPlayer.IsMuted ? 0f : 1f;
        float baseVolume = MusicPlayer.BaseVolume * mute * fade;
        float hush = HushScale();
        track.volume = MusicPlayer.BaseVolume * MusicPlayer.Mood(lantern, dawn) * fade * hush;
        tension.volume = hasTension ? baseVolume * TensionGain * EffectiveThreat * hush : 0f;
        if (hushSource != null)
        {
            hushSource.volume = hushed ? baseVolume * HushLevel * Mathf.Clamp01(hushElapsed / HushSeconds) * FinaleFade() : 0f;
        }

        if (finaleSource != null)
        {
            // The cue plays at the base music level, un-ducked by mood or threat.
            finaleSource.volume = baseVolume;
        }
    }

    // 1 until the summit hush, then fades to 0 over HushSeconds. The finale cue fades the track and tension out too.
    float HushScale()
    {
        float scale = hushed ? 1f - Mathf.Clamp01(hushElapsed / HushSeconds) : 1f;
        return Mathf.Min(scale, FinaleFade());
    }

    // 1 before the finale cue, then 0 after FinaleFadeSeconds.
    float FinaleFade()
    {
        return finaleElapsed < 0f ? 1f : 1f - Mathf.Clamp01(finaleElapsed / FinaleFadeSeconds);
    }

    // Island 5 height rings: the storm never sounds calmer than this once the keeper has climbed.
    public void SetTensionFloor(float floor)
    {
        tensionFloor = Mathf.Clamp01(floor);
    }

    // The keeper reached the summit: the track and tension fade out over 3 s and the island's hush clip (if any) fades in.
    public void EnterSummitHush()
    {
        if (hushed)
        {
            return;
        }

        hushed = true;
        hushElapsed = 0f;
        AudioClip clip = entryInUse != null ? entryInUse.hush : null;
        if (clip == null)
        {
            return;
        }

        if (hushSource == null)
        {
            hushSource = NewSource(true);
            AudioManager audio = AudioManager.Instance;
            if (audio != null && audio.MusicGroup != null)
            {
                hushSource.outputAudioMixerGroup = audio.MusicGroup;
            }
        }

        hushSource.clip = clip;
        hushSource.Play();
    }

    // The Island 5 finale: the track, tension and hush fade out over 1.5 s and the finale cue plays once on the music bus.
    public void PlayFinale()
    {
        if (finaleElapsed >= 0f)
        {
            return;
        }

        // Win() ducked the mix (-8 dB) for the short dawn; the finale cue must play at full music level.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionMix(false, "finale");
        }

        finaleElapsed = 0f;
        AudioClip clip = library != null ? library.finaleCue : null;
        if (clip == null)
        {
            return;
        }

        if (finaleSource == null)
        {
            finaleSource = NewSource(false);
            AudioManager audio = AudioManager.Instance;
            if (audio != null && audio.MusicGroup != null)
            {
                finaleSource.outputAudioMixerGroup = audio.MusicGroup;
            }
        }

        finaleSource.clip = clip;
        finaleSource.loop = false;
        finaleSource.volume = MusicPlayer.IsMuted ? 0f : MusicPlayer.BaseVolume;
        finaleSource.Play();
    }

    void OnBeaconLit(Beacon beacon)
    {
        if (library != null)
        {
            PlayStinger(library.stingerBeaconLit);
        }
    }

    void OnWon()
    {
        // Island 5 has the finale cue instead of the win stinger.
        if (GameManager.Instance != null && GameManager.Instance.HasFinale)
        {
            return;
        }

        if (library != null)
        {
            PlayStinger(library.stingerWin);
        }
    }

    void OnLost()
    {
        if (library != null)
        {
            PlayStinger(library.stingerLose);
        }
    }

    // A 2D one-shot on the Stinger group (outside the music duck). Ducks the music bus while it plays; silent when music is muted.
    public void PlayStinger(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        StingerRequests++;
        if (MusicPlayer.IsMuted)
        {
            return;
        }

        stinger.Stop();
        stinger.PlayOneShot(clip, StingerVolume);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Duck("MusicDuck", StingerDuckDb, clip.length, StingerReleaseDbPerSecond);
        }
    }
}
}
