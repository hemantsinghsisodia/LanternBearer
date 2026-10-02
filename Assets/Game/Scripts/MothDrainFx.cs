using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Visible cue that a moth is draining the lantern: a soft pale halo fades in around it (0.2 s each way)
// and a short thin trail emits while it drains. Added by Moth at runtime, so moths that are not draining
// look exactly as before. The trail is turned off on the Low graphics preset; the halo stays.
public class MothDrainFx : MonoBehaviour
{
    const float FadeSeconds = 0.2f;
    const float HaloSize = 1.2f;
    const float PeakAlpha = 0.85f;

    static readonly Color HaloColor = new Color(0.82f, 0.86f, 1f, 1f);
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static Material sharedMaterial;
    static Texture2D haloTexture;
    static MaterialPropertyBlock block;

    Transform halo;
    Renderer haloRenderer;
    TrailRenderer trail;
    Camera view;
    float amount;
    float paintedAmount = -1f;
    bool draining;
    bool trailAllowed = true;

    public float Amount => amount;
    public bool TrailEnabled => trail != null && trailAllowed;

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
        trailAllowed = GraphicsQuality.Current != GraphicsLevel.Low;
        if (trail != null && !trailAllowed)
        {
            trail.emitting = false;
            trail.Clear();
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
        haloObject.name = "DrainHalo";
        Collider collider = haloObject.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }

        haloObject.transform.SetParent(transform, false);
        haloObject.transform.localScale = Vector3.one * HaloSize;
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
        trail.time = 0.35f;
        trail.startWidth = 0.07f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(HaloColor, 0f), new GradientColorKey(HaloColor, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.emitting = false;
    }

    void Update()
    {
        if (halo == null)
        {
            return;
        }

        float dt = Time.deltaTime;
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

            Color color = HaloColor;
            color.a = amount * PeakAlpha;
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
