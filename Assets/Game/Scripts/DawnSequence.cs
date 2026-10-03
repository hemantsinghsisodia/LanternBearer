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
    Color nightHazeTint = new Color(0.55f, 0.7f, 0.78f, 1f);
    Material skybox;
    bool captured;

    public bool IsPlaying { get; private set; }

    public static void ApplyNightGlobals()
    {
        Shader.SetGlobalColor("_WaterTint", new Color(0.72f, 0.84f, 1f, 1f));
        Shader.SetGlobalColor("_SilhouetteTint", new Color(0.58f, 0.68f, 0.82f, 1f));
        Shader.SetGlobalColor("_HazeTint", new Color(0.55f, 0.7f, 0.78f, 1f));
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

        StartCoroutine(Run(onComplete));
    }

    System.Collections.IEnumerator Run(System.Action onComplete)
    {
        IsPlaying = true;
        CaptureNight();
        float elapsed = 0f;
        Color dawnLight = new Color(1f, 0.72f, 0.48f);
        Quaternion dawnRotation = Quaternion.Euler(dawnEuler);
        Color dawnFog = new Color(1f, 0.72f, 0.58f);
        Color dawnSky = new Color(0.95f, 0.62f, 0.5f);
        Color dawnEquator = new Color(0.92f, 0.5f, 0.36f);
        Color dawnGround = new Color(0.38f, 0.18f, 0.12f);
        Color dawnTop = new Color(0.45f, 0.38f, 0.62f);
        Color dawnHorizon = new Color(1f, 0.62f, 0.42f);
        Color dawnBottom = new Color(0.48f, 0.24f, 0.18f);
        Color dawnHaze = new Color(1f, 0.64f, 0.42f, 1f);
        Color dawnTint = new Color(1f, 0.94f, 0.86f, 1f);
        Color dawnWater = new Color(1f, 0.7f, 0.5f, 1f);
        Color dawnSilhouette = new Color(1f, 0.64f, 0.46f, 1f);
        Color dawnHazeTint = new Color(1f, 0.72f, 0.5f, 1f);
        bool blendSky = skybox != null && skybox.HasProperty("_Blend");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (sun != null)
            {
                sun.color = Color.Lerp(nightLightColor, dawnLight, u);
                sun.intensity = Mathf.Lerp(nightIntensity, 1.45f, u);
                sun.transform.rotation = Quaternion.Slerp(nightRotation, dawnRotation, u);
            }

            RenderSettings.fogColor = Color.Lerp(nightFog, dawnFog, u);
            RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, nightFogDensity * 0.45f, u);
            RenderSettings.ambientSkyColor = Color.Lerp(nightSky, dawnSky, u);
            RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dawnEquator, u);
            RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dawnGround, u);
            if (blendSky)
            {
                RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbient, nightAmbient + 0.28f, u);
            }

            if (skybox != null)
            {
                // NightSky lerps night to dawn itself from _Blend and _Dawn*, so writing here would compound the blend.
                if (skybox.HasProperty("_Top") && !skybox.HasProperty("_DawnTop"))
                {
                    skybox.SetColor("_Top", Color.Lerp(skyTop, dawnTop, u));
                    skybox.SetColor("_Horizon", Color.Lerp(skyHorizon, dawnHorizon, u));
                    skybox.SetColor("_Ground", Color.Lerp(skyGround, dawnBottom, u));
                }

                if (blendSky)
                {
                    skybox.SetFloat("_Blend", u);
                    float dawnExposure = nightExposure * 1.7f;
                    if (skybox.HasProperty("_DawnExposure"))
                    {
                        float stored = skybox.GetFloat("_DawnExposure");
                        if (stored > 0.05f)
                        {
                            dawnExposure = stored;
                        }
                    }

                    skybox.SetFloat("_Exposure", Mathf.Lerp(nightExposure, dawnExposure, u));
                    skybox.SetColor("_Tint", Color.Lerp(nightTint, dawnTint, u));
                    if (skybox.HasProperty("_HazeColor"))
                    {
                        skybox.SetColor("_HazeColor", Color.Lerp(nightHaze, dawnHaze, u));
                    }

                    PushMoonDir();
                }
            }

            Shader.SetGlobalFloat("_LanternSkyBlend", blendSky ? u : 0f);
            Shader.SetGlobalColor("_WaterTint", Color.Lerp(nightWater, dawnWater, u));
            Shader.SetGlobalColor("_SilhouetteTint", Color.Lerp(nightSilhouette, dawnSilhouette, u));
            Shader.SetGlobalColor("_HazeTint", Color.Lerp(nightHazeTint, dawnHazeTint, u));

            if (stars != null)
            {
                stars.SetFade(1f - u);
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
            nightIntensity = sun.intensity;
            nightRotation = sun.transform.rotation;
        }

        nightFog = RenderSettings.fogColor;
        nightFogDensity = RenderSettings.fogDensity;
        nightSky = RenderSettings.ambientSkyColor;
        nightEquator = RenderSettings.ambientEquatorColor;
        nightGround = RenderSettings.ambientGroundColor;
        nightAmbient = RenderSettings.ambientIntensity;
        nightWater = Shader.GetGlobalColor("_WaterTint");
        nightSilhouette = Shader.GetGlobalColor("_SilhouetteTint");
        nightHazeTint = Shader.GetGlobalColor("_HazeTint");
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
