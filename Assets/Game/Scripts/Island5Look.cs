using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
// Island 5: the look chills as the keeper climbs. The fog (and the horizon haze that follows it) lerps from the profile's teal to
// a cold violet, and the sky and its clouds dim, one step per ring. It eases between steps and never goes back (the ring ratchets).
// Reads LookApplier's result after it has run, so the shared look code and the other islands are untouched.
public class Island5Look : MonoBehaviour
{
    // Per ring: Low, Mid, High, Summit.
    static readonly float[] VioletMix = { 0f, 0.3f, 0.65f, 1f };
    static readonly float[] SkyBrightness = { 1f, 0.82f, 0.62f, 0.45f };
    static readonly float[] CloudBrightness = { 1f, 0.7f, 0.5f, 0.35f };

    [SerializeField] Color coldViolet = new Color(0.17f, 0.11f, 0.3f, 1f);
    [SerializeField] float easeSeconds = 4f;

    Color baseFog;
    Material sky;
    Color baseTop;
    Color baseHorizon;
    Color baseCloud;
    Material[] haze;
    Color[] hazeBase;
    float level;
    bool ready;

    public float Level => level;

    void Start()
    {
        baseFog = RenderSettings.fogColor;
        sky = RenderSettings.skybox;
        if (sky != null)
        {
            baseTop = sky.HasProperty("_Top") ? sky.GetColor("_Top") : Color.black;
            baseHorizon = sky.HasProperty("_Horizon") ? sky.GetColor("_Horizon") : Color.black;
            baseCloud = sky.HasProperty("_CloudColor") ? sky.GetColor("_CloudColor") : Color.black;
        }

        GameObject horizon = GameObject.Find("Horizon");
        if (horizon != null)
        {
            List<Material> found = new List<Material>();
            Renderer[] renderers = horizon.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material shared = renderers[i].sharedMaterial;
                if (shared != null && shared.shader != null && shared.shader.name == "LanternKeeper/HorizonHaze")
                {
                    found.Add(renderers[i].material);
                }
            }

            haze = found.ToArray();
            hazeBase = new Color[haze.Length];
            for (int i = 0; i < haze.Length; i++)
            {
                hazeBase[i] = haze[i].GetColor("_BaseColor");
            }
        }

        ready = true;
        level = (float)RingZones.Reached;
    }

    void LateUpdate()
    {
        if (!ready)
        {
            return;
        }

        // The dawn after the win owns the fog and sky.
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.IsRoundOver)
        {
            return;
        }

        float target = (float)RingZones.Reached;
        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * 3f / Mathf.Max(0.1f, easeSeconds));
        Apply(level);
    }

    static float Sample(float[] steps, float at)
    {
        float clamped = Mathf.Clamp(at, 0f, steps.Length - 1f);
        int low = Mathf.FloorToInt(clamped);
        int high = Mathf.Min(low + 1, steps.Length - 1);
        return Mathf.Lerp(steps[low], steps[high], clamped - low);
    }

    void Apply(float at)
    {
        Color fog = Color.Lerp(baseFog, coldViolet, Sample(VioletMix, at));
        RenderSettings.fogColor = fog;
        if (sky != null)
        {
            float skyScale = Sample(SkyBrightness, at);
            float cloudScale = Sample(CloudBrightness, at);
            if (sky.HasProperty("_Top"))
            {
                sky.SetColor("_Top", Scale(baseTop, skyScale));
            }

            if (sky.HasProperty("_Horizon"))
            {
                sky.SetColor("_Horizon", Scale(baseHorizon, skyScale));
            }

            if (sky.HasProperty("_CloudColor"))
            {
                sky.SetColor("_CloudColor", Scale(baseCloud, cloudScale));
            }
        }

        for (int i = 0; haze != null && i < haze.Length; i++)
        {
            Color c = fog;
            c.a = hazeBase[i].a;
            haze[i].SetColor("_BaseColor", c);
        }
    }

    static Color Scale(Color c, float k)
    {
        return new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
}
