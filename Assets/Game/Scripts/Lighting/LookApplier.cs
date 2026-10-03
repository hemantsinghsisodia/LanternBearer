using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Applies one island's look profile to the scene: sky, moonlight, ambient light, fog and the moon-rim globals.
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
        if (sky != null && sky.HasProperty("_MoonDir"))
        {
            sky.SetVector("_MoonDir", new Vector4(towardMoon.x, towardMoon.y, towardMoon.z, 0f));
        }

        if (moon != null)
        {
            moon.color = new Color(profile.moon.r, profile.moon.g, profile.moon.b, 1f);
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
