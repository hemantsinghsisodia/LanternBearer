using UnityEngine;

namespace LanternKeeper
{
// Visual side of a beacon: the storm lantern's panes and light beams, the ember burst, the ground ring's bloom radius and the
// warm point light's moment ramp. It never decides anything: Beacon.cs lights the beacon (and is safe on that same frame);
// this component only listens to Beacon.Lit for its own beacon and plays the BeaconLightCurve moment on top.
//
// Bloom kick: deliberately NOT an exposure bump. UserSettingsApplier owns the user brightness and the exposure base, and a
// temporary exposure offset could race with it (and would not restore exactly if a settings tick landed mid moment). The
// flare is done with pane emission (the BeaconLightCurve.Flare pop makes the panes and beams over-bright, which the existing
// bloom picks up) and a flare on the point light instead, so nothing outside this beacon is written.
//
// Light: Beacon.cs keeps writing the light intensity (ignite pop, settle, flicker). In LateUpdate this multiplies that value by
// Intensity01 while the moment runs, so the steady light is exactly the existing value and nothing is written afterwards.
public class BeaconVisual : MonoBehaviour
{
    public const float FlickerMin = 0.6f;
    public const float FlickerMax = 1f;
    public const int EmberCountFull = 45;
    public const int EmberCountLow = 15;
    public const float BeamLengthScaleLow = 0.5f;
    public const float BeamIntensityScaleLow = 0.6f;
    public const float WhoompVolume = 0.9f;

    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int LenScaleId = Shader.PropertyToID("_LenScale");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock block;

    [SerializeField] Renderer glassRenderer;
    [SerializeField] Renderer beamRenderer;
    [SerializeField] ParticleSystem embers;
    [SerializeField] Renderer[] cairnRenderers;

    Beacon beacon;
    Light beaconLight;
    bool low;
    bool lit;
    bool playing;
    float momentTime = -1f;
    float ringRadius;
    float lightFactor;
    float flare;
    float glow;
    float beamIntensity;
    float emberAccumulator;
    int emberTotal;
    int embersEmitted;
    float seed;

    // Seconds since the current moment started. -1 when no moment has run (unlit, or shown steady with no replay).
    public float MomentTime => momentTime;
    public bool IsMomentPlaying => playing;
    public bool IsLitVisual => lit;
    public float RingRadius => ringRadius;
    // 0..1 alpha of the ground ring: it fades in with the light.
    public float RingAlpha => lit ? (playing ? lightFactor : 1f) : 0f;
    public float LightFactor => lightFactor;
    public float Glow => glow;
    public Renderer[] CairnRenderers => cairnRenderers;
    public float BeamIntensity => beamIntensity;
    public int EmbersEmitted => embersEmitted;
    public Renderer GlassRenderer => glassRenderer;
    public Renderer BeamRenderer => beamRenderer;
    public ParticleSystem Embers => embers;

    public static float BeamLengthScale(GraphicsLevel level)
    {
        return level == GraphicsLevel.Low ? BeamLengthScaleLow : 1f;
    }

    public static float BeamIntensityScale(GraphicsLevel level)
    {
        return level == GraphicsLevel.Low ? BeamIntensityScaleLow : 1f;
    }

    public static int EmberCount(GraphicsLevel level)
    {
        return level == GraphicsLevel.Low ? EmberCountLow : EmberCountFull;
    }

    // The pane glow for a steady lit beacon: a flicker between FlickerMin and FlickerMax from a 0..1 noise value.
    public static float Flicker(float noise01)
    {
        return Mathf.Lerp(FlickerMin, FlickerMax, Mathf.Clamp01(noise01));
    }

    void Awake()
    {
        seed = Random.value * 50f;
        Resolve();
    }

