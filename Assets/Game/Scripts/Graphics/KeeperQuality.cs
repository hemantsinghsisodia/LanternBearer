using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Applies keeper settings from the active graphics preset.
// Saved renderer, light, and animator values are cached once and are the Medium baseline.
// The rim keyword is toggled on runtime material instances. Shared material assets are never edited.
public class KeeperQuality : MonoBehaviour
{
    const string KeeperShaderName = "LanternKeeper/KeeperLit";
    const string NoRimKeyword = "_LK_KEEPER_NO_RIM";
    const string ChestFillName = "ChestFill";
    const string LanternLightName = "LanternLight";

    [SerializeField] Renderer[] keeperRenderers;
    [SerializeField] Light chestFill;
    [SerializeField] Light lanternLight;
    [SerializeField] Animator animator;

    static bool fallbackWarned;

    bool baselineCached;
    bool searched;
    ShadowCastingMode[] baseCast;
    bool[] baseReceive;
    Material[][] instances;
    Material[][] sharedMaterials;
    bool baseFillEnabled;
    float baseFillIntensity;
    float baseFillRange;
    LightShadows baseFillShadows;
    LightShadows baseLanternShadows;
    int baseLanternShadowTier;
    bool baseLanternDataPresent;
    AnimatorCullingMode baseCulling;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= Ensure;
        SceneManager.sceneLoaded += Ensure;
        Ensure(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Ensure(Scene scene, LoadSceneMode mode)
    {
        if (!IsIsland(scene) || FindAnyObjectByType<KeeperQuality>() != null)
        {
            return;
        }

        PlayerController keeper = FindGameplayKeeper();
        if (keeper == null)
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
        KeeperQuality quality = host.AddComponent<KeeperQuality>();
        quality.keeperRenderers = FindKeeperRenderers(keeper);
        quality.chestFill = FindChildLight(keeper, ChestFillName);
        quality.lanternLight = FindChildLight(keeper, LanternLightName);
        quality.animator = FindKeeperAnimator(keeper);
        host.SetActive(wasActive);
    }

    public static PlayerController FindGameplayKeeper()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
        PlayerController enabledKeeper = null;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player == null)
            {
                continue;
            }

            if (player.enabled && player.CompareTag("Player"))
            {
                return player;
            }

