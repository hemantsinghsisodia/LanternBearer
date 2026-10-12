using UnityEngine;

namespace LanternKeeper
{
// The storm wall that circles Island 5 far out at sea (spec section 3, "Sky and storm"): a band of cloud cards at 380 m, 20 to 140 m above
// the sea, lit from within by distant lightning. One mesh, one material, one draw call; the Low graphics level uses a mesh with half the
// cards. It is visible during normal play, from every ring (a little thinner at the Low ring, full at the summit). In the finale the
// FinaleSequence opens it: SetOpen(0..1) grows a gap, by a dissolve threshold in the shader, from the beam's bearing until the wall is gone.
// The flicker only reuses the LightningSchedule timing (its own seed); the real Lightning rig on the ring stays what it is.
public class StormRing : MonoBehaviour
{
    public const float Radius = 380f;
    public const float MinHeight = 20f;
    public const float MaxHeight = 140f;
    // Per ring (Low, Mid, High, Summit): the wall thickens as the keeper climbs.
    static readonly float[] DensityByRing = { 0.6f, 0.75f, 0.9f, 1f };

    static readonly int OpenId = Shader.PropertyToID("_Open");
    static readonly int GapCenterId = Shader.PropertyToID("_GapCenter");
    static readonly int FlashId = Shader.PropertyToID("_Flash");
    static readonly int FlashAngleId = Shader.PropertyToID("_FlashAngle");
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int WarmId = Shader.PropertyToID("_Warm");

    [SerializeField] MeshFilter meshFilter;
    [SerializeField] MeshRenderer ringRenderer;
    [SerializeField] Mesh meshFull;
    [SerializeField] Mesh meshLow;
    [SerializeField] int cardsFull = 48;
    [SerializeField] int cardsLow = 24;
    [SerializeField] int seed = 5505;

    Material material;
    LightningSchedule schedule;
    float flashAngle = 0.3f;
    float density;
    float open;
    float gapCenter;
    float warm;
    bool low;
    bool ready;

    // The number of cards in the mesh now in use.
    public int ActiveCards => low ? cardsLow : cardsFull;
    // 0 closed, 1 fully open (the renderer is then switched off).
    public float Open => open;
    public float GapCenter => gapCenter;
    public Mesh ActiveMesh => meshFilter != null ? meshFilter.sharedMesh : null;

    void Awake()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (ringRenderer == null)
        {
            ringRenderer = GetComponent<MeshRenderer>();
        }

        if (ringRenderer != null && ringRenderer.sharedMaterial != null)
        {
            // One material instance for the per-frame values (the shared asset is never written at runtime).
            material = new Material(ringRenderer.sharedMaterial);
            ringRenderer.sharedMaterial = material;
        }

        schedule = new LightningSchedule(seed, new Vector2(2.5f, 7f));
        density = DensityByRing[0];
        ApplyQuality();
        ready = true;
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        if (ready)
        {
            ApplyQuality();
        }
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }

    void OnQuality(GraphicsProfile profile)
    {
        ApplyQuality();
    }

    void ApplyQuality()
    {
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        if (meshFilter != null)
        {
            Mesh wanted = low && meshLow != null ? meshLow : meshFull;
            if (meshFilter.sharedMesh != wanted)
            {
                meshFilter.sharedMesh = wanted;
            }
        }
    }

    // The finale: `open01` 0..1 and the bearing (0..1 clockwise from +Z) the gap starts from.
    public void SetOpen(float open01, float bearing01)
    {
        open = Mathf.Clamp01(open01);
        gapCenter = Mathf.Repeat(bearing01, 1f);
    }

    // Sunrise tint 0..1 (the wall is gone by then in the finale; kept for a partial open).
    public void SetWarm(float warm01)
    {
        warm = Mathf.Clamp01(warm01);
    }

    // Bearing 0..1 (clockwise from +Z) of a direction in XZ.
    public static float Bearing01(Vector3 direction)
    {
        return Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) / (2f * Mathf.PI), 1f);
    }

    void Update()
    {
        if (!ready || material == null || ringRenderer == null)
        {
            return;
        }

        bool gone = open >= 1f;
        if (ringRenderer.enabled == gone)
        {
            ringRenderer.enabled = !gone;
        }

        if (gone)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        schedule.Tick(dt, false, -1f, -1f);
        if (schedule.FlashedThisTick)
        {
            // A new strike a golden-ratio step round the wall: no allocation, no two in a row close together.
            flashAngle = Mathf.Repeat(flashAngle + 0.381966f, 1f);
        }

        // The wall thickens with the ring; the round over (the finale) holds it.
        GameManager manager = GameManager.Instance;
        bool over = manager != null && manager.IsRoundOver;
        float target = over ? 1f : DensityByRing[Mathf.Clamp((int)RingZones.Reached, 0, 3)];
        density = Mathf.MoveTowards(density, target, dt * 0.25f);

        material.SetFloat(OpenId, open);
        material.SetFloat(GapCenterId, gapCenter);
        material.SetFloat(FlashId, schedule.Flash01 * SettingsMath.FlashCap(UserSettings.ReduceFlashing));
        material.SetFloat(FlashAngleId, flashAngle);
        material.SetFloat(DensityId, density);
        material.SetFloat(WarmId, warm);
    }
}
}
