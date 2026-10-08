using UnityEngine;

namespace LanternKeeper
{
// The firefly: a swarm of six blinking motes (four on Low) drifting around the pickup. The blink and drift run in the
// FireflyMote shader; this controller feeds each mote its per-instance values through a MaterialPropertyBlock, publishes the
// Low preset (_LKFireflyLow) and the Reduce flashing setting (_LKReduceFlashing), and breathes the sibling Glow light with
// the number of lit motes. Collect streams the motes into the lantern (two linger and orbit); Respawn fades them back in.
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
    static readonly int IndexId = Shader.PropertyToID("_Index");
    static readonly int StreamId = Shader.PropertyToID("_Stream");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int DriftFreqId = Shader.PropertyToID("_DriftFreq");
    static readonly int DriftPhaseId = Shader.PropertyToID("_DriftPhase");

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
    Vector3[] moteScales = new Vector3[0];
    Vector3[] streamStart = new Vector3[0];
    bool streaming;
    bool arrived;
    float sinceCollect;
    Transform target;
    Vector3 lastTargetPosition;
    LanternFlame targetFlame;
    bool fadingIn;
    float sinceRespawn;

    public int VisibleMotes => low ? FireflyCurve.MotesLow : FireflyCurve.Motes;
    public Transform[] Motes => motes;
    public bool Streaming => streaming;

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

    static int lastPublishedReduce = -1;

    // Publishes the Reduce flashing global; skipped while the value is unchanged unless forced (OnEnable).
    static void PublishReduceFlashing(bool force)
    {
        int value = UserSettings.ReduceFlashing ? 1 : 0;
        if (!force && value == lastPublishedReduce)
        {
            return;
        }

        lastPublishedReduce = value;
        Shader.SetGlobalFloat(ReduceId, value);
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
        PublishReduceFlashing(true);
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
            // Already cached: Update adopts any external change (LightQuality) as the new base.
            return;
        }

