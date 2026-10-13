using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
public class DawnSequence : MonoBehaviour
{
    [SerializeField] float duration = 6f;
    [SerializeField] Light sun;
    [SerializeField] StarTwinkle stars;
    [SerializeField] Vector3 dawnEuler = new Vector3(10f, 28f, 0f);
    [SerializeField] Volume lookVolume;
    [SerializeField] Volume dawnVolume;
    // How far the dawn goes (1 = the full sunrise). Islands 1-4 end in a growing pre-dawn (PreDawnInstaller); the finale always runs at 1.
    [SerializeField, Range(0f, 1f)] float reach = 1f;

    Color nightLightColor = new Color(0.75f, 0.84f, 1f);
    float nightIntensity = 0.85f;
    Quaternion nightRotation = Quaternion.Euler(38f, -35f, 0f);
    Color nightFog = new Color(0.4f, 0.52f, 0.56f);
    float nightFogDensity = 0.016f;
    Color nightSky = new Color(0.04f, 0.06f, 0.14f);
    Color nightEquator = new Color(0.04f, 0.16f, 0.18f);
    Color nightGround = new Color(0.012f, 0.012f, 0.015f);
    Color skyTop = new Color(0.015f, 0.03f, 0.09f);
    Color skyHorizon = new Color(0.07f, 0.22f, 0.26f);
    Color skyGround = new Color(0.005f, 0.006f, 0.01f);
    Color nightHaze = new Color(0.07f, 0.16f, 0.2f, 1f);
    Color nightTint = Color.white;
    float nightExposure = 0.38f;
    float nightAmbient = 0.18f;
    Color nightWater = new Color(0.72f, 0.84f, 1f, 1f);
    Color nightSilhouette = new Color(0.58f, 0.68f, 0.82f, 1f);
    Material skybox;
    bool captured;
    float nightLookWeight = 1f;
    float nightRimStrength = 1f;
    Color nightGrassRoot;
    Color nightGrassTip;

    public bool IsPlaying { get; private set; }
    // The light that is the sun at dawn (the night's moonlight turns into it), and the direction towards the sun when the dawn is complete.
    public Light SunLight => sun;
    public Vector3 DawnSunDirection => -(Quaternion.Euler(dawnEuler) * Vector3.forward);
    // A reach of 0 would divide by zero in the pre-dawn light blend.
    const float MinReach = 0.05f;
    public float Reach => reach;
    // The sun in the sky: 0 for a pre-dawn (reach < 1, no sun at all), the dawn progress for the full sunrise.
    public float SkySunIntensity { get; private set; }

    // Finale-only overrides (Island 5). The defaults are the values the dawn always used, so Islands 1-4 are unchanged.
    [System.NonSerialized] public float FogDensityScale = 0.45f;
    [System.NonSerialized] public float DawnGradeWeight = 1f;
    [System.NonSerialized] public float WaterWarmth = 1f;
    // Alpha 0 = no override. SkyTop and Horizon replace the dawn sky colours (and the dawn fog follows Horizon); Ambient replaces the dawn ambient sky and equator.
    [System.NonSerialized] public Color SkyTopOverride = new Color(0f, 0f, 0f, 0f);
    [System.NonSerialized] public Color HorizonOverride = new Color(0f, 0f, 0f, 0f);
    [System.NonSerialized] public Color AmbientOverride = new Color(0f, 0f, 0f, 0f);
    [System.NonSerialized] public float ExposureScale = 1f;
    [System.NonSerialized] public Color WaterTintOverride = new Color(0f, 0f, 0f, 0f);

    public static void ApplyNightGlobals()
    {
        Shader.SetGlobalColor("_WaterTint", new Color(0.72f, 0.84f, 1f, 1f));
        Shader.SetGlobalColor("_SilhouetteTint", new Color(0.58f, 0.68f, 0.82f, 1f));
        Shader.SetGlobalFloat("_LanternSkyBlend", 0f);
    }

    void Awake()
    {
        if (sun == null)
        {
            GameObject moon = GameObject.Find("Moonlight");
            if (moon != null)
            {
                sun = moon.GetComponent<Light>();
            }
        }

        if (stars == null)
        {
            stars = FindAnyObjectByType<StarTwinkle>();
        }

        if (RenderSettings.skybox != null)
        {
            skybox = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skybox;
        }

        ApplyNightGlobals();
        PushMoonDir();
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat("_LanternSkyBlend", 0f);
    }

