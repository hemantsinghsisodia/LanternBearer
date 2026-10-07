using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// The warm glowing ring on the ground that marks a lit beacon's safe radius. A soft additive band lying flat on the terrain,
// in the LookPalette amber with a core-coloured heart. Its radius is driven by BeaconVisual (the bloom from the base out to
// the full ZoneRadius over 0.3..1.2 s, then steady); without a BeaconVisual it is simply the full radius.
public class BeaconSafeRing : MonoBehaviour
{
    const int Points = 72;
    // Above the grass tips, or the band disappears into the blades.
    const float HeightOffset = 0.65f;
    // The ring stays within this band of the beacon's own height, so it never drops down a cliff face in a vertical ribbon.
    const float MaxBelow = 0.6f;
    const float MaxAbove = 1.5f;
    const float BandWidth = 1.4f;

    static Material sharedMaterial;
    static Texture2D bandTexture;

    Beacon beacon;
    BeaconVisual visual;
    LineRenderer ring;
    float fittedRadius = -1f;
    float fittedAlpha = -1f;

    public LineRenderer Ring => ring;
    public float FittedRadius => fittedRadius;

    public static void Ensure(Beacon owner)
    {
        if (owner == null)
        {
            return;
        }

        BeaconSafeRing marker = owner.GetComponent<BeaconSafeRing>();
        if (marker == null)
        {
            marker = owner.gameObject.AddComponent<BeaconSafeRing>();
        }

        marker.Build();
    }

    void Awake()
    {
        beacon = GetComponent<Beacon>();
        Build();
    }

    // After BeaconVisual.Update, so the ring follows the radius computed this frame.
    void LateUpdate()
    {
        if (ring == null || beacon == null)
        {
            return;
        }

        if (!beacon.IsLit)
        {
            ring.enabled = false;
            fittedRadius = -1f;
            fittedAlpha = -1f;
            return;
        }

        if (visual == null)
        {
            visual = GetComponentInChildren<BeaconVisual>(true);
        }

        float radius = visual != null ? visual.RingRadius : Mathf.Max(1f, beacon.ZoneRadius);
        float alpha = visual != null ? visual.RingAlpha : 1f;
        if (radius < 0.05f)
        {
            ring.enabled = false;
            fittedRadius = -1f;
            return;
        }

        ring.enabled = true;
        if (!Mathf.Approximately(radius, fittedRadius))
        {
            Fit(radius);
        }

        if (!Mathf.Approximately(alpha, fittedAlpha))
        {
            SetAlpha(alpha);
        }
    }

    public void Build()
    {
        if (beacon == null)
        {
            beacon = GetComponent<Beacon>();
        }

        Transform existing = transform.Find("SafeRing");
        GameObject ringObject = existing != null ? existing.gameObject : new GameObject("SafeRing");
        if (existing == null)
        {
            ringObject.transform.SetParent(transform, false);
        }

        ring = ringObject.GetComponent<LineRenderer>();
        if (ring == null)
        {
            ring = ringObject.AddComponent<LineRenderer>();
        }

        // The band lies flat on the ground: the line faces along its transform's Z, which points up.
        ringObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ring.alignment = LineAlignment.TransformZ;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        ring.numCapVertices = 0;
        ring.numCornerVertices = 2;
        ring.widthMultiplier = BandWidth;
        ring.positionCount = Points;
        ring.textureMode = LineTextureMode.Stretch;
        ring.sharedMaterial = SharedMaterial();
        ring.enabled = false;
        fittedRadius = -1f;
        fittedAlpha = -1f;
    }

    void Fit(float radius)
    {
        Vector3 center = beacon.transform.position;
        for (int i = 0; i < Points; i++)
        {
            float angle = i * Mathf.PI * 2f / Points;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            float ground = Mathf.Clamp(TerrainQuery.Height(point, center.y), center.y - MaxBelow, center.y + MaxAbove);
            point.y = ground + HeightOffset;
            ring.SetPosition(i, point);
        }

        fittedRadius = radius;
    }

    void SetAlpha(float alpha)
    {
        Color amber = LookPalette.FromHex(LookPalette.LanternAmber);
        amber.a = Mathf.Clamp01(alpha);
        ring.startColor = amber;
        ring.endColor = amber;
        fittedAlpha = alpha;
    }

    // Soft across the band (V): a bright core line fading to nothing at both edges.
    static Texture2D BandTexture()
    {
        if (bandTexture != null)
        {
            return bandTexture;
        }

        const int height = 32;
        bandTexture = new Texture2D(2, height, TextureFormat.RGBA32, false);
        bandTexture.name = "BeaconSafeRingBand";
        bandTexture.wrapMode = TextureWrapMode.Clamp;
        bandTexture.filterMode = FilterMode.Bilinear;
        Color amber = LookPalette.FromHex(LookPalette.LanternAmber);
        Color core = LookPalette.FromHex(LookPalette.GlowCore);
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height * 2f - 1f;
            float edge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.6f);
            float heart = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 2.2f), 2f);
            Color c = Color.Lerp(amber, core, heart);
            c.a = Mathf.Clamp01(edge * 0.8f + heart * 0.35f);
            for (int x = 0; x < 2; x++)
            {
                bandTexture.SetPixel(x, y, c);
            }
        }

        bandTexture.Apply(false, true);
        return bandTexture;
    }

    static Material SharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        Shader shader = Shader.Find("LanternKeeper/AdditiveUnlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        sharedMaterial = new Material(shader);
        sharedMaterial.name = "BeaconSafeRing";
        sharedMaterial.SetTexture("_BaseMap", BandTexture());
        sharedMaterial.SetColor("_BaseColor", Color.white);
        sharedMaterial.renderQueue = 3000;
        return sharedMaterial;
    }
}
}