    void Resolve()
    {
        if (beacon == null)
        {
            beacon = GetComponentInParent<Beacon>();
        }

        if (beacon != null && beaconLight == null)
        {
            beaconLight = beacon.GetComponentInChildren<Light>(true);
        }

        if (glassRenderer == null || beamRenderer == null)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (glassRenderer == null && material.shader.name == "LanternKeeper/BeaconGlass")
                {
                    glassRenderer = renderers[i];
                }
                else if (beamRenderer == null && material.shader.name == "LanternKeeper/BeaconBeam")
                {
                    beamRenderer = renderers[i];
                }
            }
        }
    }

    // Cairn tint: the prefab is shared by every island, so the active island's LookProfile.rockTint is multiplied in through the
    // cairn renderers' material colour (the same texture-times-rockTint recipe as the per-island Nature_Rock materials).
    void Start()
    {
        ApplyRockTint();
    }

    void ApplyRockTint()
    {
        LookApplier applier = LookApplier.Current;
        LookProfile profile = applier != null ? applier.Profile : null;
        if (profile == null)
        {
            return;
        }

        Color tint = profile.rockTint;
        tint.a = 1f;
        if (cairnRenderers == null)
        {
            return;
        }

        MaterialPropertyBlock tintBlock = new MaterialPropertyBlock();

        for (int i = 0; i < cairnRenderers.Length; i++)
        {
            if (cairnRenderers[i] != null)
            {
                cairnRenderers[i].GetPropertyBlock(tintBlock);
                tintBlock.SetColor(BaseColorId, tint);
                cairnRenderers[i].SetPropertyBlock(tintBlock);
            }
        }
    }

    void OnEnable()
    {
        Resolve();
        Beacon.Lit += OnBeaconLit;
        GraphicsQuality.QualityChanged += OnQuality;
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        // No replay: a beacon that is already lit when this appears shows its steady state at once.
        if (beacon != null && beacon.IsLit)
        {
            ShowSteadyLit();
        }
        else
        {
            ShowUnlit();
        }
    }

    void OnDisable()
    {
        Beacon.Lit -= OnBeaconLit;
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnBeaconLit(Beacon lighted)
    {
        if (lighted == beacon && beacon != null)
        {
            PlayLightMoment();
        }
    }

    void OnQuality(GraphicsProfile profile)
    {
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        Write();
    }

    float Zone()
    {
        return beacon != null ? beacon.ZoneRadius : 8f;
    }

    public void ShowUnlit()
    {
        lit = false;
        playing = false;
        momentTime = -1f;
        ringRadius = 0f;
        lightFactor = 0f;
        flare = 0f;
        glow = 0f;
        beamIntensity = 0f;
        StopEmbers();
        Write();
    }

    // The steady lit look: full light, full ring, no embers and no moment. Called on enable for an already-lit beacon.
    public void ShowSteadyLit()
    {
        lit = true;
        playing = false;
        momentTime = -1f;
        ringRadius = Zone();
        lightFactor = 1f;
        flare = 0f;
        StopEmbers();
        UpdateGlow();
        Write();
    }

    public void PlayLightMoment()
    {
        lit = true;
        playing = true;
        momentTime = 0f;
        emberAccumulator = 0f;
        embersEmitted = 0;
        emberTotal = EmberCount(low ? GraphicsLevel.Low : GraphicsLevel.Ultra);
        if (embers != null)
        {
            embers.Clear(true);
            embers.Play(true);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOneShot(ProceduralAudio.Whoomp(), transform.position, WhoompVolume);
        }

        EvaluateMoment();
        Write();
    }

    void StopEmbers()
    {
        if (embers != null)
        {
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void UpdateGlow()
    {
        float noise = Mathf.PerlinNoise(Time.time * 6.5f + seed, 0.35f);
        glow = Flicker(noise) * lightFactor;
        beamIntensity = Mathf.Lerp(0.8f, 1f, noise) * lightFactor * BeamIntensityScale(low ? GraphicsLevel.Low : GraphicsLevel.Ultra);
    }

    void EvaluateMoment()
    {
        float t = momentTime;
        lightFactor = BeaconLightCurve.Intensity01(t);
        ringRadius = BeaconLightCurve.RingRadius(t, Zone());
        flare = BeaconLightCurve.Flare(t);
    }

    void Update()
    {
        if (!lit)
        {
            return;
        }

        if (playing)
        {
            float dt = Time.deltaTime;
            momentTime += dt;
            if (momentTime >= BeaconLightCurve.Duration)
            {
                momentTime = BeaconLightCurve.Duration;
                playing = false;
                lightFactor = 1f;
                ringRadius = Zone();
                flare = 0f;
                StopEmbersEmitting();
            }
            else
            {
                EvaluateMoment();
                EmitEmbers(dt);
            }
        }

        UpdateGlow();
        // Over-bright pop at the flare: the panes and beams exceed their steady value for a moment.
        glow += flare * 2f;
        beamIntensity += flare * 1f;
        if (playing || glassRenderer == null || glassRenderer.isVisible)
        {
            Write();
        }
    }

    void LateUpdate()
    {
        if (!lit || !playing || beaconLight == null || !beaconLight.enabled)
        {
            return;
        }

        beaconLight.intensity *= lightFactor + flare * 0.6f;
    }

    void StopEmbersEmitting()
    {
        if (embers != null)
        {
            embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // Spreads the burst over 0.1..1.5 s following BeaconLightCurve.EmberRate01, so the total is EmberCount.
    void EmitEmbers(float dt)
    {
        if (embers == null || embersEmitted >= emberTotal)
        {
            return;
        }

        // Integral of EmberRate01 over its window: 0.3 * 1.4 + 0.7 * 1.4 * 2 / pi.
        const float integral = 1.0443f;
        emberAccumulator += emberTotal * BeaconLightCurve.EmberRate01(momentTime) * dt / integral;
        int count = Mathf.Min((int)emberAccumulator, emberTotal - embersEmitted);
        if (count > 0)
        {
            emberAccumulator -= count;
            embers.Emit(count);
            embersEmitted += count;
        }
    }

    void Write()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        if (glassRenderer != null)
        {
            glassRenderer.GetPropertyBlock(block);
            block.SetFloat(GlowId, glow);
            glassRenderer.SetPropertyBlock(block);
        }

        if (beamRenderer != null)
        {
            beamRenderer.enabled = lit && beamIntensity > 0.001f;
            beamRenderer.GetPropertyBlock(block);
            block.SetFloat(IntensityId, beamIntensity);
            block.SetFloat(LenScaleId, BeamLengthScale(low ? GraphicsLevel.Low : GraphicsLevel.Ultra));
            beamRenderer.SetPropertyBlock(block);
        }
    }
}
}
