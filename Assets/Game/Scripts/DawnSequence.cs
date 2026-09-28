using System;
using System.Collections;
using UnityEngine;

namespace LanternKeeper
{
public class DawnSequence : MonoBehaviour
{
    [SerializeField] float duration = 6f;
    [SerializeField] Light sun;
    [SerializeField] StarTwinkle stars;

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
    Material skybox;
    bool captured;

    public bool IsPlaying { get; private set; }

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
    }

    public void Play(Action onComplete)
    {
        if (IsPlaying)
        {
            return;
        }

        StartCoroutine(Run(onComplete));
    }

    IEnumerator Run(Action onComplete)
    {
        IsPlaying = true;
        CaptureNight();
        float elapsed = 0f;
        Color dawnLight = new Color(1f, 0.72f, 0.48f);
        Quaternion dawnRotation = Quaternion.Euler(10f, 28f, 0f);
        Color dawnFog = new Color(1f, 0.72f, 0.58f);
        Color dawnSky = new Color(0.95f, 0.62f, 0.5f);
        Color dawnEquator = new Color(0.92f, 0.5f, 0.36f);
        Color dawnGround = new Color(0.38f, 0.18f, 0.12f);
        Color dawnTop = new Color(0.45f, 0.38f, 0.62f);
        Color dawnHorizon = new Color(1f, 0.62f, 0.42f);
        Color dawnBottom = new Color(0.48f, 0.24f, 0.18f);

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

            if (skybox != null)
            {
                skybox.SetColor("_Top", Color.Lerp(skyTop, dawnTop, u));
                skybox.SetColor("_Horizon", Color.Lerp(skyHorizon, dawnHorizon, u));
                skybox.SetColor("_Ground", Color.Lerp(skyGround, dawnBottom, u));
            }

            if (stars != null)
            {
                stars.SetFade(1f - u);
            }

            yield return null;
        }

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
        if (skybox != null && skybox.HasProperty("_Top"))
        {
            skyTop = skybox.GetColor("_Top");
            skyHorizon = skybox.GetColor("_Horizon");
            skyGround = skybox.GetColor("_Ground");
        }
    }
}
}
