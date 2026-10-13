using UnityEngine;

namespace LanternKeeper
{
// Island 5's hunting lightning (spec: "Lightning hunts you"). Switched on by RingHazards in the High ring and active until the great lamp
// is lit. Every 6-10 s (seeded) it picks a point 2-4 m from where the keeper will be in a second, shows a cold-blue warning ring on the
// ground for 1.0 s, then strikes: the Lightning flash and thunder crack, and a keeper within 2.5 m loses 15% of the lantern and is knocked
// back 3 m. A keeper on a built bridge or inside a lit beacon's safe ring is never targeted and never hit, even while the storm surge is
// catching them (the shelter checks are the geometry only, not Lantern's drain state).
// Reduce flashing: the pulse of the ring is shallower, and the flash itself is capped by Lightning (SettingsMath.FlashCap).
public class LightningHunter : MonoBehaviour
{
    const int Sectors = 32;
    const int Rows = 5;
    const float GroundOffset = 0.25f;
    static readonly Color RingColour = new Color(0.737f, 0.824f, 1f, 1f);
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int PulseId = Shader.PropertyToID("_Pulse");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int ColourId = Shader.PropertyToID("_Color");

    [SerializeField] int seed = 5506;
    [SerializeField] Material warningMaterial;

    // Test switch: while set the hunter neither schedules nor strikes. OnDestroy resets it (a scene reload resets it too).
    internal static bool DisabledForTests;

    public static LightningHunter Instance { get; private set; }

    System.Random random;
    PlayerController player;
    Lantern lantern;
    GreatLamp lamp;
    LowFuelFX lowFuel;
    float countdown;
    float warnLeft;
    Vector3 lastKeeper;
    Vector3 keeperVelocity;
    bool haveKeeper;
    GameObject ringObject;
    MeshRenderer ringRenderer;
    Mesh ringMesh;
    Material runtimeMaterial;
    Light strikeLight;
    float lightLeft;

    public bool WarningActive { get; private set; }
    public Vector3 StrikePoint { get; private set; }
    public int Strikes { get; private set; }
    public int Hits { get; private set; }
    // Strikes that came due while the keeper (or the point) was sheltered and were dropped.
    public int Skipped { get; private set; }
    public float SecondsToNextStrike => countdown;
    // 0..1 through the warning.
    public float WarnProgress => WarningActive ? 1f - warnLeft / StrikeLogic.WarnSeconds : 0f;
    public bool RingVisible => ringRenderer != null && ringRenderer.enabled;
    // How deep the ring pulses: 1, or a soft 0.3 with Reduce flashing.
    public float PulseDepth => UserSettings.ReduceFlashing ? 0.3f : 1f;
    public Mesh RingMesh => ringMesh;
    public Material RingMaterial => runtimeMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        DisabledForTests = false;
        // The hunter sits switched off until the High ring, so its own OnDestroy may never run: the switch also resets when a scene unloads.
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
    {
        DisabledForTests = false;
    }

    void Awake()
    {
        Instance = this;
        random = new System.Random(seed);
        countdown = StrikeLogic.Interval(random);
        BuildRing();
    }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        DisabledForTests = false;
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        if (ringMesh != null)
        {
            Destroy(ringMesh);
        }
    }

    void Start()
    {
        Resolve();
    }

    void Resolve()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        if (lantern == null)
        {
            lantern = player != null ? player.GetComponentInChildren<Lantern>() : null;
            if (lantern == null)
            {
                lantern = FindAnyObjectByType<Lantern>();
            }
        }

