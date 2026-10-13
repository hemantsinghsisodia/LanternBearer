using UnityEngine;

namespace LanternKeeper
{
// The finale's distant Islands 1-4: detailed miniatures (baked by FinaleInstaller from the TerrainData and the island scenes' props, never
// loaded in play): a 128x128 terrain in the island's own colours (64x64 on Low), its biggest trees and rocks merged into one mesh (none on Low),
// and the real beacon models. They stand round Island 5 at FinaleLayout positions and fade in (dithered) during the Islands phase. At the flare
// time of island i its beacons ignite: the lantern glass blazes, the BeaconBeam cards and an amber flame card light up, the ground round the
// beacons is lit warm, and a glow sprite and a tall column read from far. No realtime lights.
// Everything is switched off until the finale reaches it, so the objects cost nothing in play. FinaleSequence calls Tick(t) every frame
// with the finale time; Tick does no allocation (one shared MaterialPropertyBlock).
public class FinaleIslands : MonoBehaviour
{
    public const int IslandCount = 4;
    public const float FadeSeconds = 2.2f;
    public const float GlowSize = 70f;
    public const float ColumnSeconds = 1f;
    public const float ColumnHeight = 120f;
    public const float ColumnWidth = 14f;
    // Radius (metres at island scale 1) of the warm light a flare throws on the ground and trees.
    public const float FlareRadius = 38f;
    const float PopSeconds = 0.4f;
    const float PopPeak = 3f;
    const float SteadyFlare = 1.2f;
    const float SettleSeconds = 2.2f;

    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int LenScaleId = Shader.PropertyToID("_LenScale");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int FlareId = Shader.PropertyToID("_Flare");
    static readonly int FlareCenterId = Shader.PropertyToID("_FlareCenter");

    [System.Serializable]
    public class Entry
    {
        public Transform root;
        // The terrain (the field keeps its old name so scenes and the wiring check keep working).
        public Renderer silhouette;
        public Mesh terrainFull;
        public Mesh terrainLow;
        public Renderer props;
        public Renderer beaconBase;
        public Renderer glass;
        public Renderer flame;
        // Island local position of the flare's centre (the highest beacon).
        public Vector3 flareLocal;
        public Renderer[] beams;
        public Transform glow;
        public Renderer glowRenderer;
        public Transform column;
        public Renderer columnRenderer;
    }

    [SerializeField] Entry[] entries = new Entry[0];
    [SerializeField] Color glowColor = new Color(1f, 0.694f, 0.361f, 0.8f);
    // The sunrise: a glow on the horizon where the sun comes up (bearing FinaleCamera.SunBearing) and a bright path on the sea towards it.
    [SerializeField] Transform sunGlow;
    [SerializeField] Renderer sunGlowRenderer;
    [SerializeField] Renderer sunPathRenderer;
    [SerializeField] Color sunColor = new Color(1f, 0.74f, 0.38f, 1f);

    static MaterialPropertyBlock block;
    // The Horizon hills (Range*, Islet*, Ridge*, and the additive Haze* rings, which turn the distant sea gold at dawn) hide the islands and read as stepped rings of terrain from the low camera.
    // They sink flat as the storm opens: SinkInnerHorizon(0..1). Done once they are gone.
    bool sunk;
    Transform[] inner;
    float[] innerScaleY;
    Camera viewer;
    bool low;
    bool ready;
    float lastTime = -1f;

    public int Count => entries != null ? entries.Length : 0;
    public Entry EntryAt(int island) { return entries[island]; }

    // 0..1: island i (0..3) fades in just before its beacons flare (never before the Islands phase).
    public static float FadeAt(int island, float t)
    {
        return FadeFor(FinaleTimeline.IslandFlareTime(island), t);
    }

