using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Runs before the scene's other scripts so their Awake can ask for cue clips.
[DefaultExecutionOrder(-200)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // Test spy: raised with the cue name by PlayCue, PlayCue2D, PlayCueLoop and the UI path.
    public static event System.Action<string> CuePlayed;

    const float SputterFuel = 0.15f;

    const int PoolSize = 12;
    const int Grass = 0;
    const int Dirt = 1;
    const int Rock = 2;
    const int Sand = 3;

    static readonly string[] SurfaceNames = { "grass", "dirt", "rock", "sand" };
    static readonly string[] FootstepCues = { SoundCues.FootstepGrass, SoundCues.FootstepDirt, SoundCues.FootstepRock, SoundCues.FootstepWater };

    [SerializeField] Lantern lantern;
    [SerializeField] AudioMixer mixer;
    [SerializeField] SoundBank bank;

    sealed class DuckState
    {
        public float level;
        public float target;
        public float until;
        public float floor;
        public float releaseRate = DuckReleaseDbPerSecond;
    }

    const float DuckAttackDbPerSecond = 40f;
    const float DuckReleaseDbPerSecond = 8f;

    AudioSource uiSource;
    float[] voiceStart;
    bool[] voicePriority;
    readonly Dictionary<string, int> lastClipVariation = new Dictionary<string, int>();
    readonly Dictionary<string, int> lastSynthVariation = new Dictionary<string, int>();
    readonly HashSet<string> warnedUnknown = new HashSet<string>();
    bool warnedNoBank;
    // Once per cue per session, shared across AudioManager instances.
    static readonly HashSet<string> warnedNoClip = new HashSet<string>();
    static readonly string[] DuckParams = { "MusicDuck", "AmbienceDuck", "TensionDuck" };
    readonly Dictionary<string, DuckState> ducks = new Dictionary<string, DuckState>();

    AudioSource oneShots;
    AudioSource ambience;
    AudioSource heartbeat;
    AudioSource crackle;
    AudioSource[] pool;
    int poolCursor;
    bool heartOn;
    bool lanternResolved;
    bool routed;
    bool crackleHooked;
    float crackleFuel = 1f;
    bool sputterArmed = true;
    string mixState = "Normal";

    Terrain cachedTerrain;
    TerrainData cachedData;
    float[,,] alphaMaps;
    int[] layerKind;
    int alphaWidth;
    int alphaHeight;

    public int PoolChildCount => poolRoot != null ? poolRoot.childCount : 0;
    public AudioMixer Mixer => mixer;
    public AudioMixerGroup MusicGroup { get; private set; }
    public AudioMixerGroup SfxGroup { get; private set; }
    public AudioMixerGroup AmbienceGroup { get; private set; }
    public AudioMixerGroup UiGroup { get; private set; }
    public AudioMixerGroup TensionGroup { get; private set; }
    public AudioMixerGroup StingerGroup { get; private set; }
    public SoundBank Bank => bank;
    // The ambience cue chosen for the active scene (Ambience.IslandN), or null for the generic bed.
    public string AmbienceCue { get; private set; }
    public string MixState => mixState;
    public AudioClip LastFootstep { get; private set; }
    public string LastSurface { get; private set; }
    public float CrackleVolume => crackle != null ? crackle.volume : 0f;

    Transform poolRoot;

    public static float CrackleGainFor(float normalized)
    {
        if (normalized <= 0.001f)
        {
            return 0f;
        }

        return Mathf.Lerp(0.045f, 0.26f, Mathf.Clamp01(normalized));
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate AudioManager destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResetDucks();
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;
        CreatePool();

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        uiSource.ignoreListenerPause = true;

        ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.22f;
        ChooseAmbience();
        // The menu (no island bed) is music and UI only; the generic bed is just the fallback for a scene without a cue.
        if (AmbienceCue != null || SceneManager.GetActiveScene().name != "MainMenu")
        {
            ambience.Play();
        }

        heartbeat = gameObject.AddComponent<AudioSource>();
        heartbeat.playOnAwake = false;
        heartbeat.loop = true;
        heartbeat.spatialBlend = 0f;
        heartbeat.volume = 0.45f;
        heartbeat.clip = ClipFor(SoundCues.LanternHeartbeat) ?? ProceduralAudio.Heartbeat();

        crackle = gameObject.AddComponent<AudioSource>();
        crackle.playOnAwake = false;
        crackle.loop = true;
        crackle.spatialBlend = 0f;
        crackle.volume = 0f;
        crackle.clip = ClipFor(SoundCues.LanternCrackle) ?? ProceduralAudio.CrackleLoop();

        RouteSources();
    }

    // The bed follows the active scene (Island1..4). Anywhere else (the menu) it keeps the generic synthesized bed.
    void ChooseAmbience()
    {
        string cue = null;
        switch (SceneManager.GetActiveScene().name)
        {
            case "Island1":
                cue = SoundCues.AmbienceIsland1;
                break;
            case "Island2":
                cue = SoundCues.AmbienceIsland2;
                break;
            case "Island3":
                cue = SoundCues.AmbienceIsland3;
                break;
            case "Island4":
                cue = SoundCues.AmbienceIsland4;
                break;
        }

        AmbienceCue = null;
        SoundCue c = cue != null ? FindCue(cue) : null;
        AudioClip clip = c != null ? ClipFor(cue) : null;
        if (clip != null)
        {
            AmbienceCue = cue;
            ambience.clip = clip;
            ambience.volume = c.volume;
            return;
        }

        // The menu never plays the bed, so do not synthesise the generic one there.
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            ambience.clip = ProceduralAudio.Ambience();
        }
    }

    // The next clip of a cue from the live manager, or null when there is none (editor tests, scenes without audio).
    public static AudioClip CueClip(string cue)
    {
        // No AudioManager (editor tests, scenes without audio): there is no bank, so the caller stays silent.
        return Instance != null ? Instance.ClipFor(cue) : null;
    }

    void OnEnable()
    {
        UiSound.Requested += OnUiRequested;
        Shade.Stole += OnShadeStole;
    }

    void OnDisable()
    {
        UiSound.Requested -= OnUiRequested;
        Shade.Stole -= OnShadeStole;
    }

    void OnShadeStole(float amount)
    {
        if (Instance == this)
        {
            PlayCue2D(SoundCues.ShadeSteal);
        }
    }

    void OnUiRequested(string cue)
    {
        if (Instance == this)
        {
            PlayCue2D(cue);
        }
    }

    void OnDestroy()
    {
        UiSound.Requested -= OnUiRequested;
        if (lantern != null)
        {
            lantern.FuelChanged -= OnFuelForCrackle;
            lantern.FuelAdjusted -= OnFuelAdjusted;
        }

        if (Instance == this)
        {
            ResetDucks();
            Instance = null;
        }
    }

    // Ducks live in the mixer, which outlives scenes: start and end every manager at 0 dB so none stick.
    void ResetDucks()
    {
        ducks.Clear();
        if (mixer == null)
        {
            return;
        }

        for (int i = 0; i < DuckParams.Length; i++)
        {
            mixer.SetFloat(DuckParams[i], 0f);
        }
    }

    void Start()
    {
        ResolveLantern();
        HookCrackle();
        RouteSources();
        CacheTerrain();
        RouteWhisper();
    }

    void ResolveLantern()
    {
        if (lantern != null || lanternResolved)
        {
            return;
        }

        lanternResolved = true;
        lantern = FindAnyObjectByType<Lantern>();
        if (lantern != null)
        {
            Debug.LogWarning("AudioManager lantern was not wired. Resolved once.", this);
        }
    }

    void HookCrackle()
    {
        if (crackleHooked || lantern == null)
        {
            return;
        }

        crackleHooked = true;
        crackleFuel = lantern.FuelNormalized;
        sputterArmed = crackleFuel >= SputterFuel;
        lantern.FuelChanged += OnFuelForCrackle;
        lantern.FuelAdjusted += OnFuelAdjusted;
        ApplyCrackle(false);
    }

    void OnFuelForCrackle(float normalized)
    {
        crackleFuel = normalized;
        ApplyCrackle(false);
        // One sputter per downward crossing of 15%; it re-arms once fuel rises back above.
        if (normalized < SputterFuel)
        {
            if (sputterArmed)
            {
                sputterArmed = false;
                PlayCue2D(SoundCues.LanternSputter);
            }
        }
        else if (normalized > SputterFuel)
        {
            sputterArmed = true;
        }
    }

    void OnFuelAdjusted(float delta)
    {
        if (delta > 0f)
        {
            PlayCue2D(SoundCues.LanternRefuel);
        }
    }

    void CreatePool()
    {
        GameObject root = new GameObject("OneShotPool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
        pool = new AudioSource[PoolSize];
        voiceStart = new float[PoolSize];
        voicePriority = new bool[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject child = new GameObject("PooledSource");
            child.transform.SetParent(poolRoot, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.pitch = 1f;
            source.volume = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.ignoreListenerPause = false;
            pool[i] = source;
        }
    }

    void RouteSources()
    {
        if (mixer == null || routed)
        {
            return;
        }

        MusicGroup = FindGroup("Music");
        SfxGroup = FindGroup("SFX");
        AmbienceGroup = FindGroup("Ambience");
        UiGroup = FindGroup("UI");
        TensionGroup = FindGroup("Tension");
        StingerGroup = FindGroup("Stinger");
        if (MusicGroup == null || SfxGroup == null || AmbienceGroup == null)
        {
            return;
        }

        routed = true;
        Assign(ambience, AmbienceGroup);
        Assign(oneShots, SfxGroup);
        Assign(heartbeat, SfxGroup);
        Assign(crackle, SfxGroup);
        Assign(uiSource, UiGroup != null ? UiGroup : SfxGroup);
        for (int i = 0; i < pool.Length; i++)
        {
            Assign(pool[i], SfxGroup);
        }

        RouteWhisper();
    }

    void RouteWhisper()
    {
        if (SfxGroup == null)
        {
            return;
        }

        GameObject host = GameObject.Find("MothWhisper");
        if (host == null)
        {
            return;
        }

        AudioSource source = host.GetComponent<AudioSource>();
        Assign(source, SfxGroup);
    }

    AudioMixerGroup FindGroup(string groupName)
    {
        if (mixer == null)
        {
            return null;
        }

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == groupName)
            {
                return groups[i];
            }
        }

        return groupName == "UI" ? null : (groups.Length > 0 ? groups[0] : null);
    }

    static void Assign(AudioSource source, AudioMixerGroup group)
    {
        if (source == null || group == null)
        {
            return;
        }

        source.outputAudioMixerGroup = group;
    }

    public void RouteSfx(AudioSource source)
    {
        if (SfxGroup == null)
        {
            RouteSources();
        }

        Assign(source, SfxGroup);
    }

    public void TransitionMix(bool ducked, string reason)
    {
        if (mixer == null)
        {
            return;
        }

        string snapshotName = ducked ? "Ducked" : "Normal";
        AudioMixerSnapshot snapshot = mixer.FindSnapshot(snapshotName);
        if (snapshot == null)
        {
            Debug.LogWarning("Audio snapshot missing: " + snapshotName, this);
            return;
        }

        mixState = snapshotName;
        UserSettingsApplier.SetDucked(ducked);
        snapshot.TransitionTo(0.35f);
        Debug.Log("Audio snapshot -> " + snapshotName + " (" + reason + ")");
    }

    public void LogRouting()
    {
        LogSource("ambience", ambience);
        LogSource("heartbeat", heartbeat);
        LogSource("fizzleDying", oneShots);
        LogSource("crackle", crackle);
        if (pool != null)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                LogSource("pool" + i, pool[i]);
            }
        }

        GameObject host = GameObject.Find("MothWhisper");
        if (host != null)
        {
            LogSource("mothWhisper", host.GetComponent<AudioSource>());
        }

        if (MusicPlayer.Instance != null)
        {
            LogSource("music0", MusicPlayer.Instance.FirstMusic);
            LogSource("music1", MusicPlayer.Instance.SecondMusic);
        }
    }

    static void LogSource(string label, AudioSource source)
    {
        string group = source != null && source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : "none";
        Debug.Log("Mixer route " + label + " -> " + group);
    }

    void Update()
    {
        TickDucks();
        HookCrackle();
        if (!routed)
        {
            RouteSources();
        }

        ApplyCrackle(true);
        bool low = lantern != null && lantern.FuelNormalized > 0f && lantern.FuelNormalized < 0.2f
            && (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
        if (low == heartOn)
        {
            return;
        }

        heartOn = low;
        if (low)
        {
            heartbeat.Play();
        }
        else
        {
            heartbeat.Stop();
        }
    }

    void ApplyCrackle(bool sputter)
    {
        if (crackle == null)
        {
            return;
        }

        // The crackle belongs to the lantern in play: with none hooked, or in the menu (a display lantern, no
        // GameManager), it stays silent and stopped.
        bool inPlay = crackleHooked && lantern != null && GameManager.Instance != null;
        float gain = inPlay ? CrackleGainFor(crackleFuel) : 0f;
        if (gain <= 0f)
        {
            if (crackle.volume > 0f)
            {
                crackle.volume = 0f;
            }

            if (crackle.isPlaying)
            {
                crackle.Stop();
            }

            return;
        }

        if (!crackle.isPlaying)
        {
            crackle.Play();
        }

        float level = gain;
        if (sputter && crackleFuel < 0.18f)
        {
            float a = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 13.7f));
            float b = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5.1f + 1.3f));
            level *= 0.15f + 0.85f * a * b;
            crackle.pitch = Mathf.Lerp(0.82f, 1f, crackleFuel / 0.18f);
        }
        else if (crackle.pitch != 1f)
        {
            crackle.pitch = 1f;
        }

        crackle.volume = level;
    }

    // ---- Named cues -------------------------------------------------------------------------------------------

    static void RaiseCuePlayed(string cue)
    {
        System.Action<string> handler = CuePlayed;
        if (handler != null)
        {
            handler(cue);
        }
    }

    public AudioSource PlayCue(string cue, Vector3 position)
    {
        return PlayCueInternal(cue, position, false);
    }

    public AudioSource PlayCue2D(string cue)
    {
        return PlayCueInternal(cue, Vector3.zero, true);
    }

    AudioSource PlayCueInternal(string cue, Vector3 position, bool flat)
    {
        SoundCue c = FindCue(cue);
        if (c == null)
        {
            return null;
        }

        AudioClip clip = ClipFor(cue);
        if (clip == null)
        {
            return null;
        }

        float volume = c.volume * Mathf.Pow(10f, (Random.value * 2f - 1f) * c.volumeJitterDb / 20f);
        float pitch = Mathf.Approximately(c.pitch.x, c.pitch.y) ? c.pitch.x : Random.Range(c.pitch.x, c.pitch.y);
        if (pitch <= 0f)
        {
            pitch = 1f;
        }

        RaiseCuePlayed(cue);
        if (c.priority)
        {
            Duck("AmbienceDuck", -4f, 1f);
            Duck("TensionDuck", -4f, 1f);
        }

        if (c.group == SoundGroup.UI)
        {
            if (uiSource == null)
            {
                return null;
            }

            if (!routed)
            {
                RouteSources();
            }

            uiSource.pitch = pitch;
            uiSource.PlayOneShot(clip, volume);
            return uiSource;
        }

        float blend = c.spatial && !flat ? 1f : 0f;
        return StartVoice(clip, position, volume, pitch, blend, GroupFor(c.group), c.priority,
            c.minDistance, c.maxDistance, c.rolloff);
    }

    public void PlayCueLoop(string cue, AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        SoundCue c = FindCue(cue);
        if (c == null)
        {
            return;
        }

        AudioClip clip = ClipFor(cue);
        if (clip == null)
        {
            return;
        }

        RaiseCuePlayed(cue);
        AudioMixerGroup group = GroupFor(c.group);
        if (group != null)
        {
            source.outputAudioMixerGroup = group;
        }

        bool changed = source.clip != clip;
        source.clip = clip;
        source.loop = true;
        source.volume = c.volume;
        source.pitch = Mathf.Approximately(c.pitch.x, c.pitch.y) ? c.pitch.x : Random.Range(c.pitch.x, c.pitch.y);
        source.spatialBlend = c.spatial ? 1f : 0f;
        source.rolloffMode = c.rolloff;
        source.minDistance = c.minDistance;
        source.maxDistance = c.maxDistance;
        source.playOnAwake = false;
        if (changed || !source.isPlaying)
        {
            source.Play();
        }
    }

    // The next variation of the cue (never the same one twice in a row), or its synthesized fallback.
    public AudioClip ClipFor(string cue)
    {
        SoundCue c = FindCue(cue);
        if (c == null)
        {
            return null;
        }

        int count = 0;
        if (c.clips != null)
        {
            for (int i = 0; i < c.clips.Length; i++)
            {
                if (c.clips[i] != null)
                {
                    count++;
                }
            }
        }

        if (count > 0)
        {
            int last;
            if (!lastClipVariation.TryGetValue(cue, out last))
            {
                last = -1;
            }

            int pick = NextIndex(ref last, count);
            lastClipVariation[cue] = last;
            int seen = 0;
            for (int i = 0; i < c.clips.Length; i++)
            {
                if (c.clips[i] != null)
                {
                    if (seen == pick)
                    {
                        return c.clips[i];
                    }

                    seen++;
                }
            }
        }

        if (!c.synthOnly && warnedNoClip.Add(cue))
        {
            Debug.LogWarning("Sound cue has no clip, using " + (c.fallback == SynthFallback.None ? "nothing" : "synth fallback") + ": " + cue, this);
        }

        return SynthClip(c);
    }

    SoundCue FindCue(string cue)
    {
        if (bank == null)
        {
            if (!warnedNoBank)
            {
                warnedNoBank = true;
                Debug.LogWarning("AudioManager has no SoundBank assigned.", this);
            }

            return null;
        }

        SoundCue c = bank.Find(cue);
        if (c == null && warnedUnknown.Add(cue))
        {
            Debug.LogWarning("Unknown sound cue: " + cue, this);
        }

        return c;
    }

    AudioMixerGroup GroupFor(SoundGroup group)
    {
        if (!routed)
        {
            RouteSources();
        }

        switch (group)
        {
            case SoundGroup.Music:
                return MusicGroup;
            case SoundGroup.Ambience:
                return AmbienceGroup;
            case SoundGroup.UI:
                return UiGroup != null ? UiGroup : SfxGroup;
            default:
                return SfxGroup;
        }
    }

    int NextVariant(string cue, int count)
    {
        int last;
        if (!lastSynthVariation.TryGetValue(cue, out last))
        {
            last = -1;
        }

        int index = NextIndex(ref last, count);
        lastSynthVariation[cue] = last;
        return index;
    }

    AudioClip SynthClip(SoundCue c)
    {
        switch (c.fallback)
        {
            case SynthFallback.Chime:
                return ProceduralAudio.ChimeVariant(NextVariant(c.name, 3));
            case SynthFallback.FireflyArrive:
                return ProceduralAudio.FireflyArrive();
            case SynthFallback.Beacon:
                return ProceduralAudio.BeaconVariant(NextVariant(c.name, 3));
            case SynthFallback.Whoomp:
                return ProceduralAudio.Whoomp();
            case SynthFallback.Footstep:
                return ProceduralAudio.FootstepVariant(SurfaceForCue(c.name), NextVariant(c.name, ProceduralAudio.SurfaceVariantCount));
            case SynthFallback.Splash:
                return ProceduralAudio.SplashVariant(NextVariant(c.name, 3));
            case SynthFallback.Fizzle:
                return ProceduralAudio.FizzleVariant(NextVariant(c.name, 3));
            case SynthFallback.Crackle:
                return ProceduralAudio.CrackleLoop();
            case SynthFallback.Dying:
                return ProceduralAudio.LanternDying();
            case SynthFallback.Heartbeat:
                return ProceduralAudio.Heartbeat();
            case SynthFallback.MothFlutter:
                return ProceduralAudio.MothFlutter();
            case SynthFallback.MothWhisper:
                return ProceduralAudio.MothWhisper();
            case SynthFallback.ShadeDrone:
                return ProceduralAudio.ShadeDrone();
            case SynthFallback.Ambience:
                return ProceduralAudio.Ambience();
            case SynthFallback.Surf:
                return ProceduralAudio.SurfSwell();
            case SynthFallback.WindBed:
                return ProceduralAudio.WindBed();
            case SynthFallback.WindHowl:
                return ProceduralAudio.WindHowl();
            case SynthFallback.Rain:
                return ProceduralAudio.Rain();
            case SynthFallback.ThunderRumble:
                return ProceduralAudio.ThunderRumble();
            case SynthFallback.ThunderCrack:
                return ProceduralAudio.ThunderCrack();
            default:
                return null;
        }
    }

    static int SurfaceForCue(string cueName)
    {
        if (cueName.EndsWith("Dirt"))
        {
            return Dirt;
        }

        if (cueName.EndsWith("Rock"))
        {
            return Rock;
        }

        if (cueName.EndsWith("Water"))
        {
            return Sand;
        }

        return Grass;
    }

    // Lowers a mixer parameter (dB) to `db` for `seconds` of unscaled time, then releases it. Repeats extend the hold and
    // never go below the target.
    public void Duck(string parameter, float db, float seconds)
    {
        Duck(parameter, db, seconds, DuckReleaseDbPerSecond);
    }

    // Same, with a custom release speed (dB per second) for short, punchy ducks such as stingers.
    public void Duck(string parameter, float db, float seconds, float releaseDbPerSecond)
    {
        if (mixer == null)
        {
            return;
        }

        DuckState state = StateFor(parameter);
        bool held = Time.unscaledTime < state.until;
        state.target = held ? Mathf.Min(state.target, db) : db;
        state.until = Mathf.Max(state.until, Time.unscaledTime + seconds);
        // Per call, and the faster rate wins while a duck is already held, so a default duck can't slow a stinger's recovery.
        float rate = Mathf.Max(1f, releaseDbPerSecond);
        state.releaseRate = held ? Mathf.Max(state.releaseRate, rate) : rate;
    }

    // A continuous floor (dB, 0 or below) for a parameter. It combines with timed ducks by taking the deeper of the two.
    public void SetDuckFloor(string parameter, float db)
    {
        if (mixer == null)
        {
            return;
        }

        DuckState state = StateFor(parameter);
        state.floor = Mathf.Min(0f, db);
    }

    DuckState StateFor(string parameter)
    {
        DuckState state;
        if (!ducks.TryGetValue(parameter, out state))
        {
            state = new DuckState();
            ducks[parameter] = state;
        }

        return state;
    }

    void TickDucks()
    {
        if (ducks.Count == 0 || mixer == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        foreach (KeyValuePair<string, DuckState> pair in ducks)
        {
            DuckState state = pair.Value;
            bool holding = Time.unscaledTime < state.until;
            float goal = Mathf.Min(holding ? state.target : 0f, state.floor);
            if (Mathf.Approximately(state.level, goal))
            {
                continue;
            }

            float rate = goal < state.level ? DuckAttackDbPerSecond : state.releaseRate;
            state.level = Mathf.MoveTowards(state.level, goal, rate * dt);
            mixer.SetFloat(pair.Key, state.level);
        }
    }

    public void PlayFirefly(Vector3 position)
    {
        PlayCue(SoundCues.FireflyChime, position);
    }

    public void PlayFireflyArrive(Vector3 position)
    {
        PlayCue(SoundCues.FireflyArrive, position);
    }

    // The ignition whoosh. The whoomp layer is the Beacon.Ignite cue, played by BeaconVisual.
    public void PlayBeacon(Vector3 position)
    {
        PlayCue(SoundCues.BeaconWhoosh, position);
    }

    // Surface index (0 grass, 1 dirt, 2 rock, 3 sand/shallow water) to its footstep cue.
    public static string FootstepCueFor(int surface)
    {
        return FootstepCues[Mathf.Clamp(surface, 0, FootstepCues.Length - 1)];
    }

    public void PlayFootstep(Vector3 position)
    {
        int surface = SampleSurface(position);
        string cue = FootstepCueFor(surface);
        AudioSource source = PlayCue(cue, position);
        LastSurface = SurfaceNames[surface];
        if (source != null)
        {
            LastFootstep = source.clip;
        }
    }

    public void PlaySplash(Vector3 position)
    {
        PlayCue(SoundCues.WaterSplash, position);
    }

    public void PlayDying()
    {
        PlayCue2D(SoundCues.LanternDeathGutter);
    }

    public void PlayFizzle()
    {
        PlayCue2D(SoundCues.BeaconFizzle);
    }

    public void PlayLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
        {
            return;
        }

        RouteSfx(source);
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 1f;
        source.volume = volume;
        source.playOnAwake = false;
        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    public void PlayOneShot(AudioClip clip, Vector3 position, float volume)
    {
        PlayOneShot(clip, position, volume, 1f);
    }

    public void PlayOneShot(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        if (clip == null)
        {
            return;
        }

        StartVoice(clip, position, volume, pitch, 1f, SfxGroup, false, 1f, 500f, AudioRolloffMode.Logarithmic);
    }

    AudioSource StartVoice(AudioClip clip, Vector3 position, float volume, float pitch, float spatialBlend, AudioMixerGroup group,
        bool priority, float minDistance, float maxDistance, AudioRolloffMode rolloff)
    {
        int index = NextVoice();
        AudioSource source = pool[index];
        source.Stop();
        source.clip = clip;
        source.transform.position = position;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        source.rolloffMode = rolloff;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        if (group != null)
        {
            source.outputAudioMixerGroup = group;
        }

        voiceStart[index] = Time.unscaledTime;
        voicePriority[index] = priority;
        source.Play();
        return source;
    }

    static int NextIndex(ref int last, int count)
    {
        if (count <= 1)
        {
            last = 0;
            return 0;
        }

        int index;
        if (last < 0)
        {
            index = Random.Range(0, count);
        }
        else
        {
            index = Random.Range(0, count - 1);
            if (index >= last)
            {
                index++;
            }
        }

        last = index;
        return index;
    }

    // A free voice if there is one; otherwise steals the oldest non-priority voice, and only then the oldest priority one.
    int NextVoice()
    {
        int count = pool.Length;
        for (int n = 0; n < count; n++)
        {
            int index = (poolCursor + n) % count;
            if (!pool[index].isPlaying)
            {
                poolCursor = (index + 1) % count;
                return index;
            }
        }

        int oldest = -1;
        int oldestAny = poolCursor;
        for (int n = 0; n < count; n++)
        {
            int index = (poolCursor + n) % count;
            if (voiceStart[index] < voiceStart[oldestAny])
            {
                oldestAny = index;
            }

            if (!voicePriority[index] && (oldest < 0 || voiceStart[index] < voiceStart[oldest]))
            {
                oldest = index;
            }
        }

        int chosen = oldest >= 0 ? oldest : oldestAny;
        poolCursor = (chosen + 1) % count;
        return chosen;
    }

    void CacheTerrain()
    {
        if (alphaMaps != null)
        {
            return;
        }

        cachedTerrain = Terrain.activeTerrain;
        if (cachedTerrain == null || cachedTerrain.terrainData == null)
        {
            return;
        }

        cachedData = cachedTerrain.terrainData;
        alphaWidth = cachedData.alphamapWidth;
        alphaHeight = cachedData.alphamapHeight;
        alphaMaps = cachedData.GetAlphamaps(0, 0, alphaWidth, alphaHeight);
        TerrainLayer[] layers = cachedData.terrainLayers;
        layerKind = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            layerKind[i] = Classify(layers[i] != null ? layers[i].name : "");
        }
    }

    static int Classify(string layerName)
    {
        string name = layerName.ToLowerInvariant();
        if (name.Contains("rock") || name.Contains("cliff"))
        {
            return Rock;
        }

        if (name.Contains("sand"))
        {
            return Sand;
        }

        if (name.Contains("dirt") || name.Contains("path"))
        {
            return Dirt;
        }

        return Grass;
    }

    int SampleSurface(Vector3 world)
    {
        if (alphaMaps == null)
        {
            CacheTerrain();
        }

        if (alphaMaps == null || cachedTerrain == null || cachedData == null)
        {
            return Grass;
        }

        Vector3 origin = cachedTerrain.transform.position;
        Vector3 size = cachedData.size;
        if (size.x <= 0.01f || size.z <= 0.01f)
        {
            return Grass;
        }

        float u = (world.x - origin.x) / size.x;
        float v = (world.z - origin.z) / size.z;
        if (u < 0f || v < 0f || u > 1f || v > 1f)
        {
            return Grass;
        }

        int x = Mathf.Clamp(Mathf.RoundToInt(u * (alphaWidth - 1)), 0, alphaWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (alphaHeight - 1)), 0, alphaHeight - 1);
        int layers = layerKind.Length;
        int best = Grass;
        float weight = -1f;
        for (int i = 0; i < layers; i++)
        {
            float sample = alphaMaps[y, x, i];
            if (sample > weight)
            {
                weight = sample;
                best = layerKind[i];
            }
        }

        return best;
    }
}
}