        baseIntensity = glow.intensity;
        lastAppliedIntensity = baseIntensity;
        baseCached = true;
    }

    void Update()
    {
        PublishReduceFlashing(false);
        if (!shown && !streaming && !fadingIn)
        {
            // Hidden swarm: LightQuality already hides the light, so skip the breathing.
            return;
        }

        StepStream();
        StepFadeIn();
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
        moteScales = new Vector3[motes.Length];
        streamStart = new Vector3[motes.Length];
        for (int i = 0; i < motes.Length; i++)
        {
            renderers[i] = motes[i].GetComponent<Renderer>();
            motes[i].localScale = Vector3.one * FireflyCurve.MoteSize;
            moteScales[i] = motes[i].localScale;
            if (renderers[i] != null)
            {
                // Runtime-only: Renderer.localBounds is not serialized.
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
            block.SetFloat(IndexId, i);
            block.SetFloat(StreamId, streaming ? 1f : 0f);
            block.SetFloat(FadeId, 1f);
            block.SetVector(DriftFreqId, FireflyCurve.DriftFreq(seed01, i));
            block.SetVector(DriftPhaseId, FireflyCurve.DriftPhase(seed01, i));
            renderer.SetPropertyBlock(block);
        }

        // A collect stream in progress keeps its motes visible (a quality change must not hide them); the hide lands when it ends.
        if (!streaming)
        {
            ApplyShown();
        }
    }

    // Shows or hides the motes. A collect stream in progress keeps its motes visible; the hide lands when the stream ends.
    public void SetShown(bool value)
    {
        shown = value;
        if (!streaming)
        {
            ApplyShown();
        }
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

    int LingerCount => low ? FireflyCurve.LingerLow : FireflyCurve.Linger;

    void SetMoteBlock(int i, float stream, float fade)
    {
        Renderer renderer = renderers[i];
        if (renderer == null)
        {
            return;
        }

        renderer.GetPropertyBlock(block);
        block.SetFloat(StreamId, stream);
        block.SetFloat(FadeId, fade);
        renderer.SetPropertyBlock(block);
    }

    // The firefly was picked up: the visible motes leave their drifting positions and stream into the target. Two linger and
    // orbit it, then everything fades out. Scaled time, so pause freezes the stream.
    public void Collect(Transform lanternTarget)
    {
        if (motes.Length == 0)
        {
            FindMotes();
        }

        target = lanternTarget;
        targetFlame = lanternTarget != null ? lanternTarget.GetComponentInParent<LanternFlame>() : null;
        lastTargetPosition = lanternTarget != null ? lanternTarget.position : transform.position;
        fadingIn = false;
        arrived = false;
        sinceCollect = 0f;
        float now = Time.timeSinceLevelLoad;
        int count = VisibleMotes;
        for (int i = 0; i < motes.Length; i++)
        {
            streamStart[i] = motes[i].position + FireflyCurve.Drift(seed01, i, now, 0f);
            motes[i].localScale = moteScales[i];
            SetMoteBlock(i, 1f, 1f);
            if (renderers[i] != null)
            {
                renderers[i].enabled = i < count;
            }
        }

        streaming = true;
        StepStream();
        // Collect runs in the physics step; the stream clock starts from the next rendered frame.
        sinceCollect = 0f;
    }

    void StepStream()
    {
        if (!streaming)
        {
            return;
        }

        if (target != null)
        {
            lastTargetPosition = target.position;
        }

        float t = sinceCollect;
        int count = VisibleMotes;
        if (t < FireflyCurve.StreamTime)
        {
            float eased = FireflyCurve.Stream01(t);
            for (int i = 0; i < count && i < motes.Length; i++)
            {
                motes[i].position = FireflyCurve.StreamPoint(streamStart[i], lastTargetPosition, (i & 1) == 0 ? 1f : -1f, eased);
                motes[i].localScale = moteScales[i] * FireflyCurve.StreamScale(eased);
            }
        }
        else if (t < FireflyCurve.LingerEnd)
        {
            if (!arrived)
            {
                arrived = true;
                OnArrive();
            }

            int linger = LingerCount;
            float alpha = FireflyCurve.LingerAlpha(t);
            for (int i = 0; i < motes.Length; i++)
            {
                if (i >= linger || i >= count)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = false;
                    }

                    continue;
                }

                motes[i].position = lastTargetPosition + FireflyCurve.LingerOffset(i, t);
                motes[i].localScale = moteScales[i] * FireflyCurve.LingerScale(t);
                SetMoteBlock(i, 1f, alpha);
            }
        }
        else
        {
            EndStream();
            return;
        }

        sinceCollect += Time.deltaTime;
    }

    void OnArrive()
    {
        if (targetFlame != null)
        {
            targetFlame.Kick(UserSettings.ReduceFlashing ? 0.5f : 1f);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFireflyArrive(lastTargetPosition);
        }
    }

    void EndStream()
    {
        streaming = false;
        target = null;
        targetFlame = null;
        ResetMotes();
        shown = false;
        ApplyShown();
    }

    void ResetMotes()
    {
        for (int i = 0; i < motes.Length; i++)
        {
            motes[i].localPosition = Vector3.zero;
            motes[i].localScale = moteScales[i];
        }
    }

    // The swarm is back at its new home: motes return to their drift and fade in one after another over a second.
    public void Respawn()
    {
        if (motes.Length == 0)
        {
            FindMotes();
        }

        streaming = false;
        target = null;
        targetFlame = null;
        ResetMotes();
        fadingIn = true;
        sinceRespawn = 0f;
        for (int i = 0; i < motes.Length; i++)
        {
            SetMoteBlock(i, 0f, FireflyCurve.FadeIn(i, VisibleMotes, 0f));
        }

        shown = true;
        ApplyShown();
    }

    void StepFadeIn()
    {
        if (!fadingIn)
        {
            return;
        }

        sinceRespawn += Time.deltaTime;
        bool done = sinceRespawn >= FireflyCurve.FadeInTime;
        int count = VisibleMotes;
        for (int i = 0; i < motes.Length; i++)
        {
            SetMoteBlock(i, 0f, done ? 1f : FireflyCurve.FadeIn(i, count, sinceRespawn));
        }

        if (done)
        {
            fadingIn = false;
        }
    }
}
}