    // The same for an island whose beacons flare at `flareTime` (finale seconds).
    public static float FadeFor(float flareTime, float t)
    {
        float start = Mathf.Max(FinaleTimeline.IslandsStart, flareTime - FadeSeconds - 0.3f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - start) / FadeSeconds));
    }

    // The beacons' brightness: 0 before the flare, a quick pop to PopPeak, then settling to a steady glow.
    public static float FlareAt(int island, float t)
    {
        return FlareFor(FinaleTimeline.IslandFlareTime(island), t);
    }

    // The same for a flare that starts at `startTime` (finale seconds).
    public static float FlareFor(float startTime, float t)
    {
        float age = t - startTime;
        if (age < 0f)
        {
            return 0f;
        }

        if (age < PopSeconds)
        {
            return PopPeak * (age / PopSeconds);
        }

        return Mathf.Lerp(PopPeak, SteadyFlare, Mathf.SmoothStep(0f, 1f, (age - PopSeconds) / SettleSeconds));
    }

    void Awake()
    {
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        FindInnerHorizon();
        ApplyTerrainDetail();
        SetAll(false);
        ready = true;
    }

    void FindInnerHorizon()
    {
        GameObject horizon = GameObject.Find("Horizon");
        int count = 0;
        for (int i = 0; horizon != null && i < horizon.transform.childCount; i++)
        {
            if (IsInnerHorizon(horizon.transform.GetChild(i).name)) count++;
        }

        inner = new Transform[count];
        innerScaleY = new float[count];
        int cursor = 0;
        for (int i = 0; horizon != null && i < horizon.transform.childCount; i++)
        {
            Transform child = horizon.transform.GetChild(i);
            if (!IsInnerHorizon(child.name)) continue;
            inner[cursor] = child;
            innerScaleY[cursor] = child.localScale.y;
            cursor++;
        }
    }

    static bool IsInnerHorizon(string objectName)
    {
        return objectName.StartsWith("Range") || objectName.StartsWith("Islet") || objectName.StartsWith("Ridge") || objectName.StartsWith("Haze");
    }

    // 0 = the hills as built, 1 = flattened into the sea (and switched off).
    public void SinkInnerHorizon(float k)
    {
        if (sunk)
        {
            return;
        }

        k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        for (int i = 0; inner != null && i < inner.Length; i++)
        {
            if (inner[i] == null)
            {
                continue;
            }

            bool gone = k >= 0.999f;
            if (inner[i].gameObject.activeSelf == gone)
            {
                inner[i].gameObject.SetActive(!gone);
            }

            Vector3 scale = inner[i].localScale;
            scale.y = Mathf.Max(0.001f, innerScaleY[i] * (1f - k));
            inner[i].localScale = scale;
        }

        sunk = k >= 0.999f;
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnQuality(GraphicsProfile profile)
    {
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        ApplyTerrainDetail();
        if (lastTime >= 0f)
        {
            Tick(lastTime);
        }
    }

    // 128x128 terrain, or the 64x64 one on Low.
    void ApplyTerrainDetail()
    {
        for (int i = 0; entries != null && i < entries.Length; i++)
        {
            Entry e = entries[i];
            MeshFilter filter = e != null && e.silhouette != null ? e.silhouette.GetComponent<MeshFilter>() : null;
            if (filter == null)
            {
                continue;
            }

            Mesh wanted = low && e.terrainLow != null ? e.terrainLow : e.terrainFull;
            if (wanted != null && filter.sharedMesh != wanted)
            {
                filter.sharedMesh = wanted;
            }
        }
    }

    void SetAll(bool on)
    {
        if (sunGlowRenderer != null) sunGlowRenderer.enabled = false;
        if (sunPathRenderer != null) sunPathRenderer.enabled = false;
        for (int i = 0; entries != null && i < entries.Length; i++)
        {
            Entry e = entries[i];
            if (e == null)
            {
                continue;
            }

            if (e.silhouette != null) e.silhouette.enabled = on;
            if (e.props != null) e.props.enabled = on;
            if (e.beaconBase != null) e.beaconBase.enabled = on;
            if (e.glass != null) e.glass.enabled = on;
            if (e.flame != null) e.flame.enabled = on;
            if (e.glowRenderer != null) e.glowRenderer.enabled = on;
            if (e.columnRenderer != null) e.columnRenderer.enabled = on;
            for (int b = 0; e.beams != null && b < e.beams.Length; b++)
            {
                if (e.beams[b] != null) e.beams[b].enabled = on;
            }
        }
    }

    void TickSun(float t)
    {
        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - FinaleTimeline.SunriseStart + 3f) / (FinaleTimeline.SunriseLength - 2f)));
        bool on = u > 0.001f;
        if (sunGlowRenderer != null)
        {
            if (sunGlowRenderer.enabled != on) sunGlowRenderer.enabled = on;
            if (on)
            {
                if (viewer != null && sunGlow != null) sunGlow.rotation = viewer.transform.rotation;
                Color c = sunColor;
                c.a = 0.95f * u;
                sunGlowRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                sunGlowRenderer.SetPropertyBlock(block);
            }
        }

        if (sunPathRenderer != null)
        {
            if (sunPathRenderer.enabled != on) sunPathRenderer.enabled = on;
            if (on)
            {
                Color c = sunColor;
                c.a = 0.8f * u;
                sunPathRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                sunPathRenderer.SetPropertyBlock(block);
            }
        }
    }

    // Fade (dithered) and the flare light on one of an island's lit bodies.
    static void TickBody(Renderer r, bool visible, float fade, float flare, Vector3 flareWorld, float radius)
    {
        if (r == null)
        {
            return;
        }

        if (r.enabled != visible)
        {
            r.enabled = visible;
        }

        if (visible)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(FadeId, fade);
            block.SetFloat(FlareId, flare);
            block.SetVector(FlareCenterId, new Vector4(flareWorld.x, flareWorld.y, flareWorld.z, radius));
            r.SetPropertyBlock(block);
        }
    }

    // Finale time in seconds.
    public void Tick(float t)
    {
        if (!ready)
        {
            return;
        }

        lastTime = t;
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        if (viewer == null)
        {
            viewer = Camera.main;
        }

        float lenScale = low ? BeaconVisual.BeamLengthScaleLow : 1f;
        float intensityScale = low ? BeaconVisual.BeamIntensityScaleLow : 1f;
        for (int i = 0; i < entries.Length && i < IslandCount; i++)
        {
            Entry e = entries[i];
            if (e == null)
            {
                continue;
            }

            float flareTime = FinaleTimeline.IslandFlareTime(i);
            float fade = FadeFor(flareTime, t);
            float flare = FlareFor(flareTime, t);
            bool visible = fade > 0.001f;
            float scale = e.root != null ? e.root.lossyScale.x : 1f;
            Vector3 flareWorld = e.root != null ? e.root.TransformPoint(e.flareLocal) : e.flareLocal;
            TickBody(e.silhouette, visible, fade, flare, flareWorld, FlareRadius * scale);
            TickBody(e.props, visible && !low, fade, flare, flareWorld, FlareRadius * scale);
            TickBody(e.beaconBase, visible, fade, flare, flareWorld, FlareRadius * scale);

            bool lit = flare > 0.001f;
            // A gentle shimmer on the steady glow, like the beacon beams.
            float shimmer = Mathf.Lerp(0.85f, 1f, Mathf.PerlinNoise(t * 3f, i * 7.3f));
            for (int b = 0; e.beams != null && b < e.beams.Length; b++)
            {
                Renderer beam = e.beams[b];
                if (beam == null)
                {
                    continue;
                }

                if (beam.enabled != lit)
                {
                    beam.enabled = lit;
                }

                if (lit)
                {
                    beam.GetPropertyBlock(block);
                    block.SetFloat(IntensityId, flare * shimmer * intensityScale);
                    block.SetFloat(LenScaleId, lenScale);
                    beam.SetPropertyBlock(block);
                }
            }

            // The lantern glass is dark until the island is faded in, then blazes with the flare.
            if (e.glass != null)
            {
                bool glassOn = fade >= 0.95f || lit;
                if (e.glass.enabled != glassOn)
                {
                    e.glass.enabled = glassOn;
                }

                if (glassOn)
                {
                    e.glass.GetPropertyBlock(block);
                    block.SetFloat(GlowId, flare * shimmer);
                    e.glass.SetPropertyBlock(block);
                }
            }

            // The flame cards inside the lanterns: warm, additive, with the same shimmer.
            if (e.flame != null)
            {
                if (e.flame.enabled != lit)
                {
                    e.flame.enabled = lit;
                }

                if (lit)
                {
                    Color c = glowColor;
                    c.a = Mathf.Min(1f, flare * 0.55f) * shimmer;
                    e.flame.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, c);
                    e.flame.SetPropertyBlock(block);
                }
            }

            // The tall amber column rises over ColumnSeconds from the flare and lingers (a little lower once it has settled).
            if (e.columnRenderer != null && e.column != null)
            {
                float age = t - flareTime;
                bool on = age >= 0f;
                if (e.columnRenderer.enabled != on)
                {
                    e.columnRenderer.enabled = on;
                }

                if (on)
                {
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / ColumnSeconds));
                    float settle = Mathf.Lerp(1f, 0.75f, Mathf.SmoothStep(0f, 1f, (age - ColumnSeconds) / 3f));
                    float parentScale = e.root != null ? e.root.localScale.x : 1f;
                    e.column.localScale = new Vector3(ColumnWidth / parentScale, Mathf.Max(0.01f, ColumnHeight * rise * settle / parentScale), ColumnWidth / parentScale);
                }
            }

            if (e.glowRenderer != null)
            {
                if (e.glowRenderer.enabled != lit)
                {
                    e.glowRenderer.enabled = lit;
                }

                if (lit && e.glow != null)
                {
                    float parentScale = e.root != null ? e.root.localScale.x : 1f;
                    float size = GlowSize * (0.45f + 0.2f * flare) * (low ? 0.7f : 1f) / parentScale;
                    e.glow.localScale = new Vector3(size, size, size);
                    if (viewer != null)
                    {
                        e.glow.rotation = viewer.transform.rotation;
                    }

                    Color c = glowColor;
                    c.a = Mathf.Clamp01(flare * 0.4f) * glowColor.a;
                    e.glowRenderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, c);
                    e.glowRenderer.SetPropertyBlock(block);
                }
            }
        }

        TickSun(t);
    }
}
}
