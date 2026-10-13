using UnityEngine;

namespace LanternKeeper
{
// Island 5's rising storm (spec decision 5). The first beacon lit starts a clock; StormSurgeLogic.StartDelay later a dark surge rises
// from the sea up the spire at StormSurgeLogic.RisePerSecond, up to the base of the summit plateau, and every lit beacon pushes it down.
//  - A keeper below the surface is caught: the lantern drains x3 (Lantern.SetDrainModifier), safe light does not protect
//    (SafeLightSuppressed, read by Lantern), the lantern heartbeat plays, the screen edge pulses and the world goes dark and foggy.
//    Bridges and ground are still ground: caught is only the keeper's height against the surface, so climbing out ends it at once.
//  - The HUD (SurgeFeed) shows "Storm rising: N m below" under 25 m. The music tension floor rises from 0.6 to 0.9 as the gap closes.
//  - It stops when the great lamp is lit (or the round ends). A scene reload (retry) builds a new one; nothing here is static state
//    except Instance and the two switches, which OnDestroy resets.
// The look: one dark roiling layer (shader LanternKeeper/StormSurge), a disc built here at runtime with a swell surface and a mist sheet
// in one mesh, one draw call, lit from within by its own lightning (capped by Reduce flashing) and the real Lightning's flashes.
[DefaultExecutionOrder(100)]
public class StormSurge : MonoBehaviour
{
    // The surge stops this far below the plateau (the path's top height), so a keeper on the plateau is never caught.
    public const float CapMargin = 1.5f;
    public const float FadeInSeconds = 6f;
    public const float FadeOutSeconds = 3f;
    const float RiseFollowRate = 6f;
    const float PushFollowRate = 3f;
    const float FogDensityInside = 0.11f;
    static readonly Color FogInside = new Color(0.008f, 0.009f, 0.022f, 1f);
    static readonly Color PulseColour = new Color(0.1f, 0.07f, 0.24f, 1f);
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int FlashId = Shader.PropertyToID("_Flash");
    static readonly int DetailId = Shader.PropertyToID("_Detail");
    static readonly int UTimeId = Shader.PropertyToID("_UTime");

    [SerializeField] Material material;
    [SerializeField] float radius = 220f;
    [SerializeField] int seed = 7705;

    // Test switch: while set the surge neither rises nor catches (like FinaleSequence.TestSpeed). OnDestroy resets it.
    internal static bool DisabledForTests;

    public static StormSurge Instance { get; private set; }
    // Safe light (beacon rings, built bridges) does not protect while the keeper is caught in the surge. Read by Lantern.
    public static bool SafeLightSuppressed => Instance != null && Instance.IsCaught;

    PlayerController player;
    Lantern lantern;
    GreatLamp lamp;
    LowFuelFX lowFuel;
    MusicDirector director;
    Island5Look look;
    LightningSchedule schedule;
    Transform camTransform;
    Transform layer;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Material runtimeMaterial;
    Mesh mesh;
    Light flashLight;
    bool lowQuality;
    bool subscribed;

    float sea;
    bool seaKnown;
    float elapsed;
    float surgeHeight;
    float forcedHeight = float.NaN;
    float visibility;
    float stoppedFor;
    float pulseClock;
    float fogAmount;
    bool fogSaved;
    bool fogRecapture;
    float nextDirectorSearch;
    bool savedFogOn;
    FogMode savedFogMode;
    Color savedFogColour;
    float savedFogDensity;
    float lastFloor = -1f;

    public bool Started { get; private set; }
    public bool Stopped { get; private set; }
    public bool IsCaught { get; private set; }
    // Seconds since the first beacon was lit (0 before it).
    public float Elapsed => elapsed;
    // Surge height above the sea, and its world Y.
    public float Height => surgeHeight;
    public float SurfaceY => sea + surgeHeight;
    public float CapHeight => SpireLayout.PathTopHeight - CapMargin;
    // The keeper's metres above the surge (large when there is no surge yet).
    public float Gap { get; private set; } = 999f;
    // Running: started, not stopped, and the surge has begun to show.
    public bool Active => Started && !Stopped && !DisabledForTests;
    public float Visibility => visibility;
    public float FogAmount => fogAmount;
    public float Flash { get; private set; }
    public Mesh LayerMesh => mesh;
    public int DrawCalls => meshRenderer != null && meshRenderer.enabled ? 1 : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        DisabledForTests = false;
    }

    void Awake()
    {
        Instance = this;
        schedule = new LightningSchedule(seed, new Vector2(2.5f, 5.5f));
        BuildLayer();
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        if (layer != null)
        {
            ApplyQuality();
        }
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        lamp = FindAnyObjectByType<GreatLamp>();
        lowFuel = FindAnyObjectByType<LowFuelFX>();
        director = FindAnyObjectByType<MusicDirector>();
        ResolveLantern();
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            manager.BeaconsChanged += OnBeaconsChanged;
            subscribed = true;
            if (manager.LitCount > 0)
            {
                Begin();
            }
        }

