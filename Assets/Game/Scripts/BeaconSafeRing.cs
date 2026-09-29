using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
public class BeaconSafeRing : MonoBehaviour
{
    static Material sharedMaterial;

    Beacon beacon;
    LineRenderer ring;
    bool fitted;

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

    void Update()
    {
        if (beacon == null)
        {
            beacon = GetComponent<Beacon>();
        }

        bool show = beacon != null && beacon.IsLit;
        if (ring != null)
        {
            ring.enabled = show;
        }

        if (show && !fitted)
        {
            Fit();
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

        ring.loop = true;
        ring.useWorldSpace = true;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        ring.numCapVertices = 4;
        ring.numCornerVertices = 2;
        ring.widthMultiplier = 0.45f;
        ring.positionCount = 64;
        ring.material = SharedMaterial();
        ring.textureMode = LineTextureMode.Stretch;
        Color glow = new Color(1f, 0.58f, 0.22f, 0.85f);
        ring.startColor = glow;
        ring.endColor = glow;
        ring.enabled = false;
        fitted = false;
    }

    void Fit()
    {
        if (ring == null || beacon == null)
        {
            return;
        }

        float radius = Mathf.Max(1f, beacon.ZoneRadius);
        int count = ring.positionCount;
        Vector3 center = beacon.transform.position;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            float height = TerrainQuery.Height(point, center.y);
            point.y = height + 0.35f;
            ring.SetPosition(i, point);
        }

        Color glow = new Color(1f, 0.58f, 0.22f, 0.9f);
        Light beaconLight = beacon.GetComponentInChildren<Light>(true);
        if (beaconLight != null)
        {
            glow = beaconLight.color;
            glow.a = 0.9f;
        }

        ring.startColor = glow;
        ring.endColor = glow;
        ring.widthMultiplier = 0.85f;
        fitted = true;
    }

    static Material SharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        sharedMaterial = new Material(shader);
        sharedMaterial.name = "BeaconSafeRing";
        Color color = new Color(1f, 0.58f, 0.22f, 0.9f);
        if (sharedMaterial.HasProperty("_BaseColor"))
        {
            sharedMaterial.SetColor("_BaseColor", color);
        }

        if (sharedMaterial.HasProperty("_Color"))
        {
            sharedMaterial.SetColor("_Color", color);
        }

        if (sharedMaterial.HasProperty("_Surface"))
        {
            sharedMaterial.SetFloat("_Surface", 1f);
        }

        sharedMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        sharedMaterial.SetInt("_DstBlend", (int)BlendMode.One);
        sharedMaterial.SetInt("_ZWrite", 0);
        sharedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedMaterial.renderQueue = 3000;
        return sharedMaterial;
    }
}
}
