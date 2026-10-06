using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Applies grass and terrain settings from the active graphics preset.
// The scene's saved terrain values are cached once and are the Medium baseline.
// Low, High, and Ultra are that baseline scaled by the preset over the Medium profile.
// When the saved values already match the Medium profile, those levels use the profile numbers directly.
// QualitySettings.lodBias stays with the Unity quality level. Tree distances are scaled from the
// cached baseline only when the terrain actually has trees.
public class TerrainQuality : MonoBehaviour
{
    const string GrassNoBendKeyword = "_LK_GRASS_NO_BEND";
    const string FoliageLowKeyword = "_LK_FOLIAGE_LOW";
    const string TerrainLowKeyword = "_LK_TERRAIN_LOW";

    [SerializeField] Terrain terrain;

    static GraphicsProfile mediumProfile;
    static bool fallbackWarned;

    bool baselineCached;
    bool hasTrees;
    float baseDetailDistance;
    float baseDetailDensity;
    float basePixelError;
    float baseBasemapDistance;
    float baseTreeDistance;
    float baseBillboardDistance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= Ensure;
        SceneManager.sceneLoaded += Ensure;
        Ensure(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    public static void EnsureScene(Scene scene)
    {
        Ensure(scene, LoadSceneMode.Single);
    }

    static void Ensure(Scene scene, LoadSceneMode mode)
    {
        if (!IsIsland(scene) || FindAnyObjectByType<TerrainQuality>() != null)
        {
            return;
        }

        Terrain active = Terrain.activeTerrain;
        if (active == null)
        {
            active = FindAnyObjectByType<Terrain>();
        }

        if (active == null)
        {
            if (FindAnyObjectByType<TerrainQualityLateHook>() == null)
            {
                new GameObject("TerrainQualityLateHook").AddComponent<TerrainQualityLateHook>();
            }

            return;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        bool wasActive = host.activeSelf;
        host.SetActive(false);
        TerrainQuality quality = host.AddComponent<TerrainQuality>();
        quality.terrain = active;
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

        ResolveTerrain();
        CacheBaseline();
        GraphicsQuality.QualityChanged += Apply;
        Apply(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= Apply;
    }

    void ResolveTerrain()
    {
        if (terrain != null)
        {
            return;
        }

        terrain = Terrain.activeTerrain;
        if (!fallbackWarned)
        {
            fallbackWarned = true;
            Debug.LogWarning("TerrainQuality has no terrain reference; using the active terrain.", this);
        }
    }

    void CacheBaseline()
    {
        if (baselineCached || terrain == null)
        {
            return;
        }

        baseDetailDistance = terrain.detailObjectDistance;
        baseDetailDensity = terrain.detailObjectDensity;
        basePixelError = terrain.heightmapPixelError;
        baseBasemapDistance = terrain.basemapDistance;
        TerrainData data = terrain.terrainData;
        hasTrees = data != null && data.treePrototypes != null && data.treePrototypes.Length > 0 && data.treeInstanceCount > 0;
        if (hasTrees)
        {
            baseTreeDistance = terrain.treeDistance;
            baseBillboardDistance = terrain.treeBillboardDistance;
        }

        baselineCached = true;
    }

    void Apply(GraphicsProfile profile)
    {
        if (!Application.isPlaying || profile == null)
        {
            return;
        }

        ResolveTerrain();
        CacheBaseline();
        if (profile.grassBending)
        {
            Shader.DisableKeyword(GrassNoBendKeyword);
        }
        else
        {
            Shader.EnableKeyword(GrassNoBendKeyword);
        }

        // Low stops the nature-kit foliage sway (LanternKeeper/Foliage), the same switch that stills the grass.
        if (profile.level == GraphicsLevel.Low)
        {
            Shader.EnableKeyword(FoliageLowKeyword);
        }
        else
        {
            Shader.DisableKeyword(FoliageLowKeyword);
        }

        // Low uses the cheaper dominant-axis projection for the terrain rock-wall shading.
        if (profile.level == GraphicsLevel.Low)
        {
            Shader.EnableKeyword(TerrainLowKeyword);
        }
        else
        {
            Shader.DisableKeyword(TerrainLowKeyword);
        }

        if (terrain == null || !baselineCached)
        {
            return;
        }

        if (profile.level == GraphicsLevel.Medium)
        {
            RestoreBaseline();
            return;
        }

        GraphicsProfile medium = MediumProfile();
        terrain.detailObjectDistance = Scale(baseDetailDistance, medium != null ? medium.grassDrawDistance : 0f, profile.grassDrawDistance);
        terrain.detailObjectDensity = Scale(baseDetailDensity, medium != null ? medium.grassDensity : 0f, profile.grassDensity);
        terrain.heightmapPixelError = Scale(basePixelError, medium != null ? medium.terrainPixelError : 0f, profile.terrainPixelError);
        terrain.basemapDistance = Scale(baseBasemapDistance, medium != null ? medium.terrainBasemapDistance : 0f, profile.terrainBasemapDistance);
        if (hasTrees)
        {
            float bias = medium != null ? medium.treeLodBias : 0f;
            terrain.treeDistance = Scale(baseTreeDistance, bias, profile.treeLodBias);
            terrain.treeBillboardDistance = Scale(baseBillboardDistance, bias, profile.treeLodBias);
        }
    }

    void RestoreBaseline()
    {
        terrain.detailObjectDistance = baseDetailDistance;
        terrain.detailObjectDensity = baseDetailDensity;
        terrain.heightmapPixelError = basePixelError;
        terrain.basemapDistance = baseBasemapDistance;
        if (hasTrees)
        {
            terrain.treeDistance = baseTreeDistance;
            terrain.treeBillboardDistance = baseBillboardDistance;
        }
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

    // Same numbers as the Medium profile means use the preset value. Otherwise scale this island's saved Medium.
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
}

// Created only when an island loads before its terrain is active. Removes itself after one start.
sealed class TerrainQualityLateHook : MonoBehaviour
{
    void Start()
    {
        Scene scene = gameObject.scene;
        Destroy(gameObject);
        TerrainQuality.EnsureScene(scene);
    }
}
}
