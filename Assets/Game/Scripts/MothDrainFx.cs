using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Visible cue that a moth is draining the lantern: a soft violet aura (LookPalette.DrainViolet) fades in around it
// (0.2 s each way), a short thin violet trail and falling pale wing dust appear while it drains. Added by Moth at
// runtime, so moths that are not draining look exactly as before. On the Low graphics preset the trail is off and the
// dust rate is lower; the aura stays. The aura and the dust stop as soon as draining stops, and everything is a child
// of the moth, so it goes with the moth when it is destroyed.
public class MothDrainFx : MonoBehaviour
{
    const float FadeSeconds = 0.2f;
    const float AuraSize = 1.1f;
    const float PeakAlpha = 0.7f;
    public const float DustRatePerSecond = 12f;
    public const float DustRateLow = 5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static Material sharedMaterial;
    static Texture2D haloTexture;
    static MaterialPropertyBlock block;

    Transform halo;
    Renderer haloRenderer;
    TrailRenderer trail;
    ParticleSystem dust;
    Camera view;
    float amount;
    float paintedAmount = -1f;
    bool draining;
    bool trailAllowed = true;
    bool low;

    public static Color AuraBaseColor
    {
        get { return LookPalette.FromHex(LookPalette.DrainViolet); }
    }

    public float Amount => amount;
    public bool TrailEnabled => trail != null && trailAllowed;

    // Current aura colour: the drain violet, with alpha following the fade.
    public Color AuraColor
    {
        get
        {
            Color color = AuraBaseColor;
            color.a = amount * PeakAlpha;
            return color;
        }
    }

    public bool DustEmitting => dust != null && dust.emission.enabled;
    public float DustRate => dust != null ? dust.emission.rateOverTime.constant : 0f;

    public void SetDraining(bool value)
    {
        draining = value;
    }

    void Awake()
    {
        Build();
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        ApplyQuality();
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnQuality(GraphicsProfile profile)
    {
        ApplyQuality();
    }

    void ApplyQuality()
    {
        low = GraphicsQuality.Current == GraphicsLevel.Low;
        trailAllowed = !low;
        if (trail != null && !trailAllowed)
        {
            trail.emitting = false;
            trail.Clear();
        }

        if (dust != null)
        {
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.rateOverTime = low ? DustRateLow : DustRatePerSecond;
        }
    }

    void Build()
    {
        if (halo != null)
        {
            return;
        }

        Material material = SharedMaterial();
        if (material == null)
        {
            return;
        }

        GameObject haloObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        haloObject.name = "DrainAura";
        Collider collider = haloObject.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }

        haloObject.transform.SetParent(transform, false);
        haloObject.transform.localScale = Vector3.one * AuraSize;
        haloRenderer = haloObject.GetComponent<Renderer>();
        haloRenderer.sharedMaterial = material;
        haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
        haloRenderer.enabled = false;
        halo = haloObject.transform;

        GameObject trailObject = new GameObject("DrainTrail");
        trailObject.transform.SetParent(transform, false);
        trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = 0.3f;
        trail.startWidth = 0.035f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Color violet = AuraBaseColor;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(violet, 0f), new GradientColorKey(violet, 1f) },
            new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.emitting = false;

        BuildDust(material);
        ApplyQuality();
    }

    void BuildDust(Material material)
    {
        GameObject dustObject = new GameObject("DrainDust");
        dustObject.transform.SetParent(transform, false);
        dust = dustObject.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.3f;
        main.startSpeed = 0.03f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        main.startColor = new Color(0.96f, 0.91f, 0.82f, 0.8f);
        main.gravityModifier = 0.05f;
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = DustRatePerSecond;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.35f, 0.01f, 0.16f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dust.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        ParticleSystemRenderer particleRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        if (Application.isPlaying)
        {
            dust.Play();
        }
    }

    void Update()
    {
        Tick(Time.deltaTime);
    }

    // One step of the fade. Update calls this each frame; tests call it with a fixed step.
    public void Tick(float dt)
    {
        Build();
        if (halo == null)
        {
            return;
        }

        amount = Mathf.MoveTowards(amount, draining ? 1f : 0f, dt / FadeSeconds);
        bool show = amount > 0.001f;
        if (haloRenderer.enabled != show)
        {
            haloRenderer.enabled = show;
        }

        bool emit = draining && trailAllowed;
        if (trail.emitting != emit)
        {
            trail.emitting = emit;
        }

        if (dust != null)
        {
            ParticleSystem.EmissionModule emission = dust.emission;
            if (emission.enabled != draining)
            {
                emission.enabled = draining;
            }
        }

        if (!show)
        {
            paintedAmount = -1f;
            return;
        }

        if (view == null)
        {
            view = Camera.main;
        }

        if (view != null)
        {
            halo.rotation = view.transform.rotation;
        }

        if (Mathf.Abs(amount - paintedAmount) > 0.002f)
        {
            paintedAmount = amount;
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            Color color = AuraColor;
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            haloRenderer.SetPropertyBlock(block);
        }
    }

    static Material SharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        sharedMaterial = new Material(shader);
        sharedMaterial.name = "MothDrainGlow";
        Texture2D texture = HaloTexture();
        if (sharedMaterial.HasProperty("_BaseMap"))
        {
            sharedMaterial.SetTexture("_BaseMap", texture);
        }

        sharedMaterial.mainTexture = texture;
        if (sharedMaterial.HasProperty("_Surface"))
        {
            sharedMaterial.SetFloat("_Surface", 1f);
        }

        if (sharedMaterial.HasProperty("_Blend"))
        {
            sharedMaterial.SetFloat("_Blend", 2f);
        }

        sharedMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        sharedMaterial.SetInt("_DstBlend", (int)BlendMode.One);
        sharedMaterial.SetInt("_ZWrite", 0);
        sharedMaterial.SetInt("_Cull", (int)CullMode.Off);
        sharedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedMaterial.renderQueue = 3000;
        return sharedMaterial;
    }

    // Soft radial falloff, white with alpha, so the quad reads as a glow rather than a disc.
    static Texture2D HaloTexture()
    {
        if (haloTexture != null)
        {
            return haloTexture;
        }

        const int size = 64;
        haloTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        haloTexture.name = "MothDrainHalo";
        haloTexture.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float a = Mathf.Clamp01(1f - r);
                a *= a;
                byte alpha = (byte)Mathf.RoundToInt(a * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        haloTexture.SetPixels32(pixels);
        haloTexture.Apply(false, true);
        return haloTexture;
    }
}
}
