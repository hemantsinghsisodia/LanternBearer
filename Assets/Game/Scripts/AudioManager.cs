using UnityEngine;
using UnityEngine.Audio;

namespace LanternKeeper
{
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const int PoolSize = 12;
    const int Grass = 0;
    const int Dirt = 1;
    const int Rock = 2;
    const int Sand = 3;

    static readonly string[] SurfaceNames = { "grass", "dirt", "rock", "sand" };

    [SerializeField] Lantern lantern;
    [SerializeField] AudioMixer mixer;

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
    int[] lastStep = { -1, -1, -1, -1 };
    int lastChime = -1;
    int lastBeacon = -1;
    int lastSplash = -1;
    int lastFizzle = -1;
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
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;
        CreatePool();

        ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.22f;
        ambience.clip = ProceduralAudio.Ambience();
        ambience.Play();

        heartbeat = gameObject.AddComponent<AudioSource>();
        heartbeat.playOnAwake = false;
        heartbeat.loop = true;
        heartbeat.spatialBlend = 0f;
        heartbeat.volume = 0.45f;
        heartbeat.clip = ProceduralAudio.Heartbeat();

        crackle = gameObject.AddComponent<AudioSource>();
        crackle.playOnAwake = false;
        crackle.loop = true;
        crackle.spatialBlend = 0f;
        crackle.volume = 0f;
        crackle.clip = ProceduralAudio.CrackleLoop();
        crackle.Play();

        RouteSources();
    }

    void OnDestroy()
    {
        if (lantern != null)
        {
            lantern.FuelChanged -= OnFuelForCrackle;
        }

        if (Instance == this)
        {
            Instance = null;
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
        lantern.FuelChanged += OnFuelForCrackle;
        ApplyCrackle(false);
    }

    void OnFuelForCrackle(float normalized)
    {
        crackleFuel = normalized;
        ApplyCrackle(false);
    }

    void CreatePool()
    {
        GameObject root = new GameObject("OneShotPool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
        pool = new AudioSource[PoolSize];
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
        if (MusicGroup == null || SfxGroup == null || AmbienceGroup == null)
        {
            return;
        }

        routed = true;
        Assign(ambience, AmbienceGroup);
        Assign(oneShots, SfxGroup);
        Assign(heartbeat, SfxGroup);
        Assign(crackle, SfxGroup);
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

        return groups.Length > 0 ? groups[0] : null;
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

        float gain = CrackleGainFor(crackleFuel);
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

    public void PlayFirefly(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.ChimeVariant(NextIndex(ref lastChime, 3)), position, 0.8f);
    }

    public void PlayFireflyArrive(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.FireflyArrive(), position, 0.5f);
    }

    public void PlayBeacon(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.BeaconVariant(NextIndex(ref lastBeacon, 3)), position, 0.9f);
    }

    public void PlayFootstep(Vector3 position)
    {
        int surface = SampleSurface(position);
        int variant = NextIndex(ref lastStep[surface], ProceduralAudio.SurfaceVariantCount);
        AudioClip clip = ProceduralAudio.FootstepVariant(surface, variant);
        LastFootstep = clip;
        LastSurface = SurfaceNames[surface];
        float pitch = 1f + (Random.value * 2f - 1f) * 0.08f;
        float volume = 0.32f * (1f + (Random.value * 2f - 1f) * 0.1f);
        PlayOneShot(clip, position, volume, pitch);
    }

    public void PlaySplash(Vector3 position)
    {
        PlayOneShot(ProceduralAudio.SplashVariant(NextIndex(ref lastSplash, 3)), position, 0.85f);
    }

    public void PlayDying()
    {
        if (oneShots == null)
        {
            return;
        }

        oneShots.PlayOneShot(ProceduralAudio.LanternDying(), 0.65f);
    }

    public void PlayFizzle()
    {
        if (oneShots == null)
        {
            return;
        }

        oneShots.PlayOneShot(ProceduralAudio.FizzleVariant(NextIndex(ref lastFizzle, 3)), 0.7f);
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

        AudioSource source = NextSource();
        source.Stop();
        source.clip = clip;
        source.transform.position = position;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = false;
        source.spatialBlend = 1f;
        source.Play();
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

    AudioSource NextSource()
    {
        int count = pool.Length;
        for (int n = 0; n < count; n++)
        {
            int index = (poolCursor + n) % count;
            if (!pool[index].isPlaying)
            {
                poolCursor = (index + 1) % count;
                return pool[index];
            }
        }

        AudioSource stolen = pool[poolCursor];
        poolCursor = (poolCursor + 1) % count;
        return stolen;
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