            if (player.enabled && enabledKeeper == null)
            {
                enabledKeeper = player;
            }
        }

        return enabledKeeper != null ? enabledKeeper : (players.Length > 0 ? players[0] : null);
    }

    public static Renderer[] FindKeeperRenderers(PlayerController keeper)
    {
        if (keeper == null)
        {
            return new Renderer[0];
        }

        Renderer[] all = keeper.GetComponentsInChildren<Renderer>(true);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (UsesKeeperLit(all[i]))
            {
                count++;
            }
        }

        Renderer[] found = new Renderer[count];
        int cursor = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (UsesKeeperLit(all[i]))
            {
                found[cursor] = all[i];
                cursor++;
            }
        }

        return found;
    }

    public static Light FindChildLight(PlayerController keeper, string lightName)
    {
        if (keeper == null)
        {
            return null;
        }

        Light[] lights = keeper.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].name == lightName)
            {
                return lights[i];
            }
        }

        return null;
    }

    public static Animator FindKeeperAnimator(PlayerController keeper)
    {
        return keeper != null ? keeper.GetComponentInChildren<Animator>(true) : null;
    }

    static bool IsIsland(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && scene.name.StartsWith("Island");
    }

    static bool UsesKeeperLit(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.shader != null && material.shader.name == KeeperShaderName)
            {
                return true;
            }
        }

        return false;
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRefs();
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
            Material[] live = instances[i];
            if (live == null)
            {
                continue;
            }

            Material[] shared = sharedMaterials != null && i < sharedMaterials.Length ? sharedMaterials[i] : null;
            for (int m = 0; m < live.Length; m++)
            {
                Material instance = live[m];
                if (instance == null)
                {
                    continue;
                }

                bool isShared = shared != null && m < shared.Length && instance == shared[m];
                if (!isShared)
                {
                    Destroy(instance);
                }
            }
        }
    }

    void ResolveRefs()
    {
        if (HasRefs() || searched)
        {
            return;
        }

        searched = true;
        PlayerController keeper = FindGameplayKeeper();
        keeperRenderers = FindKeeperRenderers(keeper);
        chestFill = FindChildLight(keeper, ChestFillName);
        lanternLight = FindChildLight(keeper, LanternLightName);
        animator = FindKeeperAnimator(keeper);
        if (!fallbackWarned)
        {
            fallbackWarned = true;
            Debug.LogWarning("KeeperQuality is missing a keeper reference; using the PlayerController in the scene.", this);
        }
    }

    bool HasRefs()
    {
        if (keeperRenderers == null || keeperRenderers.Length == 0 || chestFill == null || lanternLight == null || animator == null)
        {
            return false;
        }

        for (int i = 0; i < keeperRenderers.Length; i++)
        {
            if (keeperRenderers[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    void CacheBaseline()
    {
        if (baselineCached || !HasRefs())
        {
            return;
        }

        int count = keeperRenderers.Length;
        baseCast = new ShadowCastingMode[count];
        baseReceive = new bool[count];
        instances = new Material[count][];
        sharedMaterials = new Material[count][];
        for (int i = 0; i < count; i++)
        {
            Renderer renderer = keeperRenderers[i];
            baseCast[i] = renderer.shadowCastingMode;
            baseReceive[i] = renderer.receiveShadows;
            sharedMaterials[i] = renderer.sharedMaterials;
            instances[i] = renderer.materials;
        }

        baseFillEnabled = chestFill.enabled;
        baseFillIntensity = chestFill.intensity;
        baseFillRange = chestFill.range;
        baseFillShadows = chestFill.shadows;
        baseLanternShadows = lanternLight.shadows;
        UniversalAdditionalLightData lightData = lanternLight.GetComponent<UniversalAdditionalLightData>();
        baseLanternDataPresent = lightData != null;
        baseLanternShadowTier = lightData != null
            ? lightData.additionalLightsShadowResolutionTier
            : UniversalAdditionalLightData.AdditionalLightsShadowDefaultResolutionTier;
        baseCulling = animator.cullingMode;
        baselineCached = true;
    }

    void Apply(GraphicsProfile profile)
    {
        if (!Application.isPlaying || profile == null)
        {
            return;
        }

        ResolveRefs();
        CacheBaseline();
        if (!baselineCached)
        {
            return;
        }

        if (profile.level == GraphicsLevel.Medium)
        {
            RestoreBaseline();
            SetRimKeyword(false);
            LogApplied(profile, false, false);
            return;
        }

        bool receiving = ApplyRenderers(profile.keeperSelfShadow);
        ApplyChestFill(profile);
        animator.cullingMode = profile.keeperAlwaysAnimate ? AnimatorCullingMode.AlwaysAnimate : baseCulling;
        bool lanternSoft = ApplyLanternShadow(profile, receiving);
        SetRimKeyword(!profile.keeperRim);
        LogApplied(profile, receiving, lanternSoft);
    }

    void RestoreBaseline()
    {
        for (int i = 0; i < keeperRenderers.Length; i++)
        {
            Renderer renderer = keeperRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = baseCast[i];
            renderer.receiveShadows = baseReceive[i];
        }

        chestFill.enabled = baseFillEnabled;
        chestFill.intensity = baseFillIntensity;
        chestFill.range = baseFillRange;
        chestFill.shadows = baseFillShadows;
        lanternLight.shadows = baseLanternShadows;
        RestoreLanternTier();
        animator.cullingMode = baseCulling;
    }

    bool ApplyRenderers(bool selfShadow)
    {
        bool receiving = false;
        for (int i = 0; i < keeperRenderers.Length; i++)
        {
            Renderer renderer = keeperRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (selfShadow)
            {
                renderer.shadowCastingMode = baseCast[i];
                renderer.receiveShadows = baseReceive[i];
            }
            else
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (renderer.receiveShadows)
            {
                receiving = true;
            }
        }

        return receiving;
    }

    void ApplyChestFill(GraphicsProfile profile)
    {
        chestFill.intensity = baseFillIntensity;
        chestFill.range = baseFillRange;
        if (!profile.chestFillLight)
        {
            chestFill.enabled = false;
            chestFill.shadows = baseFillShadows;
            return;
        }

        chestFill.enabled = baseFillEnabled;
        // High and Ultra keep the fill on and stop it casting a shadow.
        chestFill.shadows = profile.keeperAlwaysAnimate ? LightShadows.None : baseFillShadows;
    }

    bool ApplyLanternShadow(GraphicsProfile profile, bool keeperReceives)
    {
        bool supported = AdditionalShadowsSupported();
        if (profile.lanternSoftShadowOnKeeper && keeperReceives && supported)
        {
            lanternLight.shadows = LightShadows.Soft;
            UniversalAdditionalLightData data = lanternLight.GetUniversalAdditionalLightData();
            data.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            return true;
        }

        lanternLight.shadows = baseLanternShadows;
        RestoreLanternTier();
        return false;
    }

    void RestoreLanternTier()
    {
        UniversalAdditionalLightData data = lanternLight.GetComponent<UniversalAdditionalLightData>();
        if (data == null)
        {
            return;
        }

        if (!baseLanternDataPresent && data.additionalLightsShadowResolutionTier == UniversalAdditionalLightData.AdditionalLightsShadowDefaultResolutionTier)
        {
            return;
        }

        data.additionalLightsShadowResolutionTier = baseLanternShadowTier;
    }

    static bool AdditionalShadowsSupported()
    {
        UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        return asset != null && asset.supportsAdditionalLightShadows;
    }

    void SetRimKeyword(bool skipRim)
    {
        if (instances == null)
        {
            return;
        }

        for (int i = 0; i < instances.Length; i++)
        {
            Material[] materials = instances[i];
            if (materials == null)
            {
                continue;
            }

            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null || material.shader == null || material.shader.name != KeeperShaderName)
                {
                    continue;
                }

                if (skipRim)
                {
                    material.EnableKeyword(NoRimKeyword);
                }
                else
                {
                    material.DisableKeyword(NoRimKeyword);
                }
            }
        }
    }

    void LogApplied(GraphicsProfile profile, bool receiving, bool lanternSoft)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append("KeeperQuality ").Append(profile.level);
        text.Append(" chestFill=").Append(chestFill.enabled ? "on" : "off");
        text.Append(" intensity=").Append(chestFill.intensity.ToString("0.###"));
        text.Append(" range=").Append(chestFill.range.ToString("0.###"));
        text.Append(" fillShadows=").Append(chestFill.shadows);
        text.Append(" lanternShadows=").Append(lanternLight.shadows);
        UniversalAdditionalLightData lightData = lanternLight.GetComponent<UniversalAdditionalLightData>();
        text.Append(" lanternTier=").Append(lightData != null ? lightData.additionalLightsShadowResolutionTier.ToString() : "default");
        text.Append(" lanternSoft=").Append(lanternSoft);
        text.Append(" keeperReceives=").Append(receiving);
        text.Append(" culling=").Append(animator.cullingMode);
        bool assetRimKeyword = false;
        for (int i = 0; i < keeperRenderers.Length; i++)
        {
            Renderer renderer = keeperRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            text.Append(" | ").Append(renderer.name);
            text.Append(" cast=").Append(renderer.shadowCastingMode);
            text.Append(" receive=").Append(renderer.receiveShadows);
            Material[] live = instances[i];
            Material[] shared = sharedMaterials[i];
            if (live == null)
            {
                continue;
            }

            for (int m = 0; m < live.Length; m++)
            {
                Material material = live[m];
                if (material == null || material.shader == null || material.shader.name != KeeperShaderName)
                {
                    continue;
                }

                bool noRim = material.IsKeywordEnabled(NoRimKeyword);
                text.Append(" ").Append(material.name).Append(" noRim=").Append(noRim);
                if (shared != null && m < shared.Length && shared[m] != null && shared[m].IsKeywordEnabled(NoRimKeyword))
                {
                    assetRimKeyword = true;
                }
            }
        }

        text.Append(" assetNoRim=").Append(assetRimKeyword);
        Debug.Log(text.ToString(), this);
    }
}
}
