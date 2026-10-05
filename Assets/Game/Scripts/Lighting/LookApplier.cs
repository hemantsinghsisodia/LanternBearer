using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Applies one island's look profile to the scene: sky, moonlight, ambient light, fog, the moon-rim globals and the grass root and tip colours.
// Runs in Awake and again whenever the graphics preset changes, so nothing from a previous island survives.
[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class LookApplier : MonoBehaviour
{
    [SerializeField] LookProfile profile;
    [SerializeField] Light moon;
    [SerializeField] Material skyMaterial;

    Material runtimeSky;
    bool skyAssigned;

    public static LookApplier Current { get; private set; }

    public LookProfile Profile
    {
        get { return profile; }
    }

    void Awake()
    {
        Current = this;
        Apply();
    }

    void OnEnable()
    {
        Current = this;
        GraphicsQuality.QualityChanged += HandleQualityChanged;
        if (!Application.isPlaying)
        {
            Apply();
        }
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= HandleQualityChanged;
        if (Current == this)
        {
            Current = null;
            // Globals outlive the scene, so leaving an island (into the menu) must switch the moon rim off.
            Shader.SetGlobalFloat("_LKMoonRimStrength", 0f);
            // Alpha 0 tells GrassBend to use its built-in fallback tones (the menu has no LookApplier).
            Shader.SetGlobalColor("_LKGrassRoot", Color.clear);
            Shader.SetGlobalColor("_LKGrassTip", Color.clear);
        }
    }

    void OnDestroy()
    {
        if (runtimeSky != null)
        {
            Destroy(runtimeSky);
        }
    }

    void HandleQualityChanged(GraphicsProfile graphics)
    {
        Apply();
    }

    public void Apply()
    {
        if (profile == null)
        {
            return;
        }

        Vector3 towardMoon = moon != null ? -moon.transform.forward : new Vector3(0.3f, 0.45f, 0.8f).normalized;

        // DawnSequence clones the live skybox in its own Awake, so the sky is only swapped once, before it runs.
        if (!skyAssigned || !Application.isPlaying)
        {
            Material resolved = ResolveSky();
            if (resolved != null)
            {
                RenderSettings.skybox = resolved;
                skyAssigned = true;
            }
        }

        Material sky = RenderSettings.skybox;
        // Edit mode would write into the shared NightSky asset (git churn); the island builder bakes the same direction in.
        if (Application.isPlaying && sky != null && sky.HasProperty("_MoonDir"))
        {
            sky.SetVector("_MoonDir", new Vector4(towardMoon.x, towardMoon.y, towardMoon.z, 0f));
        }

        if (moon != null)
        {
            moon.color = LookMapping.MoonColour(profile.moonOn, profile.moon, profile.moonRim);
            moon.intensity = LookMapping.MoonIntensity(profile.moonOn, profile.moonIntensity);
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;
        }

        LookMapping.AmbientValues ambient = LookMapping.Ambient(profile.sky, profile.sea, profile.land, profile.ambientIntensity);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambient.sky;
        RenderSettings.ambientEquatorColor = ambient.equator;
        RenderSettings.ambientGroundColor = ambient.ground;
        RenderSettings.ambientIntensity = ambient.intensity;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = profile.fogColour;
        RenderSettings.fogDensity = profile.fogDensity;

        Shader.SetGlobalColor("_LKMoonRimColor", profile.moonRim);
        Shader.SetGlobalFloat("_LKMoonRimStrength", profile.moonRimStrength);
        Shader.SetGlobalVector("_LKMoonDir", new Vector4(towardMoon.x, towardMoon.y, towardMoon.z, 0f));

        Shader.SetGlobalColor("_LKGrassRoot", GroundPalette.GrassRootFor(profile));
        Shader.SetGlobalColor("_LKGrassTip", GroundPalette.GrassTipFor(profile));

        ApplyWater(profile, towardMoon);
        ApplyRidges(profile);
    }

    // Per-frame global so the water (and anything else) can brighten on a lightning flash and fall back to 0 after it.
    void Update()
    {
        if (Application.isPlaying)
        {
            Shader.SetGlobalFloat("_LKLightningFlash", Lightning.CurrentFlash);
        }
    }

    // Flat silhouette layers from the island palette: Range0..2 nearest to farthest, Ridge* (Ultra only) at the far colour,
    // islets and their pines slightly darker than the nearest range. The haze takes the fog colour. Runtime instances only.
    void ApplyRidges(LookProfile p)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        GameObject horizon = GameObject.Find("Horizon");
        if (horizon == null)
        {
            return;
        }

        Color baseLand = LookMapping.OrDerived(p.ridgeColour, p.land);
        Color[] layers = LookMapping.RidgeLayers(baseLand, p.skyHorizon, 3);
        Renderer[] renderers = horizon.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material shared = renderers[i].sharedMaterial;
            if (shared == null || shared.shader == null)
            {
                continue;
            }

            string shaderName = shared.shader.name;
            string objectName = renderers[i].name;
            if (shaderName == "LanternKeeper/DistantRange")
            {
                Color c;
                if (objectName.StartsWith("Range") || objectName.StartsWith("Ridge"))
                {
                    int layer = objectName.StartsWith("Range") && objectName.Length > 5 ? Mathf.Clamp(objectName[5] - '0', 0, layers.Length - 1) : layers.Length - 1;
                    c = layers[layer];
                }
                else
                {
                    // Islet, Pine, PineTip.
                    c = layers[0] * 0.85f;
                }

                c.a = 1f;
                shared.SetColor("_LayerColor", c);
            }
            else if (shaderName == "LanternKeeper/HorizonHaze")
            {
                Color haze = p.fogColour;
                haze.a = shared.GetColor("_BaseColor").a;
                shared.SetColor("_BaseColor", haze);
            }
        }
    }

    // Sets the island's water colours on the live water materials. Runtime instances only, so the saved asset never changes.
    void ApplyWater(LookProfile p, Vector3 towardMoon)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material shared = renderers[i].sharedMaterial;
            if (shared == null || shared.shader == null || shared.shader.name != "LanternKeeper/Water")
            {
                continue;
            }

            Material water = renderers[i].material;
            // Keep the material's own alpha: shallow alpha is what lets the bank, stones and bed show through.
            Color shallow = LookMapping.OrDerived(p.waterShallow, LookMapping.WaterShallow(p.sea, p.moonRim));
            Color deep = LookMapping.OrDerived(p.waterDeep, LookMapping.WaterDeep(p.sea));
            shallow.a = water.GetColor("_ShallowColor").a;
            deep.a = water.GetColor("_DeepColor").a;
            water.SetColor("_ShallowColor", shallow);
            water.SetColor("_DeepColor", deep);
            water.SetColor("_FoamColor", LookMapping.OrDerived(p.foamColour, LookMapping.Foam(p.moonRim)));
            water.SetColor("_RimColor", p.moonRim);
            water.SetColor("_TideBandColor", p.tideBand);
            water.SetVector("_MoonDir", new Vector4(towardMoon.x, 0f, towardMoon.z, p.moonOn ? 1f : 0f));
        }
    }

    // In play mode the sky is an instance, so per-island writes never dirty the asset and DawnSequence clones the same thing.
    Material ResolveSky()
    {
        if (skyMaterial == null)
        {
            return null;
        }

        if (!Application.isPlaying)
        {
            return skyMaterial;
        }

        if (runtimeSky == null)
        {
            runtimeSky = new Material(skyMaterial);
            runtimeSky.name = skyMaterial.name + " (Runtime)";
        }

        return runtimeSky;
    }
}
}