    void Start()
    {
        RenderSkyProbe();
    }

    public void Play(System.Action onComplete)
    {
        if (IsPlaying)
        {
            return;
        }

        StartCoroutine(Run(onComplete, duration, false, Mathf.Clamp(reach, MinReach, 1f)));
    }

    // The Island 5 finale's sunrise: its own length, on unscaled time (the finale keeps running at timeScale 0). The other islands
    // call Play(Action) above, which is unchanged: the serialized duration on scaled time.
    public void Play(System.Action onComplete, float durationSeconds, bool unscaled)
    {
        if (IsPlaying)
        {
            return;
        }

        StartCoroutine(Run(onComplete, Mathf.Max(0.01f, durationSeconds), unscaled, 1f));
    }

    System.Collections.IEnumerator Run(System.Action onComplete, float runDuration, bool unscaled, float runReach)
    {
        bool preDawn = runReach < 0.999f;
        IsPlaying = true;
        CaptureNight();
        float elapsed = 0f;
        Quaternion dawnRotation = Quaternion.Euler(dawnEuler);
        Color dawnLight = new Color(1f, 0.72f, 0.48f);
        Color dawnFog = new Color(1f, 0.72f, 0.58f);
        Color dawnSky = new Color(0.95f, 0.62f, 0.5f);
        Color dawnEquator = new Color(0.92f, 0.5f, 0.36f);
        Color dawnGround = new Color(0.38f, 0.18f, 0.12f);
        Color dawnTop = new Color(0.45f, 0.38f, 0.62f);
        Color dawnHorizon = new Color(1f, 0.62f, 0.42f);
        Color dawnBottom = new Color(0.48f, 0.24f, 0.18f);
        float ambientBoost = 0.28f;
        // The island's DawnLook drives the sun, fog and ambient; the constants above are only the fallback without a look.
        LookApplier applier = LookApplier.Current;
        DawnLook look = applier != null && applier.Profile != null ? applier.Profile.dawn : null;
        if (look != null)
        {
            dawnLight = look.dawnLight;
            dawnFog = look.dawnHorizon;
            dawnSky = look.dawnTop;
            dawnEquator = look.dawnHorizon;
            dawnGround = look.dawnGround;
            dawnTop = look.dawnTop;
            dawnHorizon = look.dawnHorizon;
            dawnBottom = look.dawnGround;
            ambientBoost = look.dawnAmbientBoost;
        }

        if (HorizonOverride.a > 0f)
        {
            dawnFog = HorizonOverride;
            dawnHorizon = HorizonOverride;
            if (skybox != null && skybox.HasProperty("_DawnHorizon"))
            {
                skybox.SetColor("_DawnHorizon", HorizonOverride);
            }
        }

        if (SkyTopOverride.a > 0f && skybox != null && skybox.HasProperty("_DawnTop"))
        {
            skybox.SetColor("_DawnTop", SkyTopOverride);
        }

        if (AmbientOverride.a > 0f)
        {
            dawnSky = AmbientOverride;
            dawnEquator = AmbientOverride;
        }

        Color dawnHaze = new Color(1f, 0.64f, 0.42f, 1f);
        Color dawnTint = new Color(1f, 0.94f, 0.86f, 1f);
        Color dawnWater = new Color(1f, 0.7f, 0.5f, 1f);
        if (WaterTintOverride.a > 0f)
        {
            dawnWater = WaterTintOverride;
        }
        Color dawnSilhouette = new Color(1f, 0.64f, 0.46f, 1f);
        bool blendSky = skybox != null && skybox.HasProperty("_Blend");

        // Grass follows the dawn too: the island land colour pulled toward the dawn ground, lit by the dawn light. Daylight, so mild warmth is fine.
        bool blendGrass = nightGrassRoot.a > 0.5f && applier != null && applier.Profile != null && look != null;
        Color dawnGrassRoot = nightGrassRoot;
        Color dawnGrassTip = nightGrassTip;
        if (blendGrass)
        {
            LookProfile gp = applier.Profile;
            if (gp.grassRoot.a > 0f && gp.grassTip.a > 0f)
            {
                // Same island hue, pulled a little toward the dawn ground and lit by the dawn light.
                dawnGrassRoot = Color.Lerp(gp.grassRoot, look.dawnGround, 0.2f);
                dawnGrassRoot.a = 1f;
                dawnGrassTip = LookMapping.GrassTipLit(Color.Lerp(gp.grassTip, look.dawnGround, 0.2f), look.dawnLight);
            }
            else
            {
                Color dawnLand = Color.Lerp(gp.land, look.dawnGround, 0.35f);
                dawnGrassRoot = LookMapping.GrassRoot(dawnLand);
                dawnGrassTip = LookMapping.GrassTip(dawnLand, look.dawnLight);
            }
        }

        while (elapsed < runDuration)
        {
            elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = PreDawn.Blend(elapsed / runDuration, runReach);
            SkySunIntensity = preDawn ? 0f : u;
            // A pre-dawn warms the horizon first and the rest of the world far less, so the night stays night: sky top, ambient, grass and the grade
            // follow us, fog, water, silhouettes and light colour um, the horizon band uh. A full dawn uses u for all of them (unchanged).
            float us = preDawn ? u * 0.2f : u;
            float um = preDawn ? u * 0.35f : u;
            float uh = preDawn ? Mathf.Clamp01(u * 1.8f) : u;
            if (sun != null)
            {
                if (preDawn)
                {
                    // The moon stays the light: a touch warmer and brighter, and barely turned, so no low sun throws long shadows.
                    sun.color = Color.Lerp(nightLightColor, dawnLight, um);
                    sun.intensity = Mathf.Lerp(nightIntensity, nightIntensity * 1.15f, u / runReach);
                    sun.transform.rotation = Quaternion.Slerp(nightRotation, dawnRotation, u * 0.12f);
                }
                else
                {
                    sun.color = Color.Lerp(nightLightColor, dawnLight, u);
                    sun.intensity = Mathf.Lerp(nightIntensity, 1.45f, u);
                    sun.transform.rotation = Quaternion.Slerp(nightRotation, dawnRotation, u);
                }
            }

            RenderSettings.fogColor = Color.Lerp(nightFog, dawnFog, um);
            RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, nightFogDensity * FogDensityScale, um);
            RenderSettings.ambientSkyColor = Color.Lerp(nightSky, dawnSky, us);
            RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dawnEquator, us);
            RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dawnGround, us);
            if (blendSky)
            {
                RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbient, nightAmbient + ambientBoost, us);
            }

            if (skybox != null)
            {
                // NightSky lerps night to dawn itself from _Blend and _Dawn*, so writing here would compound the blend.
                if (skybox.HasProperty("_Top") && !skybox.HasProperty("_DawnTop"))
                {
                    skybox.SetColor("_Top", Color.Lerp(skyTop, dawnTop, us));
                    skybox.SetColor("_Horizon", Color.Lerp(skyHorizon, dawnHorizon, uh));
                    skybox.SetColor("_Ground", Color.Lerp(skyGround, dawnBottom, us));
                }

                if (blendSky)
                {
                    skybox.SetFloat("_Blend", us);
                    if (preDawn && skybox.HasProperty("_HorizonBlend"))
                    {
                        skybox.SetFloat("_HorizonBlend", uh);
                    }

                    if (preDawn && skybox.HasProperty("_MoonAlpha"))
                    {
                        // The moon disc fades out over a pre-dawn, so it is never mistaken for a sun.
                        skybox.SetFloat("_MoonAlpha", 1f - Mathf.Clamp01(u / runReach));
                    }

                    float dawnExposure = nightExposure * 1.7f;
                    if (skybox.HasProperty("_DawnExposure"))
                    {
                        float stored = skybox.GetFloat("_DawnExposure");
                        if (stored > 0.05f)
                        {
                            dawnExposure = stored;
                        }
                    }

                    dawnExposure *= ExposureScale;
                    skybox.SetFloat("_Exposure", Mathf.Lerp(nightExposure, dawnExposure, us));
                    skybox.SetColor("_Tint", Color.Lerp(nightTint, dawnTint, us));
                    if (skybox.HasProperty("_HazeColor"))
                    {
                        skybox.SetColor("_HazeColor", Color.Lerp(nightHaze, dawnHaze, uh));
                    }

                    PushMoonDir();
                }
            }

            // The dawn grade takes over from the night grade, and the moon rim fades out with the moon.
            if (lookVolume != null)
            {
                lookVolume.weight = Mathf.Lerp(nightLookWeight, 0f, us);
            }

            if (dawnVolume != null)
            {
                dawnVolume.weight = us * DawnGradeWeight;
            }

            Shader.SetGlobalFloat("_LKMoonRimStrength", Mathf.Lerp(nightRimStrength, 0f, u));
            Shader.SetGlobalFloat("_LanternSkyBlend", blendSky ? (preDawn ? uh * 0.5f : u) : 0f);
            Shader.SetGlobalColor("_WaterTint", Color.Lerp(nightWater, Color.Lerp(Color.white, dawnWater, WaterWarmth), um));
            if (blendGrass)
            {
                Shader.SetGlobalColor("_LKGrassRoot", Color.Lerp(nightGrassRoot, dawnGrassRoot, us));
                Shader.SetGlobalColor("_LKGrassTip", Color.Lerp(nightGrassTip, dawnGrassTip, us));
            }
            Shader.SetGlobalColor("_SilhouetteTint", Color.Lerp(nightSilhouette, dawnSilhouette, um));

            if (stars != null)
            {
                stars.SetFade(preDawn ? 1f - u * 0.6f : 1f - u);
            }

            yield return null;
        }

        RenderSkyProbe();
        DynamicGI.UpdateEnvironment();
        IsPlaying = false;
        if (onComplete != null)
        {
            onComplete.Invoke();
        }
    }

    void CaptureNight()
    {
        if (captured)
        {
            return;
        }

        captured = true;
        if (sun != null)
        {
            nightLightColor = sun.color;
            // The authored night values, not the brightness-scaled ones: UserSettingsApplier keeps scaling what dawn writes.
            nightIntensity = UserSettingsApplier.AuthoredMoon(sun);
            nightRotation = sun.transform.rotation;
        }

        nightFog = RenderSettings.fogColor;
        nightFogDensity = RenderSettings.fogDensity;
        nightSky = UserSettingsApplier.AuthoredSky;
        nightEquator = UserSettingsApplier.AuthoredEquator;
        nightGround = UserSettingsApplier.AuthoredGround;
        nightAmbient = UserSettingsApplier.AuthoredAmbientIntensity;
        nightRimStrength = Shader.GetGlobalFloat("_LKMoonRimStrength");
        if (lookVolume != null)
        {
            nightLookWeight = lookVolume.weight;
        }

        nightWater = Shader.GetGlobalColor("_WaterTint");
        nightGrassRoot = Shader.GetGlobalColor("_LKGrassRoot");
        nightGrassTip = Shader.GetGlobalColor("_LKGrassTip");
        nightSilhouette = Shader.GetGlobalColor("_SilhouetteTint");
        if (skybox != null && skybox.HasProperty("_Top"))
        {
            skyTop = skybox.GetColor("_Top");
            skyHorizon = skybox.GetColor("_Horizon");
            skyGround = skybox.GetColor("_Ground");
        }

        if (skybox != null && skybox.HasProperty("_Blend"))
        {
            nightExposure = skybox.GetFloat("_Exposure");
            nightTint = skybox.GetColor("_Tint");
            if (skybox.HasProperty("_HazeColor"))
            {
                nightHaze = skybox.GetColor("_HazeColor");
            }
        }
    }

    void PushMoonDir()
    {
        if (skybox == null || sun == null || !skybox.HasProperty("_MoonDir"))
        {
            return;
        }

        Vector3 toward = -sun.transform.forward;
        skybox.SetVector("_MoonDir", new Vector4(toward.x, toward.y, toward.z, 0f));
    }

    static void RenderSkyProbe()
    {
        ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null && probes[i].mode == ReflectionProbeMode.Realtime)
            {
                probes[i].RenderProbe();
            }
        }
    }
}
}
