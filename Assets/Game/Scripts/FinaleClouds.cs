using UnityEngine;

namespace LanternKeeper
{
// The Island 5 finale's dramatic clouds (H4 spec, "Dramatic clouds"): three depth layers of soft cloud banks (near 250 m, mid 450 m, far 700 m,
// one mesh and one material each) and a set of god rays (one mesh, one material). They are finale only: everything is switched off until the
// storm starts to break, and the gameplay StormRing is untouched (in the finale it fades out as these fade in, FinaleCloudMath.CrossFade).
//   - the break (FinaleTimeline BeamAndStorm): the banks drift apart from the gap bearing, the sky behind shows;
//   - the islands phase: the gap drifts round to the sun's bearing (FinaleCloudMath.GapBearing);
//   - the sunrise: the cloud edges facing the sun catch gold and pink, the undersides stay violet, the far layer glows at the horizon, and the god
//     rays come up (never on Low, and capped under Reduce flashing). The sun is the live dawn light, so the sunrise reworks do not move these.
// Low graphics: only the mid layer, no rays. FinaleSequence calls Tick(t) every frame with the finale time. Unscaled time, no allocation.
public class FinaleClouds : MonoBehaviour
{
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int DawnId = Shader.PropertyToID("_Dawn");
    static readonly int OpenId = Shader.PropertyToID("_Open");
    static readonly int GapScaleId = Shader.PropertyToID("_GapScale");
    static readonly int GapCenterId = Shader.PropertyToID("_GapCenter");
    static readonly int SunDirId = Shader.PropertyToID("_SunDir");
    static readonly int UTimeId = Shader.PropertyToID("_UTime");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");

    // The mid layer is the one Low keeps.
    public const int LowLayerIndex = 1;
    // Island 5's dawn sun elevation if there is no light to read.
    const float FallbackElevation = 10f;

    // Near, mid, far.
    [SerializeField] MeshRenderer[] layers = new MeshRenderer[FinaleCloudMath.FullLayers];
    [SerializeField] MeshRenderer rays;
    [SerializeField] DawnSequence dawn;

    Material[] materials;
    Material rayMaterial;
    bool low;
    bool ready;
    bool shown;
    float beamBearing;
    float lastTime;
    Light sunLight;
    float rayStrength;

    public int LayerCount => layers != null ? layers.Length : 0;
    // The layers drawn at the current quality (3, or 1 on Low).
    public int ActiveLayers => FinaleCloudMath.LayerCount(low);
    public bool RaysEnabled => FinaleCloudMath.RaysEnabled(low);
    // The god ray strength last written (0 until the sunrise, capped under Reduce flashing).
    public float RayStrength => rayStrength;
    public bool Shown => shown;
    public MeshRenderer RaysRenderer => rays;

    public MeshRenderer LayerRenderer(int index)
    {
        return layers[index];
    }

    // Draw calls and triangles the clouds cost when fully shown at the current quality.
    public void Cost(out int drawCalls, out int triangles)
    {
        drawCalls = 0;
        triangles = 0;
        for (int i = 0; i < LayerCount; i++)
        {
            if (LayerUsed(i))
            {
                drawCalls++;
                triangles += TrianglesOf(layers[i]);
            }
        }

        if (RaysEnabled && rays != null)
        {
            drawCalls++;
            triangles += TrianglesOf(rays);
        }
    }

    static int TrianglesOf(MeshRenderer renderer)
    {
        MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        return filter != null && filter.sharedMesh != null ? filter.sharedMesh.triangles.Length / 3 : 0;
    }

    bool LayerUsed(int index)
    {
        return !low || index == LowLayerIndex;
    }

    void Awake()
    {
        materials = new Material[LayerCount];
        for (int i = 0; i < LayerCount; i++)
        {
            if (layers[i] != null && layers[i].sharedMaterial != null)
            {
                // One material instance each for the per-frame values (the shared assets are never written at runtime).
                materials[i] = new Material(layers[i].sharedMaterial);
                layers[i].sharedMaterial = materials[i];
            }
        }

        if (rays != null && rays.sharedMaterial != null)
        {
            rayMaterial = new Material(rays.sharedMaterial);
            rays.sharedMaterial = rayMaterial;
        }

        if (dawn == null)
        {
            dawn = FindAnyObjectByType<DawnSequence>();
        }

        ApplyQuality();
        SetShown(false);
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
        for (int i = 0; materials != null && i < materials.Length; i++)
        {
            if (materials[i] != null)
            {
                Destroy(materials[i]);
            }
        }

        if (rayMaterial != null)
        {
            Destroy(rayMaterial);
        }
    }

