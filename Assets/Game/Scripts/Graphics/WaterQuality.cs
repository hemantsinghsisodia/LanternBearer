using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Applies lake water settings from the active graphics preset.
// The saved water material is cached once and is the Medium baseline.
// Quality changes are written to a runtime material instance and to shader globals.
// The saved material asset is never edited.
public class WaterQuality : MonoBehaviour
{
    const string WaterShaderName = "LanternKeeper/Water";
    const string LowKeywordName = "_LK_WATER_LOW";
    const string HighKeywordName = "_LK_WATER_HIGH";
    const string GlitterProperty = "_Glitter";
    const float ShaderFoamReach = 8f;
    const float ShaderRippleRange = 1f;

    [SerializeField] Renderer[] waterRenderers;

    static GraphicsProfile mediumProfile;
    static bool fallbackWarned;
    static bool probeMissingLogged;

    bool baselineCached;
    bool searched;
    bool probeRendered;
    Material[] instances;
    float[] baseGlitter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= Ensure;
        SceneManager.sceneLoaded += Ensure;
        Ensure(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Ensure(Scene scene, LoadSceneMode mode)
    {
        if (!IsIsland(scene) || FindAnyObjectByType<WaterQuality>() != null)
        {
            return;
        }

        Renderer[] found = FindWaterRenderers();
        if (found.Length == 0)
        {
            return;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        bool wasActive = host.activeSelf;
        host.SetActive(false);
        WaterQuality quality = host.AddComponent<WaterQuality>();
        quality.waterRenderers = found;
        host.SetActive(wasActive);
    }

    static bool IsIsland(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && scene.name.StartsWith("Island");
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRenderers();
        CacheBaseline();
        GraphicsQuality.QualityChanged += Apply;
        Apply(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= Apply;
    }

    void OnDestroy()
    {
        if (!Application.isPlaying || instances == null)
        {
            return;
        }

        for (int i = 0; i < instances.Length; i++)
        {
            Material instance = instances[i];
            if (instance == null)
            {
                continue;
            }

            Material shared = waterRenderers != null && i < waterRenderers.Length && waterRenderers[i] != null
                ? waterRenderers[i].sharedMaterial
                : null;
            if (instance != shared)
            {
                Destroy(instance);
            }
        }
    }

    void ResolveRenderers()
    {
        if (HasRenderers() || searched)
        {
            return;
        }

        searched = true;
        waterRenderers = FindWaterRenderers();
        if (!fallbackWarned)
        {
            fallbackWarned = true;
            Debug.LogWarning("WaterQuality has no water renderer; using the LanternKeeper/Water material.", this);
        }
    }

    bool HasRenderers()
    {
        if (waterRenderers == null || waterRenderers.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < waterRenderers.Length; i++)
        {
            if (waterRenderers[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    void CacheBaseline()
    {
        if (baselineCached || !HasRenderers())
        {
            return;
        }

        instances = new Material[waterRenderers.Length];
        baseGlitter = new float[waterRenderers.Length];
        for (int i = 0; i < waterRenderers.Length; i++)
        {
            Renderer renderer = waterRenderers[i];
            Material shared = renderer.sharedMaterial;
            baseGlitter[i] = shared != null ? shared.GetFloat(GlitterProperty) : 0f;
            instances[i] = renderer.material;
        }

        baselineCached = true;
    }

    void Apply(GraphicsProfile profile)
    {
        if (!Application.isPlaying || profile == null)
        {
            return;
        }

        ResolveRenderers();
        CacheBaseline();
        if (!baselineCached || instances == null)
        {
            return;
        }

        bool low = UseLow(profile);
        bool high = UseHigh(profile);
        GraphicsProfile medium = MediumProfile();
        float mediumGlint = medium != null ? medium.waterGlint : 0f;
        float foamReach = FoamReach(profile);
        float rippleRange = RippleRange(profile);
        float detail = profile.waterDetailLayer ? profile.waterDetailStrength : 0f;
        Shader.SetGlobalFloat("_LK_FoamReach", foamReach);
        Shader.SetGlobalFloat("_LK_RippleRange", rippleRange);
        Shader.SetGlobalFloat("_LK_DetailStrength", detail);

        for (int i = 0; i < instances.Length; i++)
        {
            Material material = instances[i];
            if (material == null)
            {
                continue;
            }

            float glitter = profile.level == GraphicsLevel.Medium ? baseGlitter[i] : Scale(baseGlitter[i], mediumGlint, profile.waterGlint);
            material.SetFloat(GlitterProperty, glitter);
            if (low)
            {
                material.EnableKeyword(LowKeywordName);
            }
            else
            {
                material.DisableKeyword(LowKeywordName);
            }

            if (high)
            {
                material.EnableKeyword(HighKeywordName);
            }
            else
            {
                material.DisableKeyword(HighKeywordName);
            }

            LogApplied(profile, material, i, glitter, foamReach, rippleRange, detail);
        }

        UpdateProbe(profile);
    }

    void LogApplied(GraphicsProfile profile, Material material, int index, float glitter, float foamReach, float rippleRange, float detail)
    {
        bool lowOn = material.IsKeywordEnabled(LowKeywordName);
        bool highOn = material.IsKeywordEnabled(HighKeywordName);
        float assetGlitter = 0f;
        if (waterRenderers != null && index < waterRenderers.Length && waterRenderers[index] != null)
        {
            Material shared = waterRenderers[index].sharedMaterial;
            if (shared != null)
            {
                assetGlitter = shared.GetFloat(GlitterProperty);
            }
        }

        Debug.Log(
            "WaterQuality " + profile.level
            + " renderer=" + index
            + " glitter=" + glitter.ToString("0.###")
            + " assetGlitter=" + assetGlitter.ToString("0.###")
            + " low=" + lowOn
            + " high=" + highOn
            + " foamReach=" + foamReach.ToString("0.###")
            + " rippleRange=" + rippleRange.ToString("0.###")
            + " detail=" + detail.ToString("0.###")
            + " probe=" + (profile.waterReflectionProbe ? "once" : "off"),
            this);
    }

    void UpdateProbe(GraphicsProfile profile)
    {
        if (!profile.waterReflectionProbe || probeRendered)
        {
            return;
        }

        ReflectionProbe[] probes = FindObjectsByType<ReflectionProbe>();
        ReflectionProbe realtime = null;
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null && probes[i].mode == ReflectionProbeMode.Realtime)
            {
                realtime = probes[i];
                break;
            }
        }

        probeRendered = true;
        if (realtime == null)
        {
            if (!probeMissingLogged)
            {
                probeMissingLogged = true;
                Debug.Log("WaterQuality: no reflection probe on this island; skipping the Ultra probe update.");
            }

            return;
        }

        realtime.RenderProbe();
        Debug.Log("WaterQuality rendered reflection probe once: " + realtime.name, this);
    }

    static bool UseLow(GraphicsProfile profile)
    {
        return !profile.waterRipples && !profile.waterShoreFoam && !profile.waterDepthTexture;
    }

    static bool UseHigh(GraphicsProfile profile)
    {
        if (UseLow(profile))
        {
            return false;
        }

        GraphicsProfile medium = MediumProfile();
        float mediumRange = medium != null ? medium.waterRippleRange : ShaderRippleRange;
        float mediumFoam = medium != null ? medium.waterFoamReach : ShaderFoamReach;
        return profile.waterDetailLayer
            || profile.waterRippleRange > mediumRange + 0.001f
            || profile.waterFoamReach > mediumFoam + 0.001f;
    }

    static float FoamReach(GraphicsProfile profile)
    {
        if (profile.level == GraphicsLevel.Medium || UseLow(profile))
        {
            return ShaderFoamReach;
        }

        GraphicsProfile medium = MediumProfile();
        float mediumFoam = medium != null ? medium.waterFoamReach : ShaderFoamReach;
        return Scale(ShaderFoamReach, mediumFoam, profile.waterFoamReach);
    }

    static float RippleRange(GraphicsProfile profile)
    {
        if (profile.level == GraphicsLevel.Medium || UseLow(profile))
        {
            return ShaderRippleRange;
        }

        GraphicsProfile medium = MediumProfile();
        float mediumRange = medium != null ? medium.waterRippleRange : ShaderRippleRange;
        return Scale(ShaderRippleRange, mediumRange, profile.waterRippleRange);
    }

    static GraphicsProfile MediumProfile()
    {
        if (mediumProfile == null)
        {
            GraphicsProfileSet set = Resources.Load<GraphicsProfileSet>("GraphicsProfileSet");
            if (set != null)
            {
                mediumProfile = set.Get(GraphicsLevel.Medium);
            }
        }

        return mediumProfile;
    }

    static float Scale(float baseline, float mediumValue, float presetValue)
    {
        if (mediumValue <= 0.0001f)
        {
            return baseline;
        }

        if (Mathf.Approximately(baseline, mediumValue))
        {
            return presetValue;
        }

        return baseline * (presetValue / mediumValue);
    }

    static Renderer[] FindWaterRenderers()
    {
        Renderer[] all = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsWater(all[i]))
            {
                count++;
            }
        }

        Renderer[] found = new Renderer[count];
        int cursor = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsWater(all[i]))
            {
                found[cursor] = all[i];
                cursor++;
            }
        }

        return found;
    }

    static bool IsWater(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Material material = renderer.sharedMaterial;
        return material != null && material.shader != null && material.shader.name == WaterShaderName;
    }
}
}