        if (lamp == null)
        {
            lamp = FindAnyObjectByType<GreatLamp>();
        }
    }

    // Safe light as the strike sees it: a built bridge, or a lit beacon's ring. Geometry only, so the storm surge's "safe light does not
    // protect" (Lantern) never exposes a keeper standing in a lit ring.
    public static bool Sheltered(Vector3 position)
    {
        if (LightBridge.AnySafe(position))
        {
            return true;
        }

        for (int i = 0; i < Beacon.All.Count; i++)
        {
            Beacon beacon = Beacon.All[i];
            if (beacon != null && beacon.ContainsSafe(position))
            {
                return true;
            }
        }

        return false;
    }

    // Picks a target and starts the warning. False, and nothing started, while the keeper (or the point) is sheltered or a strike is pending.
    public bool TryTarget()
    {
        Resolve();
        if (player == null || WarningActive)
        {
            return false;
        }

        Vector3 keeper = player.transform.position;
        if (Sheltered(keeper))
        {
            return false;
        }

        Vector3 point = StrikeLogic.Target(keeper, keeperVelocity, random);
        if (Sheltered(point))
        {
            return false;
        }

        BeginWarning(point);
        return true;
    }

    // Starts the warning ring at `point` now.
    public void BeginWarning(Vector3 point)
    {
        float height;
        Vector3 normal;
        if (TerrainQuery.TrySample(point, out height, out normal))
        {
            point.y = height;
        }

        StrikePoint = point;
        WarningActive = true;
        warnLeft = StrikeLogic.WarnSeconds;
        FitRing(point);
        ringRenderer.enabled = true;
        ApplyRing(0f);
    }

    // Test hook: the seconds until the next strike is picked.
    public void SetCountdownForTests(float seconds)
    {
        countdown = Mathf.Max(0f, seconds);
    }

    void CancelWarning()
    {
        WarningActive = false;
        if (ringRenderer != null)
        {
            ringRenderer.enabled = false;
        }
    }

    void Update()
    {
        UpdateLight();
        if (DisabledForTests)
        {
            CancelWarning();
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && manager.IsPaused)
        {
            return;
        }

        Resolve();
        if (player == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        TrackKeeper(dt);
        bool over = manager != null && (manager.IsRoundOver || manager.IsDying);
        if (over || (lamp != null && lamp.Lit))
        {
            CancelWarning();
            return;
        }

        if (WarningActive)
        {
            warnLeft -= dt;
            ApplyRing(Mathf.Clamp01(1f - warnLeft / StrikeLogic.WarnSeconds));
            if (warnLeft <= 0f)
            {
                Impact();
            }

            return;
        }

        countdown -= dt;
        if (countdown <= 0f)
        {
            // A sheltered keeper is skipped; the next strike is a fresh interval away.
            if (!TryTarget())
            {
                Skipped++;
                countdown = StrikeLogic.Interval(random);
            }
        }
    }

    void TrackKeeper(float dt)
    {
        Vector3 now = player.transform.position;
        if (haveKeeper && dt > 0f)
        {
            Vector3 instant = (now - lastKeeper) / dt;
            // A teleport or a fall is not a walking direction.
            if (instant.magnitude < 12f)
            {
                keeperVelocity = Vector3.Lerp(keeperVelocity, instant, 1f - Mathf.Exp(-12f * dt));
            }
        }

        lastKeeper = now;
        haveKeeper = true;
    }

    // The strike lands. Public so tests can end a warning at once.
    public void Impact()
    {
        CancelWarning();
        countdown = StrikeLogic.Interval(random);
        Strikes++;
        Lightning storm = Lightning.Instance;
        if (storm != null)
        {
            storm.StrikeFlash();
        }

        StartLight();
        Resolve();
        if (player == null)
        {
            return;
        }

        Vector3 keeper = player.transform.position;
        if (Sheltered(keeper) || !StrikeLogic.Hits(keeper, StrikePoint))
        {
            return;
        }

        Hits++;
        if (lantern != null)
        {
            lantern.Hit(lantern.MaxFuel * StrikeLogic.FuelFraction);
        }

        player.Knockback(StrikeLogic.KnockDirection(keeper, StrikePoint, player.transform.forward), StrikeLogic.KnockDistance);
        if (lowFuel == null)
        {
            lowFuel = FindAnyObjectByType<LowFuelFX>();
        }

        if (lowFuel != null)
        {
            lowFuel.PulseEdge(0.6f, RingColour);
        }
    }

    // ---- the cue: a cold light at the impact, capped by Reduce flashing ----

    const float LightSeconds = 0.3f;

    void StartLight()
    {
        if (strikeLight == null)
        {
            GameObject host = new GameObject("StrikeLight");
            host.transform.SetParent(transform, false);
            strikeLight = host.AddComponent<Light>();
            strikeLight.type = LightType.Point;
            strikeLight.color = new Color(0.78f, 0.86f, 1f);
            strikeLight.range = 18f;
            strikeLight.shadows = LightShadows.None;
            strikeLight.enabled = false;
        }

        strikeLight.transform.position = StrikePoint + Vector3.up * 2.5f;
        lightLeft = LightSeconds;
    }

    // Peak intensity is scaled by the same cap as the Lightning flash.
    public static float LightPeak => 14f * SettingsMath.FlashCap(UserSettings.ReduceFlashing);
    public float StrikeLightIntensity => strikeLight != null && strikeLight.enabled ? strikeLight.intensity : 0f;

    void UpdateLight()
    {
        if (strikeLight == null)
        {
            return;
        }

        if (lightLeft > 0f)
        {
            lightLeft -= Time.deltaTime;
            strikeLight.enabled = lightLeft > 0f;
            strikeLight.intensity = LightPeak * Mathf.Clamp01(lightLeft / LightSeconds);
        }
        else if (strikeLight.enabled)
        {
            strikeLight.enabled = false;
        }
    }

    // ---- the warning ring ----

    void BuildRing()
    {
        ringObject = new GameObject("StrikeWarning");
        ringObject.transform.SetParent(transform, false);
        ringMesh = new Mesh { name = "StrikeWarningDisc" };
        ringMesh.MarkDynamic();
        MeshFilter filter = ringObject.AddComponent<MeshFilter>();
        filter.sharedMesh = ringMesh;
        ringRenderer = ringObject.AddComponent<MeshRenderer>();
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ringRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        ringRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        Shader shader = warningMaterial != null ? warningMaterial.shader : Shader.Find("LanternKeeper/StrikeWarning");
        if (warningMaterial != null)
        {
            runtimeMaterial = new Material(warningMaterial);
        }
        else if (shader != null)
        {
            runtimeMaterial = new Material(shader);
        }

        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetColor(ColourId, RingColour);
            ringRenderer.sharedMaterial = runtimeMaterial;
        }

        ringRenderer.enabled = false;
        BuildGrid();
    }

    // A polar grid on the unit disc; FitRing moves each vertex onto the ground.
    void BuildGrid()
    {
        int count = 1 + Rows * Sectors;
        Vector3[] vertices = new Vector3[count];
        Vector2[] uvs = new Vector2[count];
        int[] triangles = new int[(Sectors + (Rows - 1) * Sectors * 2) * 3];
        uvs[0] = Vector2.zero;
        for (int r = 1; r <= Rows; r++)
        {
            float f = r / (float)Rows;
            for (int s = 0; s < Sectors; s++)
            {
                float a = s / (float)Sectors * Mathf.PI * 2f;
                uvs[1 + (r - 1) * Sectors + s] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * f;
            }
        }

        int t = 0;
        for (int s = 0; s < Sectors; s++)
        {
            int next = (s + 1) % Sectors;
            triangles[t++] = 0;
            triangles[t++] = 1 + next;
            triangles[t++] = 1 + s;
        }

        for (int r = 1; r < Rows; r++)
        {
            for (int s = 0; s < Sectors; s++)
            {
                int next = (s + 1) % Sectors;
                int a0 = 1 + (r - 1) * Sectors + s;
                int a1 = 1 + (r - 1) * Sectors + next;
                int b0 = 1 + r * Sectors + s;
                int b1 = 1 + r * Sectors + next;
                triangles[t++] = a0;
                triangles[t++] = a1;
                triangles[t++] = b0;
                triangles[t++] = a1;
                triangles[t++] = b1;
                triangles[t++] = b0;
            }
        }

        ringMesh.vertices = vertices;
        ringMesh.uv = uvs;
        ringMesh.triangles = triangles;
        ringMesh.bounds = new Bounds(Vector3.zero, new Vector3(StrikeLogic.HitRadius * 2f, 40f, StrikeLogic.HitRadius * 2f));
    }

    void FitRing(Vector3 centre)
    {
        ringObject.transform.SetPositionAndRotation(centre, Quaternion.identity);
        Vector3[] vertices = ringMesh.vertices;
        Vector2[] uvs = ringMesh.uv;
        vertices[0] = new Vector3(0f, GroundOffset, 0f);
        for (int i = 1; i < vertices.Length; i++)
        {
            Vector3 local = new Vector3(uvs[i].x, 0f, uvs[i].y) * StrikeLogic.HitRadius;
            float height;
            Vector3 normal;
            float y = TerrainQuery.TrySample(centre + local, out height, out normal) ? height - centre.y : 0f;
            local.y = y + GroundOffset;
            vertices[i] = local;
        }

        // The centre follows the ground too.
        float centreHeight;
        Vector3 centreNormal;
        if (TerrainQuery.TrySample(centre, out centreHeight, out centreNormal))
        {
            vertices[0].y = centreHeight - centre.y + GroundOffset;
        }

        ringMesh.vertices = vertices;
        ringMesh.RecalculateBounds();
    }

    void ApplyRing(float progress)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(ProgressId, progress);
        runtimeMaterial.SetFloat(PulseId, PulseDepth);
        runtimeMaterial.SetFloat(AlphaId, UserSettings.ReduceFlashing ? 0.7f : 1f);
    }
}
}