    void OnQuality(GraphicsProfile profile)
    {
        ApplyQuality();
        if (shown)
        {
            SetShown(true);
            Tick(lastTime);
        }
    }

    void ApplyQuality()
    {
        low = GraphicsQuality.Current == GraphicsLevel.Low;
    }

    // The bearing (0..1 clockwise from +Z) the beam points at when the storm starts to break.
    public void SetBeamBearing(float bearing01)
    {
        beamBearing = Mathf.Repeat(bearing01, 1f);
    }

    void SetShown(bool on)
    {
        shown = on;
        for (int i = 0; i < LayerCount; i++)
        {
            if (layers[i] != null)
            {
                layers[i].enabled = on && LayerUsed(i);
            }
        }

        if (rays != null)
        {
            rays.enabled = on && RaysEnabled && rayStrength > 0.001f;
        }
    }

    // The direction towards the sun: the live dawn light (the one that moves the sky's sun), or a fixed one if the scene has none.
    Vector3 SunDirection()
    {
        if (sunLight == null && dawn != null)
        {
            sunLight = dawn.SunLight;
        }

        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
        }

        if (sunLight != null)
        {
            Vector3 toward = -sunLight.transform.forward;
            if (toward.y > 0.02f)
            {
                return toward;
            }
        }

        float bearing = FinaleCamera.SunBearing * Mathf.Deg2Rad;
        float elevation = FallbackElevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(bearing) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(bearing) * Mathf.Cos(elevation));
    }

    // The bearing the gap ends at: the dawn sun's.
    float SunBearing01()
    {
        if (dawn != null)
        {
            return StormRing.Bearing01(dawn.DawnSunDirection);
        }

        return StormRing.Bearing01(SunDirection());
    }

    // Called by FinaleSequence every frame with the finale time (unscaled seconds since the lamp was lit).
    public void Tick(float t)
    {
        if (!ready)
        {
            return;
        }

        lastTime = t;
        float fade = FinaleCloudMath.CrossFade(t);
        bool visible = fade > 0.001f;
        float strength = FinaleCloudMath.RayStrength(t, UserSettings.ReduceFlashing, low);
        rayStrength = strength;
        if (visible != shown || (rays != null && rays.enabled != (visible && RaysEnabled && strength > 0.001f)))
        {
            SetShown(visible);
        }

        if (!visible)
        {
            return;
        }

        Vector3 sun = SunDirection();
        Vector4 sunVector = new Vector4(sun.x, sun.y, sun.z, 0f);
        float open = FinaleCloudMath.Break01(t);
        float dawn01 = FinaleCloudMath.Dawn01(t);
        float gapScale = FinaleCloudMath.GapScale(t);
        float gap = FinaleCloudMath.GapBearing(beamBearing, Mathf.Repeat(FinaleCamera.LookBearing(t) / 360f, 1f), SunBearing01(), t);
        float utime = Time.unscaledTime;
        for (int i = 0; i < LayerCount; i++)
        {
            Material m = materials[i];
            if (m == null || !LayerUsed(i))
            {
                continue;
            }

            // The far banks arrive first, the near ones last.
            float stagger = (LayerCount - 1 - i) / (float)Mathf.Max(1, LayerCount) * 0.35f;
            m.SetFloat(FadeId, Mathf.Clamp01((fade - stagger * 0.5f) / (1f - stagger * 0.5f)));
            m.SetFloat(DawnId, dawn01);
            m.SetFloat(OpenId, open);
            m.SetFloat(GapScaleId, gapScale);
            m.SetFloat(GapCenterId, gap);
            m.SetVector(SunDirId, sunVector);
            m.SetFloat(UTimeId, utime);
        }

        if (rays != null && rayMaterial != null && rays.enabled)
        {
            // The rays' object turns to the sun's bearing; their slant comes from the sun's elevation.
            rays.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(sun.x, sun.z) * Mathf.Rad2Deg, 0f);
            rayMaterial.SetFloat(StrengthId, strength);
            rayMaterial.SetVector(SunDirId, sunVector);
            rayMaterial.SetFloat(UTimeId, utime);
        }
    }
}
}
