using UnityEngine;

namespace LanternKeeper
{
// The firefly: a swarm of six blinking motes (four on Low) drifting around the pickup. The blink and drift run in the
// FireflyMote shader; this controller feeds each mote its per-instance values through a MaterialPropertyBlock, publishes the
// Low preset (_LKFireflyLow) and the Reduce flashing setting (_LKReduceFlashing), and breathes the sibling Glow light with
// the number of lit motes. Collect and Respawn belong to the collect stream (a later task).
public class FireflySwarm : MonoBehaviour
{
    public const string LowGlobalName = "_LKFireflyLow";
    public const string ReduceFlashingGlobalName = "_LKReduceFlashing";
    const string GlowName = "Glow";
    const float LightSmoothing = 0.25f;
    const float MinLightFactor = 0.6f;

    static readonly int LowId = Shader.PropertyToID(LowGlobalName);
    static readonly int ReduceId = Shader.PropertyToID(ReduceFlashingGlobalName);
    static readonly int PhaseId = Shader.PropertyToID("_Phase");
    static readonly int PeriodId = Shader.PropertyToID("_Period");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int IndexId = Shader.PropertyToID("_Index");
    static readonly int StreamId = Shader.PropertyToID("_Stream");
    static readonly int FadeId = Shader.PropertyToID("_Fade");

    Transform[] motes = new Transform[0];
    Renderer[] renderers = new Renderer[0];
    MaterialPropertyBlock block;
    Light glow;
    float seed01;
    float period = 3f;
    bool low;
    bool shown = true;
    float baseIntensity;
    float lastAppliedIntensity;
    bool baseCached;
    float smoothedLit = 0.5f;

    public int VisibleMotes => low ? FireflyCurve.MotesLow : FireflyCurve.Motes;
    public Transform[] Motes => motes;
    public bool Streaming => false;

    // CPU mirror of the shader blink: lit motes now over the visible motes.
    public float LitFraction
    {
        get
        {
            int count = VisibleMotes;
            return FireflyCurve.LitCount(Time.timeSinceLevelLoad, period, seed01, count, UserSettings.ReduceFlashing) / (float)count;
        }
    }

    // Writes the Low flag to the shader global. Called whenever the preset changes.
    public static void PublishQuality()
    {
        Shader.SetGlobalFloat(LowId, GraphicsQuality.Current == GraphicsLevel.Low ? 1f : 0f);
    }

    static void PublishReduceFlashing()
    {
        Shader.SetGlobalFloat(ReduceId, UserSettings.ReduceFlashing ? 1f : 0f);
    }

    void Awake()
    {
        FindMotes();
        Vector3 p = transform.position;
        seed01 = Mathf.Repeat(Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f)) * 43758.5453f, 1f);
        block = new MaterialPropertyBlock();
        Transform parent = transform.parent;
        Transform glowTransform = parent != null ? parent.Find(GlowName) : null;
        glow = glowTransform != null ? glowTransform.GetComponent<Light>() : null;
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        Apply();
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        PublishQuality();
        PublishReduceFlashing();
        CacheLight();
        Apply();
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
        if (glow != null && baseCached)
        {
            glow.intensity = baseIntensity;
        }

        baseCached = false;
    }

    void OnQuality(GraphicsProfile profile)
    {
        PublishQuality();
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        CacheLight();
        Apply();
    }

    // The Glow intensity LightQuality (or the prefab) set; the breathing multiplies against this.
    void CacheLight()
    {
        if (glow == null)
        {
            return;
        }

        if (baseCached)
        {
            glow.intensity = baseIntensity;
        }

        baseIntensity = glow.intensity;
        lastAppliedIntensity = baseIntensity;
        baseCached = true;
    }

    void Update()
    {
        PublishReduceFlashing();
        smoothedLit += (LitFraction - smoothedLit) * (1f - Mathf.Exp(-Time.deltaTime / LightSmoothing));
        if (glow == null)
        {
            return;
        }

        if (!baseCached || Mathf.Abs(glow.intensity - lastAppliedIntensity) > 1e-4f)
        {
            // Someone else changed the intensity: that is the new base.
            baseIntensity = glow.intensity;
            baseCached = true;
        }

        lastAppliedIntensity = baseIntensity * Mathf.Lerp(MinLightFactor, 1f, smoothedLit);
        glow.intensity = lastAppliedIntensity;
    }

    // Finds the MoteN children and gives their renderers large bounds so drift and the stream are never culled.
    void FindMotes()
    {
        System.Collections.Generic.List<Transform> found = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < FireflyCurve.Motes; i++)
        {
            Transform mote = transform.Find("Mote" + i);
            if (mote != null)
            {
                found.Add(mote);
            }
        }

        motes = found.ToArray();
        renderers = new Renderer[motes.Length];
        for (int i = 0; i < motes.Length; i++)
        {
            renderers[i] = motes[i].GetComponent<Renderer>();
            if (renderers[i] != null)
            {
                renderers[i].localBounds = new Bounds(Vector3.zero, Vector3.one * 3f);
            }
        }
    }

    void Apply()
    {
        if (motes.Length == 0)
        {
            FindMotes();
        }

        int count = VisibleMotes;
        period = FireflyCurve.Period(seed01, low);
        for (int i = 0; i < motes.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(block);
            block.SetFloat(PhaseId, FireflyCurve.MoteOffset(i, count, FireflyCurve.MoteJitter01(seed01, i)));
            block.SetFloat(PeriodId, period);
            block.SetFloat(SeedId, seed01);
            block.SetFloat(IndexId, i);
            block.SetFloat(StreamId, 0f);
            block.SetFloat(FadeId, 1f);
            renderer.SetPropertyBlock(block);
        }

        ApplyShown();
    }

    // Hides the motes when the firefly is collected (until the collect stream exists, nothing is left to show).
    public void SetShown(bool value)
    {
        shown = value;
        ApplyShown();
    }

    void ApplyShown()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = shown && i < VisibleMotes;
            }
        }
    }

    public void Collect(Transform target)
    {
    }

    public void Respawn()
    {
    }
}
}