        // The disc is centred on the lamp, which stands at the island's centre.
        if (lamp != null)
        {
            Vector3 centre = lamp.transform.position;
            transform.position = new Vector3(centre.x, transform.position.y, centre.z);
        }
    }

    void OnDestroy()
    {
        if (subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.BeaconsChanged -= OnBeaconsChanged;
        }

        ReleaseCaught();
        RestoreFog();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.HeartbeatOverride = false;
        }

        if (Instance == this)
        {
            Instance = null;
        }

        DisabledForTests = false;
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    void OnBeaconsChanged(int lit, int total)
    {
        if (lit > 0)
        {
            Begin();
        }
    }

    void Begin()
    {
        if (Started || DisabledForTests)
        {
            return;
        }

        Started = true;
        elapsed = 0f;
        surgeHeight = 0f;
    }

    void ResolveLantern()
    {
        if (lantern == null)
        {
            lantern = player != null ? player.GetComponentInChildren<Lantern>() : null;
            if (lantern == null)
            {
                lantern = FindAnyObjectByType<Lantern>();
            }
        }
    }

    void ResolveSea()
    {
        if (seaKnown)
        {
            return;
        }

        RingZones zones = RingZones.Instance;
        if (zones != null && zones.Sea > 0f)
        {
            sea = zones.Sea;
            seaKnown = true;
        }
        else if (WaterHazard.SurfaceY > 0f)
        {
            sea = WaterHazard.SurfaceY;
            seaKnown = true;
        }
    }

    // Test hooks. The surge clock (seconds since the first beacon, starting the surge if it has not started) and a held height.
    internal void SetElapsedForTests(float seconds)
    {
        if (!Started && !DisabledForTests)
        {
            Started = true;
        }

        elapsed = Mathf.Max(0f, seconds);
        surgeHeight = TargetHeight();
    }

    // Holds the surge at this height above the sea (NaN releases it). Starts the surge if it has not started.
    internal void HoldHeightForTests(float heightAboveSea)
    {
        if (!Started && !DisabledForTests && !float.IsNaN(heightAboveSea))
        {
            Started = true;
        }

        forcedHeight = heightAboveSea;
        if (!float.IsNaN(heightAboveSea))
        {
            surgeHeight = heightAboveSea;
            visibility = 1f;
        }
    }

    float TargetHeight()
    {
        if (!float.IsNaN(forcedHeight))
        {
            return forcedHeight;
        }

        GameManager manager = GameManager.Instance;
        int lit = manager != null ? manager.LitCount : 0;
        return StormSurgeLogic.Height(elapsed, lit, CapHeight);
    }

    void Update()
    {
        if (DisabledForTests)
        {
            ReleaseCaught();
            visibility = 0f;
            UpdateLayer();
            return;
        }

        ResolveSea();
        ResolveLantern();
        GameManager manager = GameManager.Instance;
        bool roundOver = manager != null && manager.IsRoundOver;
        if (!Stopped && Started && ((lamp != null && lamp.Lit) || roundOver))
        {
            Stopped = true;
            stoppedFor = 0f;
        }

        float dt = Time.deltaTime;
        if (Started && !Stopped && dt > 0f && (manager == null || !manager.IsDying))
        {
            elapsed += dt;
            float target = TargetHeight();
            float rate = target < surgeHeight ? PushFollowRate : RiseFollowRate;
            surgeHeight = Mathf.MoveTowards(surgeHeight, target, rate * dt);
        }

        UpdateVisibility(dt);
        UpdateCaught();
        UpdateFlash(dt);
        UpdateMusic();
        UpdateLayer();
    }

    void LateUpdate()
    {
        UpdateFog();
    }

    void UpdateVisibility(float dt)
    {
        if (!Started)
        {
            visibility = 0f;
            return;
        }

        if (Stopped)
        {
            stoppedFor += dt;
            visibility = Mathf.Min(visibility, 1f - Mathf.Clamp01(stoppedFor / FadeOutSeconds));
            return;
        }

        if (!float.IsNaN(forcedHeight))
        {
            visibility = 1f;
            return;
        }

        float fadeStart = StormSurgeLogic.StartDelay - FadeInSeconds;
        visibility = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - fadeStart) / FadeInSeconds));
    }

    void UpdateCaught()
    {
        bool caught = false;
        if (Active && player != null && seaKnown)
        {
            float keeperY = player.transform.position.y;
            float surfaceY = SurfaceY;
            Gap = StormSurgeLogic.Gap(keeperY, surfaceY);
            caught = surgeHeight > 0.01f && StormSurgeLogic.Caught(keeperY, surfaceY);
        }
        else
        {
            Gap = 999f;
        }

        if (caught == IsCaught)
        {
            if (caught)
            {
                PulseWhileCaught();
            }

            return;
        }

        IsCaught = caught;
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, caught ? StormSurgeLogic.CaughtDrain : 1f);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.HeartbeatOverride = caught;
        }

        pulseClock = 0f;
        if (caught)
        {
            PulseWhileCaught();
        }
    }

    // Stops being caught without a height change (disabled for tests, the lamp lit, the lantern replaced).
    void ReleaseCaught()
    {
        if (!IsCaught)
        {
            return;
        }

        IsCaught = false;
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, 1f);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.HeartbeatOverride = false;
        }
    }

    void PulseWhileCaught()
    {
        // A dark violet pulse on the screen edge with every heartbeat-ish second (every other second with Reduce flashing).
        pulseClock -= Time.deltaTime;
        if (pulseClock > 0f)
        {
            return;
        }

        pulseClock = SettingsMath.MothPulsePeriod(UserSettings.ReduceFlashing);
        if (lowFuel == null)
        {
            lowFuel = FindAnyObjectByType<LowFuelFX>();
        }

        if (lowFuel != null)
        {
            lowFuel.PulseEdge(0.55f, PulseColour);
        }
    }

    void UpdateFlash(float dt)
    {
        float cap = SettingsMath.FlashCap(UserSettings.ReduceFlashing);
        float flash = 0f;
        if (Started && !Stopped)
        {
            schedule.Tick(dt, false, -1f, -1f);
            flash = schedule.Flash01 * cap;
            flash = Mathf.Max(flash, Lightning.CurrentFlash);
            if (IsCaught)
            {
                // Inside it the surge flickers, never beyond the Reduce-flashing cap.
                flash = Mathf.Max(flash, cap * 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f) * Mathf.Sin(Time.time * 3.1f)));
            }
        }

        Flash = Mathf.Clamp01(flash);
    }

    void UpdateMusic()
    {
        if (director == null)
        {
            // Cached once found; while there is none (stripped test scenes) the search is retried at most every 2 s.
            if (Time.unscaledTime < nextDirectorSearch)
            {
                return;
            }

            director = FindAnyObjectByType<MusicDirector>();
            if (director == null)
            {
                nextDirectorSearch = Time.unscaledTime + 2f;
                return;
            }
        }

        float ringFloor = RingLogic.TensionFloor(RingZones.Reached);
        if (Active && Gap < 900f)
        {
            float floor = StormSurgeLogic.MusicFloor(Gap, ringFloor);
            if (Gap <= StormSurgeLogic.MusicGap || !Mathf.Approximately(lastFloor, floor))
            {
                director.SetTensionFloor(floor);
                lastFloor = floor;
            }
        }
        else if (Stopped && lastFloor >= 0f)
        {
            // The lamp is lit: back to the ring floor.
            director.SetTensionFloor(ringFloor);
            lastFloor = -1f;
        }
    }

    void UpdateFog()
    {
        float target = 0f;
        if (!DisabledForTests && Started && visibility > 0.01f && surgeHeight > 0.01f)
        {
            if (camTransform == null && Camera.main != null)
            {
                camTransform = Camera.main.transform;
            }

            if (camTransform != null)
            {
                Vector3 cam = camTransform.position;
                float flat = Vector2.Distance(new Vector2(cam.x, cam.z), new Vector2(transform.position.x, transform.position.z));
                if (flat < radius * 0.55f)
                {
                    target = Mathf.Clamp01((SurfaceY - cam.y) / 1.5f) * visibility;
                }
            }
        }

        fogAmount = Mathf.MoveTowards(fogAmount, target, Time.unscaledDeltaTime * 3f);
        if (fogAmount <= 0.001f)
        {
            RestoreFog();
            return;
        }

        if (fogSaved && fogRecapture)
        {
            // A quality change re-applied the level's fog while the surge held it: the saved values are stale, take them again.
            fogSaved = false;
        }

        fogRecapture = false;
        if (!fogSaved)
        {
            fogSaved = true;
            savedFogOn = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColour = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.Lerp(savedFogColour, FogInside, fogAmount);
        RenderSettings.fogDensity = Mathf.Lerp(savedFogDensity, FogDensityInside, fogAmount);
    }

    void RestoreFog()
    {
        if (!fogSaved)
        {
            return;
        }

        fogSaved = false;
        if (!fogRecapture)
        {
            RenderSettings.fog = savedFogOn;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogDensity = savedFogDensity;
        }

        fogRecapture = false;
        // The colour goes back to Island5Look's current target, which may have moved with the ring while the surge held the fog.
        if (look == null)
        {
            look = FindAnyObjectByType<Island5Look>();
        }

        if (look != null)
        {
            look.Reapply();
        }
        else
        {
            RenderSettings.fogColor = savedFogColour;
        }
    }

    // ---- the layer ----

    void OnQuality(GraphicsProfile profile)
    {
        fogRecapture = true;
        ApplyQuality();
    }

    void ApplyQuality()
    {
        bool low = GraphicsQuality.Current == GraphicsLevel.Low;
        if (low == lowQuality && mesh != null)
        {
            return;
        }

        lowQuality = low;
        BuildMesh();
        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(DetailId, lowQuality ? 0f : 1f);
        }

        if (flashLight != null)
        {
            flashLight.gameObject.SetActive(!lowQuality);
        }
    }

    void BuildLayer()
    {
        GameObject child = new GameObject("SurgeLayer");
        child.transform.SetParent(transform, false);
        layer = child.transform;
        meshFilter = child.AddComponent<MeshFilter>();
        meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (material != null)
        {
            runtimeMaterial = new Material(material);
            meshRenderer.sharedMaterial = runtimeMaterial;
        }

        meshRenderer.enabled = false;

        GameObject lightObject = new GameObject("SurgeFlash");
        lightObject.transform.SetParent(transform, false);
        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(0.55f, 0.62f, 1f);
        flashLight.range = 60f;
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
        flashLight.enabled = false;
        lowQuality = GraphicsQuality.Current == GraphicsLevel.Low;
        BuildMesh();
        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(DetailId, lowQuality ? 0f : 1f);
        }

        flashLight.gameObject.SetActive(!lowQuality);
    }

    // A polar grid, twice (the surface at y 0 and a mist sheet 1.2 m above), as one mesh. Vertex colour r = layer, g = radius 0..1.
    void BuildMesh()
    {
        int sectors = lowQuality ? 32 : 64;
        int rings = lowQuality ? 8 : 14;
        int perLayer = 1 + rings * sectors;
        Vector3[] vertices = new Vector3[perLayer * 2];
        Color[] colours = new Color[perLayer * 2];
        int[] triangles = new int[(sectors + (rings - 1) * sectors * 2) * 3 * 2];
        int t = 0;
        for (int l = 0; l < 2; l++)
        {
            int offset = l * perLayer;
            float y = l * 1.2f;
            vertices[offset] = new Vector3(0f, y, 0f);
            colours[offset] = new Color(l, 0f, 0f, 1f);
            for (int r = 1; r <= rings; r++)
            {
                float f = r / (float)rings;
                float rad = radius * Mathf.Pow(f, 1.5f);
                for (int s = 0; s < sectors; s++)
                {
                    float a = s / (float)sectors * Mathf.PI * 2f;
                    int index = offset + 1 + (r - 1) * sectors + s;
                    vertices[index] = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
                    colours[index] = new Color(l, rad / radius, 0f, 1f);
                }
            }

            for (int s = 0; s < sectors; s++)
            {
                int next = (s + 1) % sectors;
                triangles[t++] = offset;
                triangles[t++] = offset + 1 + next;
                triangles[t++] = offset + 1 + s;
            }

            for (int r = 1; r < rings; r++)
            {
                for (int s = 0; s < sectors; s++)
                {
                    int next = (s + 1) % sectors;
                    int a0 = offset + 1 + (r - 1) * sectors + s;
                    int a1 = offset + 1 + (r - 1) * sectors + next;
                    int b0 = offset + 1 + r * sectors + s;
                    int b1 = offset + 1 + r * sectors + next;
                    triangles[t++] = a0;
                    triangles[t++] = a1;
                    triangles[t++] = b0;
                    triangles[t++] = a1;
                    triangles[t++] = b1;
                    triangles[t++] = b0;
                }
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "StormSurgeDisc" };
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(radius * 2f, 8f, radius * 2f));
        meshFilter.sharedMesh = mesh;
    }

    void UpdateLayer()
    {
        bool show = visibility > 0.01f && Started;
        if (meshRenderer != null && meshRenderer.enabled != show)
        {
            meshRenderer.enabled = show;
        }

        if (!show)
        {
            if (flashLight != null && flashLight.enabled)
            {
                flashLight.enabled = false;
            }

            return;
        }

        Vector3 position = transform.position;
        position.y = SurfaceY + 0.12f;
        transform.position = position;
        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(AlphaId, visibility);
            runtimeMaterial.SetFloat(FlashId, Flash);
            runtimeMaterial.SetFloat(UTimeId, Time.unscaledTime);
        }

        if (flashLight != null && flashLight.gameObject.activeSelf)
        {
            bool on = Flash > 0.02f;
            flashLight.enabled = on;
            if (on)
            {
                Vector3 centre = player != null ? player.transform.position : position;
                flashLight.transform.position = new Vector3(centre.x, SurfaceY + 4f, centre.z);
                flashLight.intensity = 6f * Flash;
            }
        }
    }
}
}
