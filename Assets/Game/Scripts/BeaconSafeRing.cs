using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// The warm glowing ring on the ground that marks a lit beacon's safe radius, drawn as a ground-conforming mesh strip
// (a "projected" ring): every vertex is placed on the terrain height at its own XZ, so the ring hugs the ground and
// follows slopes. Vertex alpha fades the ring out wherever it should not be seen:
//  - where the terrain height differs from the beacon's base by more than FadeStart..FadeEnd (a cliff edge, a drop or a rise),
//  - where the sample is off the terrain, and
//  - at or below the water surface (plus a margin).
// Chosen over a URP decal: a mesh needs no decal feature on every quality renderer, costs one draw call and is rebuilt only
// while the radius changes. The radius is driven by BeaconVisual (the bloom from the base out to ZoneRadius over 0.3..1.2 s).
// The gameplay radius (Beacon.SafeRadius) is not touched.
public class BeaconSafeRing : MonoBehaviour
{
    public const int Segments = 96;
    public const float Width = 1.3f;
    public const float FadeStart = 0.6f;
    public const float FadeEnd = 1.1f;
    public const float WaterMargin = 0.3f;
    // Above the grass tips.
    const float HeightOffset = 0.3f;

    static Material sharedMaterial;
    static Texture2D bandTexture;

    Beacon beacon;
    BeaconVisual visual;
    MeshFilter filter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Vector3[] vertices;
    Color[] colours;
    float fittedRadius = -1f;
    float fittedAlpha = -1f;

    public Mesh RingMesh => mesh;
    public MeshRenderer RingRenderer => meshRenderer;
    public float FittedRadius => fittedRadius;

    // 0..1 visibility of a ring point from its height relative to the beacon's base, and the water surface.
    public static float Visibility(float groundHeight, bool onTerrain, float baseHeight, float waterY)
    {
        if (!onTerrain)
        {
            return 0f;
        }

        if (groundHeight < waterY + WaterMargin)
        {
            return 0f;
        }

        float diff = Mathf.Abs(groundHeight - baseHeight);
        return 1f - Mathf.Clamp01((diff - FadeStart) / (FadeEnd - FadeStart));
    }

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

    void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    // After BeaconVisual.Update, so the ring follows the radius computed this frame.
    void LateUpdate()
    {
        if (meshRenderer == null || beacon == null)
        {
            return;
        }

        if (!beacon.IsLit)
        {
            meshRenderer.enabled = false;
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
            meshRenderer.enabled = false;
            fittedRadius = -1f;
            return;
        }

        meshRenderer.enabled = true;
        if (!Mathf.Approximately(radius, fittedRadius) || !Mathf.Approximately(alpha, fittedAlpha))
        {
            Fit(radius, alpha);
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

        // An earlier version drew a LineRenderer.
        LineRenderer legacy = ringObject.GetComponent<LineRenderer>();
        if (legacy != null)
        {
            Destroy(legacy);
        }

        // World space vertices: the child must not move, rotate or scale with the beacon.
        ringObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ringObject.transform.localScale = Vector3.one;

        filter = ringObject.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = ringObject.AddComponent<MeshFilter>();
        }

        meshRenderer = ringObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = ringObject.AddComponent<MeshRenderer>();
        }

        if (mesh == null)
        {
            BuildMesh();
        }

        filter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = SharedMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;
        fittedRadius = -1f;
        fittedAlpha = -1f;
    }

    void BuildMesh()
    {
        int count = Segments * 2;
        vertices = new Vector3[count];
        colours = new Color[count];
        Vector2[] uvs = new Vector2[count];
        int[] triangles = new int[Segments * 6];
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            uvs[i * 2] = new Vector2(0f, 0f);
            uvs[i * 2 + 1] = new Vector2(0f, 1f);
            int t = i * 6;
            // Facing up.
            triangles[t] = i * 2;
            triangles[t + 1] = i * 2 + 1;
            triangles[t + 2] = next * 2;
            triangles[t + 3] = next * 2;
            triangles[t + 4] = i * 2 + 1;
            triangles[t + 5] = next * 2 + 1;
        }

        mesh = new Mesh();
        mesh.name = "BeaconSafeRing";
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colours;
        mesh.triangles = triangles;
        // Bounds are set by Fit.
    }

    void Fit(float radius, float alpha)
    {
        Vector3 centre = beacon.transform.position;
        float baseHeight = TerrainQuery.Height(centre, centre.y);
        float water = WaterHazard.SurfaceY;
        float half = Width * 0.5f;
        float a = Mathf.Clamp01(alpha);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int side = 0; side < 2; side++)
            {
                float r = radius + (side == 0 ? -half : half);
                if (r < 0f)
                {
                    r = 0f;
                }

                Vector3 p = centre + dir * r;
                float height;
                Vector3 normal;
                bool onTerrain = TerrainQuery.TrySample(p, out height, out normal);
                p.y = (onTerrain ? height : centre.y) + HeightOffset;
                vertices[i * 2 + side] = p;
                float vis = Visibility(height, onTerrain, baseHeight, water) * a;
                // Soft across the band comes from the texture; the alpha here is the plateau/water fade.
                colours[i * 2 + side] = new Color(1f, 1f, 1f, vis);
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colours;
        mesh.bounds = new Bounds(centre, new Vector3(radius * 2f + Width + 4f, 30f, radius * 2f + Width + 4f));
        fittedRadius = radius;
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
