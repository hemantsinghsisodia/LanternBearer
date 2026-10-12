using UnityEngine;

namespace LanternKeeper
{
// The finale's distant Islands 1-4: low-poly silhouettes of each island's terrain (baked by FinaleInstaller from the TerrainData, not
// loaded scenes) standing round Island 5 at FinaleLayout positions. They fade in during the Islands phase; at FinaleTimeline.IslandFlareTime(i)
// the beacon cluster on island i flares: five BeaconBeam card clusters and one additive glow sprite. No realtime lights.
// Everything is switched off until the finale reaches it, so the objects cost nothing in play. FinaleSequence calls Tick(t) every frame
// with the finale time; Tick does no allocation (one shared MaterialPropertyBlock).
public class FinaleIslands : MonoBehaviour
{
    public const int IslandCount = 4;
    public const float FadeSeconds = 2.2f;
    public const float GlowSize = 120f;
    const float PopSeconds = 0.4f;
    const float PopPeak = 3f;
    const float SteadyFlare = 1.2f;
    const float SettleSeconds = 2.2f;

    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int LenScaleId = Shader.PropertyToID("_LenScale");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [System.Serializable]
    public class Entry
    {
        public Transform root;
        public Renderer silhouette;
        public Renderer[] beams;
        public Transform glow;
        public Renderer glowRenderer;
    }

    [SerializeField] Entry[] entries = new Entry[0];
    [SerializeField] Color glowColor = new Color(1f, 0.694f, 0.361f, 0.8f);

    static MaterialPropertyBlock block;
    // The Horizon hills nearer than the silhouettes (Range*, Islet*) would hide them, and from the orbit they read as bands of terrain.
    // They sink flat as the storm opens: SinkInnerHorizon(0..1).
    Transform[] inner;
    float[] innerScaleY;
    Camera viewer;
    bool low;
    bool ready;
    float lastTime = -1f;

    public int Count => entries != null ? entries.Length : 0;
    public Entry EntryAt(int island) { return entries[island]; }

    // 0..1: the silhouette of island i (0..3) fades in just before its beacons flare (never before the Islands phase).
    public static float FadeAt(int island, float t)
    {
        float flare = FinaleTimeline.IslandFlareTime(island);
        float start = Mathf.Max(FinaleTimeline.IslandsStart, flare - FadeSeconds - 0.3f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - start) / FadeSeconds));
    }

    // The beacon cluster's brightness: 0 before the flare, a quick pop to PopPeak, then settling to a steady glow.
    public static float FlareAt(int island, float t)
    {
        float age = t - FinaleTimeline.IslandFlareTime(island);
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
        return objectName.StartsWith("Range") || objectName.StartsWith("Islet");
    }

    // 0 = the hills as built, 1 = flattened into the sea (and switched off).
    public void SinkInnerHorizon(float k)
    {
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
        if (lastTime >= 0f)
        {
            Tick(lastTime);
        }
    }

    void SetAll(bool on)
    {
        for (int i = 0; entries != null && i < entries.Length; i++)
        {
            Entry e = entries[i];
            if (e == null)
            {
                continue;
            }

            if (e.silhouette != null) e.silhouette.enabled = on;
            if (e.glowRenderer != null) e.glowRenderer.enabled = on;
            for (int b = 0; e.beams != null && b < e.beams.Length; b++)
            {
                if (e.beams[b] != null) e.beams[b].enabled = on;
            }
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

            float fade = FadeAt(i, t);
            float flare = FlareAt(i, t);
            if (e.silhouette != null)
            {
                bool visible = fade > 0.001f;
                if (e.silhouette.enabled != visible)
                {
                    e.silhouette.enabled = visible;
                }

                if (visible)
                {
                    e.silhouette.GetPropertyBlock(block);
                    block.SetFloat(FadeId, fade);
                    e.silhouette.SetPropertyBlock(block);
                }
            }

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

            if (e.glowRenderer != null)
            {
                if (e.glowRenderer.enabled != lit)
                {
                    e.glowRenderer.enabled = lit;
                }

                if (lit && e.glow != null)
                {
                    float size = GlowSize * (0.45f + 0.2f * flare) * (low ? 0.7f : 1f);
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
    }
}
}
