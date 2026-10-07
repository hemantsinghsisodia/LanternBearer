using UnityEngine;

namespace LanternKeeper
{
// Visual side of the Shade: it only reads state, it never changes it. Each frame (LateUpdate, after Shade.cs has written its own
// body colour block) it merges three values into the body's MaterialPropertyBlock for the Shade shader:
//   _Outline  the capped lightning flash (Lightning.CurrentFlash already honours Reduce flashing), drawn as a pale blue rim.
//   _Burn     0 -> 1 while the Shade is frozen in the lantern light, easing back to 0 over BurnFallSeconds after release.
//   _Speed    the Shade's speed in m/s from a smoothed position delta (the Shade moves its transform directly).
//   _Stunned  0 -> 1 while ShadeState.Stunned (the shader's dark navy ink), eased over StunEaseSeconds.
// It also drives the smoke trail emission by speed, and publishes the Low preset as the shader global _LKShadeLow.
public class ShadeVisual : MonoBehaviour
{
    public const string LowGlobalName = "_LKShadeLow";
    // Freeze burn edge: a dim, desaturated ash ember. Not amber (#FFB15C): amber means safe warmth.
    public const string BurnEdgeHex = "A86A55";
    public const float BurnRiseSeconds = 0.45f;
    public const float BurnFallSeconds = 0.3f;
    // Seconds for _Stunned to ease fully in or out (the old look followed Shade.cs's tint, which snaps; this is a short ease).
    public const float StunEaseSeconds = 0.08f;
    public const float MaxSpeed = 5f;
    public const float TrailLifetimeScaleLow = 0.5f;
    public const float TrailRateScaleLow = 0.6f;
    public const float TrailIdleRate = 0.35f;
    public const float TrailMovingRate = 1.6f;
    // A single frame jump above this speed is a teleport (spawn, retreat respawn), not motion.
    const float TeleportSpeed = 30f;

    static readonly int OutlineId = Shader.PropertyToID("_Outline");
    static readonly int BurnId = Shader.PropertyToID("_Burn");
    static readonly int SpeedId = Shader.PropertyToID("_Speed");
    static readonly int StunnedId = Shader.PropertyToID("_Stunned");
    static readonly int LowId = Shader.PropertyToID(LowGlobalName);

    [SerializeField] Renderer bodyRenderer;
    [SerializeField] ParticleSystem trail;
    [SerializeField] float trailBaseRate = 10f;

    Shade shade;
    MaterialPropertyBlock block;
    float outline;
    float burn;
    float speed;
    float stunned;
    Vector3 lastPosition;
    bool hasLast;
    float trailBaseLifetime = -1f;
    float appliedLifetimeScale = -1f;

    public float Outline => outline;
    public float Burn => burn;
    public float Speed => speed;
    public float Stunned => stunned;

    // Stunned eases toward 1 while the Shade is stunned and back to 0 after.
    public static float StepStunned(float value, bool isStunned, float dt)
    {
        float step = dt / StunEaseSeconds;
        return isStunned ? Mathf.Min(1f, value + step) : Mathf.Max(0f, value - step);
    }
    public Renderer BodyRenderer => bodyRenderer;
    public ParticleSystem Trail => trail;

    // Burn eases up while frozen and back down after release (linear, so it is exactly 0 within BurnFallSeconds).
    public static float StepBurn(float burn, bool frozen, float dt)
    {
        if (frozen)
        {
            return Mathf.Min(1f, burn + dt / BurnRiseSeconds);
        }

        return Mathf.Max(0f, burn - dt / BurnFallSeconds);
    }

    public static float TrailLifetimeScale(GraphicsLevel level)
    {
        return level == GraphicsLevel.Low ? TrailLifetimeScaleLow : 1f;
    }

    // Emission rate for the trail: a faint wisp when idle, a thick trail at full speed, thinned on Low.
    public static float TrailRate(float baseRate, float speed, GraphicsLevel level)
    {
        float t = Mathf.Clamp01(speed / MaxSpeed);
        float rate = baseRate * Mathf.Lerp(TrailIdleRate, TrailMovingRate, t);
        return level == GraphicsLevel.Low ? rate * TrailRateScaleLow : rate;
    }

    // Writes the Low flag to the shader global. Called whenever the preset changes.
    public static void PublishQuality()
    {
        Shader.SetGlobalFloat(LowId, GraphicsQuality.Current == GraphicsLevel.Low ? 1f : 0f);
    }

    void Awake()
    {
        Resolve();
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        PublishQuality();
        hasLast = false;
        stunned = 0f;
        burn = 0f;
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnQuality(GraphicsProfile profile)
    {
        PublishQuality();
    }

    void Resolve()
    {
        if (shade == null)
        {
            shade = GetComponentInParent<Shade>();
        }

        if (bodyRenderer == null)
        {
            Transform body = transform.Find("Body");
            bodyRenderer = body != null ? body.GetComponent<Renderer>() : null;
            if (bodyRenderer == null)
            {
                Transform parentBody = transform.parent != null ? transform.parent.Find("Body") : null;
                bodyRenderer = parentBody != null ? parentBody.GetComponent<Renderer>() : null;
            }
        }
    }

    void LateUpdate()
    {
        Tick(Time.deltaTime);
        Apply();
    }

    // Reads the Shade and the flash; does not touch any renderer. Public so tests can step it without a play loop.
    public void Tick(float dt)
    {
        Resolve();
        ShadeState state = shade != null ? shade.State : ShadeState.Chase;
        burn = StepBurn(burn, state == ShadeState.Freeze, dt);
        stunned = StepStunned(stunned, state == ShadeState.Stunned, dt);
        outline = Lightning.CurrentFlash;

        Vector3 position = transform.position;
        if (dt > 0.0001f)
        {
            float raw = 0f;
            if (hasLast)
            {
                raw = (position - lastPosition).magnitude / dt;
                if (raw > TeleportSpeed)
                {
                    raw = 0f;
                }
            }

            speed = Mathf.Lerp(speed, raw, 1f - Mathf.Exp(-12f * dt));
        }

        lastPosition = position;
        hasLast = true;
    }

    void Apply()
    {
        if (bodyRenderer != null)
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            // Merge into whatever Shade.cs wrote this frame (its _BaseColor tint and alpha).
            bodyRenderer.GetPropertyBlock(block);
            block.SetFloat(OutlineId, outline);
            block.SetFloat(BurnId, burn);
            block.SetFloat(SpeedId, speed);
            block.SetFloat(StunnedId, stunned);
            bodyRenderer.SetPropertyBlock(block);
        }

        if (trail != null)
        {
            GraphicsLevel level = GraphicsQuality.Current;
            if (trailBaseLifetime < 0f)
            {
                trailBaseLifetime = trail.main.startLifetime.constant;
            }

            float scale = TrailLifetimeScale(level);
            if (!Mathf.Approximately(scale, appliedLifetimeScale))
            {
                appliedLifetimeScale = scale;
                ParticleSystem.MainModule main = trail.main;
                main.startLifetime = trailBaseLifetime * scale;
            }

            ParticleSystem.EmissionModule emission = trail.emission;
            emission.rateOverTime = TrailRate(trailBaseRate, speed, level) * ParticleQuality.Multiplier;
        }
    }
}
}
