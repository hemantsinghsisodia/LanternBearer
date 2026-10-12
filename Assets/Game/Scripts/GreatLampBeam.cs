using UnityEngine;

namespace LanternKeeper
{
// The lighthouse beam: two opposed BeaconBeam cards at the lantern room (mesh built by LighthouseInstaller) that turn once the great
// lamp is lit. Begin() ramps the intensity 0..1 and starts the rotation; both run on unscaled time so the finale cinematic (which may
// slow or pause the world) keeps the beam turning at a steady rate. On the Low graphics level the beam is dimmer and shorter, the
// same scale factors the beacons use.
public class GreatLampBeam : MonoBehaviour
{
    public const float DegreesPerSecond = 24f;
    public const float DefaultRampSeconds = 2f;
    const float MaxStep = 0.1f;

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int LenScaleId = Shader.PropertyToID("_LenScale");

    [SerializeField] Renderer beamRenderer;
    [SerializeField] float peakIntensity = 2.2f;

    MaterialPropertyBlock block;
    bool started;
    float ramp = DefaultRampSeconds;
    float elapsed;
    float angle;

    public bool Running => started;
    // 0..1 (before the Low-quality scale).
    public float Intensity01 => started ? IntensityAt(elapsed, ramp) : 0f;
    // Degrees turned since Begin().
    public float Angle => angle;

    public static float IntensityAt(float seconds, float rampSeconds)
    {
        return rampSeconds <= 0f ? 1f : Mathf.Clamp01(seconds / rampSeconds);
    }

    void Awake()
    {
        if (beamRenderer == null)
        {
            beamRenderer = GetComponent<Renderer>();
        }

        if (beamRenderer != null)
        {
            beamRenderer.enabled = false;
        }
    }

    public void Begin(float rampSeconds)
    {
        ramp = Mathf.Max(0f, rampSeconds);
        elapsed = 0f;
        started = true;
        if (beamRenderer != null)
        {
            beamRenderer.enabled = true;
        }

        Apply();
    }

    void Update()
    {
        if (!started)
        {
            return;
        }

        // A long hitch (a loading frame, a window drag) must not skip the ramp or jump the sweep.
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        elapsed += dt;
        float step = DegreesPerSecond * dt;
        angle += step;
        transform.Rotate(0f, step, 0f, Space.Self);
        Apply();
    }

    void Apply()
    {
        if (beamRenderer == null)
        {
            return;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        bool low = GraphicsQuality.Current == GraphicsLevel.Low;
        beamRenderer.GetPropertyBlock(block);
        block.SetFloat(IntensityId, Intensity01 * peakIntensity * (low ? BeaconVisual.BeamIntensityScaleLow : 1f));
        block.SetFloat(LenScaleId, low ? BeaconVisual.BeamLengthScaleLow : 1f);
        beamRenderer.SetPropertyBlock(block);
    }
}
}
