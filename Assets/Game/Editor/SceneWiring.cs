using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
public static class SceneWiring
{
    [MenuItem("Lantern Keeper/Wire Scene References")]
    public static void WireMenu()
    {
        int assigned = ApplyActiveScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Wire Scene References assigned " + assigned + " fields in " + EditorSceneManager.GetActiveScene().name + ".");
    }

    [MenuItem("Lantern Keeper/Validate Scene Wiring")]
    public static void ValidateMenu()
    {
        int nulls = Report(false);
        if (nulls == 0)
        {
            Debug.Log("Validate Scene Wiring: 0 null references in " + EditorSceneManager.GetActiveScene().name + ".");
        }
        else
        {
            Debug.LogWarning("Validate Scene Wiring: " + nulls + " null references in " + EditorSceneManager.GetActiveScene().name + ".");
        }
    }

    public static int ApplyActiveScene()
    {
        int assigned = 0;
        PlayerController body = Object.FindAnyObjectByType<PlayerController>();
        Lantern lantern = body != null ? body.GetComponentInChildren<Lantern>(true) : Object.FindAnyObjectByType<Lantern>();
        HUD hud = Object.FindAnyObjectByType<HUD>();
        CameraFollow follow = Object.FindAnyObjectByType<CameraFollow>();
        DawnSequence dawn = Object.FindAnyObjectByType<DawnSequence>();
        UnityEngine.Rendering.Volume volume = FindVolume("EffectsVolume");
        Transform player = body != null ? body.transform : null;

        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager != null)
        {
            assigned += Set(manager, "lantern", lantern);
            assigned += Set(manager, "hud", hud);
            assigned += Set(manager, "dawn", dawn);
        }

        if (dawn != null)
        {
            UnityEngine.Rendering.Volume lookVolume = FindVolume("LookVolume");
            UnityEngine.Rendering.Volume dawnVolume = FindVolume("LookVolume_dawn");
            if (lookVolume != null)
            {
                assigned += Set(dawn, "lookVolume", lookVolume);
            }

            if (dawnVolume != null)
            {
                assigned += Set(dawn, "dawnVolume", dawnVolume);
            }
        }

        if (hud != null)
        {
            assigned += Set(hud, "lantern", lantern);
        }

        AudioManager audio = Object.FindAnyObjectByType<AudioManager>();
        if (audio != null && lantern != null)
        {
            assigned += Set(audio, "lantern", lantern);
        }

        LowFuelFX fx = Object.FindAnyObjectByType<LowFuelFX>();
        if (fx != null)
        {
            assigned += Set(fx, "lantern", lantern);
            if (volume != null)
            {
                assigned += Set(fx, "volume", volume);
            }
        }

        MothSpawner moths = Object.FindAnyObjectByType<MothSpawner>();
        if (moths != null)
        {
            assigned += Set(moths, "lantern", lantern);
        }

        if (body != null)
        {
            assigned += Set(body, "cameraFollow", follow);
        }

        if (follow != null && player != null)
        {
            assigned += Set(follow, "target", player);
        }

        BeaconCompass compass = Object.FindAnyObjectByType<BeaconCompass>();
        if (compass != null && player != null)
        {
            assigned += Set(compass, "player", player);
        }

        Beacon[] beacons = Object.FindObjectsByType<Beacon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < beacons.Length; i++)
        {
            assigned += Set(beacons[i], "player", player);
            assigned += Set(beacons[i], "lantern", lantern);
        }

        Lantern[] sceneLanterns = Object.FindObjectsByType<Lantern>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneLanterns.Length; i++)
        {
            assigned += Set(sceneLanterns[i], "lanternLight", LanternLight(sceneLanterns[i]));
        }

        WaterHazard[] water = Object.FindObjectsByType<WaterHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < water.Length; i++)
        {
            assigned += Set(water[i], "player", body);
            assigned += Set(water[i], "lantern", lantern);
            assigned += Set(water[i], "hud", hud);
            assigned += Set(water[i], "cameraFollow", follow);
        }

        Tide[] tides = Object.FindObjectsByType<Tide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tides.Length; i++)
        {
            GameObject waterObject = GameObject.Find("Water");
            assigned += Set(tides[i], "waterRoot", waterObject != null ? waterObject.transform : null);
            assigned += Set(tides[i], "hazard", water.Length > 0 ? water[0] : null);
        }

        Wind[] winds = Object.FindObjectsByType<Wind>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < winds.Length; i++)
        {
            assigned += Set(winds[i], "player", body);
            assigned += Set(winds[i], "hazard", water.Length > 0 ? water[0] : null);
        }

        ShadeSpawner[] shadeSpawners = Object.FindObjectsByType<ShadeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shadeSpawners.Length; i++)
        {
            assigned += Set(shadeSpawners[i], "lantern", lantern);
            assigned += Set(shadeSpawners[i], "player", body);
            assigned += Set(shadeSpawners[i], "hazard", water.Length > 0 ? water[0] : null);
            assigned += Set(shadeSpawners[i], "hud", hud);
        }

        Lightning[] lightnings = Object.FindObjectsByType<Lightning>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lightnings.Length; i++)
        {
            assigned += Set(lightnings[i], "flashLight", lightnings[i].GetComponentInChildren<Light>(true));
            assigned += Set(lightnings[i], "volume", volume);
        }

        Rain[] rains = Object.FindObjectsByType<Rain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rains.Length; i++)
        {
            assigned += Set(rains[i], "system", rains[i].GetComponent<ParticleSystem>());
            assigned += Set(rains[i], "player", body);
        }

        LightRevealed[] revealed = Object.FindObjectsByType<LightRevealed>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < revealed.Length; i++)
        {
            assigned += Set(revealed[i], "lantern", lantern);
        }

        assigned += WireTerrainQuality();
        assigned += WireWaterQuality();
        assigned += WireKeeperQuality();
        assigned += WireLightQuality();
        assigned += WireHorizonQuality();
        assigned += WireGraphicsMenus();
        assigned += WireMoonRim();
        return assigned;
    }

    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string MoonRimShaderPath = "Assets/Game/Shaders/MoonRim.shader";

    // Islands (the scenes with a LookApplier) get a MoonRimSettings beside it; the menu has no look and so no rim.
    static int WireMoonRim()
    {
        int added = 0;
        LookApplier[] appliers = Object.FindObjectsByType<LookApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < appliers.Length; i++)
        {
            if (appliers[i].GetComponent<MoonRimSettings>() == null)
            {
                Undo.AddComponent<MoonRimSettings>(appliers[i].gameObject);
                added++;
            }
        }

        return added;
    }

    // Adds MoonRimFeature to PC_Renderer (once) as a sub-asset and points it at the shader so builds include it.
    [MenuItem("Lantern Keeper/Look/Ensure Moon Rim Feature")]
    public static void EnsureMoonRimFeature()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
        Object renderer = null;
        MoonRimFeature feature = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is MoonRimFeature)
            {
                feature = (MoonRimFeature)assets[i];
            }
            else if (assets[i] is UnityEngine.Rendering.Universal.ScriptableRendererData)
            {
                renderer = assets[i];
            }
        }

        if (renderer == null)
        {
            Debug.LogWarning("Ensure Moon Rim Feature: " + RendererPath + " not found.");
            return;
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<MoonRimFeature>();
            feature.name = "MoonRimFeature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            SerializedObject rendererObject = new SerializedObject(renderer);
            SerializedProperty list = rendererObject.FindProperty("m_RendererFeatures");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject featureObject = new SerializedObject(feature);
        featureObject.FindProperty("shader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(MoonRimShaderPath);
        featureObject.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Rendering.Universal.ScriptableRendererData data = (UnityEngine.Rendering.Universal.ScriptableRendererData)renderer;
        data.SetDirty();
        AssetDatabase.SaveAssetIfDirty(renderer);
        AssetDatabase.SaveAssets();
    }

    // PC_Renderer must contain a MoonRimFeature with its shader assigned; islands need MoonRimSettings, the menu must not have one.
    static int ReportMoonRim(bool quiet)
    {
        int problems = 0;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
        MoonRimFeature feature = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is MoonRimFeature)
            {
                feature = (MoonRimFeature)assets[i];
            }
        }

        string problem = null;
        if (feature == null)
        {
            problem = "PC_Renderer has no MoonRimFeature";
        }
        else if (GetObject(feature, "shader") == null)
        {
            problem = "MoonRimFeature has no shader assigned";
        }

        if (problem != null)
        {
            problems++;
            if (!quiet)
            {
                Debug.LogWarning("Moon rim problem: " + problem);
            }
        }

        bool hasLook = Object.FindAnyObjectByType<LookApplier>(FindObjectsInactive.Include) != null;
        bool hasSettings = Object.FindAnyObjectByType<MoonRimSettings>(FindObjectsInactive.Include) != null;
        if (hasLook != hasSettings)
        {
            problems++;
            if (!quiet)
            {
                Debug.LogWarning("Moon rim problem: " + EditorSceneManager.GetActiveScene().name + (hasLook ? " has a LookApplier but no MoonRimSettings" : " has MoonRimSettings without a LookApplier"));
            }
        }

        return problems;
    }

    static Light LanternLight(Lantern lantern)
    {
        if (lantern == null)
        {
            return null;
        }

        Light[] lights = lantern.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].name == "LanternLight")
            {
                return lights[i];
            }
        }

        return null;
    }

    static int WireHorizonQuality()
    {
        if (GameObject.Find("Horizon") == null)
        {
            return 0;
        }

        GameObject[] ranges = HorizonQuality.FindMountainRanges();
        GameObject[] ridges = HorizonQuality.FindExtraRidges();
        GameObject[] haze = HorizonQuality.FindHazeLayers();
        Camera[] cameras = HorizonQuality.FindGameplayCameras();
        if (ranges.Length == 0 && haze.Length == 0)
        {
            return 0;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        HorizonQuality quality = host.GetComponent<HorizonQuality>();
        if (quality == null)
        {
            quality = host.AddComponent<HorizonQuality>();
        }

        int assigned = 0;
        if (ranges.Length > 0)
        {
            assigned += SetArray(quality, "mountainRanges", ranges);
        }

        if (ridges.Length > 0)
        {
            assigned += SetArray(quality, "extraRidges", ridges);
        }

        if (haze.Length > 0)
        {
            assigned += SetArray(quality, "hazeLayers", haze);
        }

        if (cameras.Length > 0)
        {
            assigned += SetArray(quality, "cameras", cameras);
        }

        return assigned;
    }

    static int WireTerrainQuality()
    {
        if (Object.FindAnyObjectByType<PlayerController>() == null)
        {
            return 0;
        }

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            return 0;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        TerrainQuality quality = host.GetComponent<TerrainQuality>();
        if (quality == null)
        {
            quality = host.AddComponent<TerrainQuality>();
        }

        return Set(quality, "terrain", terrain);
    }

    static int WireWaterQuality()
    {
        if (Object.FindAnyObjectByType<PlayerController>() == null)
        {
            return 0;
        }

        Renderer[] water = FindWaterRenderers();
        if (water.Length == 0)
        {
            return 0;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        WaterQuality quality = host.GetComponent<WaterQuality>();
        if (quality == null)
        {
            quality = host.AddComponent<WaterQuality>();
        }

        return SetArray(quality, "waterRenderers", water);
    }

    static int WireKeeperQuality()
    {
        if (!IsIslandScene())
        {
            return 0;
        }

        PlayerController keeper = KeeperQuality.FindGameplayKeeper();
        if (keeper == null)
        {
            return 0;
        }

        Renderer[] renderers = KeeperQuality.FindKeeperRenderers(keeper);
        Light chestFill = KeeperQuality.FindChildLight(keeper, "ChestFill");
        Light lanternLight = KeeperQuality.FindChildLight(keeper, "LanternLight");
        Animator animator = KeeperQuality.FindKeeperAnimator(keeper);
        if (renderers.Length == 0 || chestFill == null || lanternLight == null || animator == null)
        {
            return 0;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        KeeperQuality quality = host.GetComponent<KeeperQuality>();
        if (quality == null)
        {
            quality = host.AddComponent<KeeperQuality>();
        }

        int assigned = SetArray(quality, "keeperRenderers", renderers);
        assigned += Set(quality, "chestFill", chestFill);
        assigned += Set(quality, "lanternLight", lanternLight);
        assigned += Set(quality, "animator", animator);
        return assigned;
    }

    static int WireLightQuality()
    {
        if (!IsIslandScene() || Object.FindAnyObjectByType<PlayerController>() == null)
        {
            return 0;
        }

        Light[] beaconLights = LightQuality.FindBeaconLights();
        Light[] fireflyLights = LightQuality.FindFireflyLights();
        if (beaconLights.Length == 0 && fireflyLights.Length == 0)
        {
            return 0;
        }

        GameObject host = GameObject.Find("GraphicsAppliers");
        if (host == null)
        {
            host = new GameObject("GraphicsAppliers");
        }

        LightQuality quality = host.GetComponent<LightQuality>();
        if (quality == null)
        {
            quality = host.AddComponent<LightQuality>();
        }

        int assigned = 0;
        if (beaconLights.Length > 0)
        {
            assigned += SetArray(quality, "beaconLights", beaconLights);
        }

        if (fireflyLights.Length > 0)
        {
            assigned += SetArray(quality, "fireflyLights", fireflyLights);
        }

        return assigned;
    }

    static bool IsIslandScene()
    {
        string sceneName = EditorSceneManager.GetActiveScene().name;
        return sceneName.StartsWith("Island");
    }

    static Renderer[] FindWaterRenderers()
    {
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsWaterRenderer(all[i]))
            {
                count++;
            }
        }

        Renderer[] found = new Renderer[count];
        int cursor = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsWaterRenderer(all[i]))
            {
                found[cursor] = all[i];
                cursor++;
            }
        }

        return found;
    }

    static bool IsWaterRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Material material = renderer.sharedMaterial;
        return material != null && material.shader != null && material.shader.name == "LanternKeeper/Water";
    }

    public static int ReportActiveScene()
    {
        return Report(false);
    }

    static int Report(bool quiet)
    {
        int nulls = 0;
        bool needsLantern = Object.FindAnyObjectByType<Lantern>() != null;
        GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            nulls += Require(managers[i], "lantern", quiet);
            nulls += Require(managers[i], "hud", quiet);
            nulls += Require(managers[i], "dawn", quiet);
        }

        HUD[] huds = Object.FindObjectsByType<HUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            nulls += Require(huds[i], "lantern", quiet);
            nulls += Require(huds[i], "fuelFill", quiet);
            nulls += Require(huds[i], "promptText", quiet);
            nulls += Require(huds[i], "timerText", quiet);
            nulls += Require(huds[i], "statusText", quiet);
            nulls += Require(huds[i], "pausePanel", quiet);
            nulls += Require(huds[i], "graphicsMenu", quiet);
            nulls += Require(huds[i], "graphicsButton", quiet);
            nulls += Require(huds[i], "fadeOverlay", quiet);
            nulls += Require(huds[i], "fuelMeter", quiet);
        }

        Lantern[] lanterns = Object.FindObjectsByType<Lantern>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lanterns.Length; i++)
        {
            nulls += Require(lanterns[i], "lanternLight", quiet);
            nulls += RequireNearKeeper(lanterns[i], quiet);
        }

        Tide[] tides = Object.FindObjectsByType<Tide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tides.Length; i++)
        {
            nulls += Require(tides[i], "waterRoot", quiet);
            nulls += Require(tides[i], "hazard", quiet);
        }

        Wind[] winds = Object.FindObjectsByType<Wind>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < winds.Length; i++)
        {
            nulls += Require(winds[i], "player", quiet);
            nulls += Require(winds[i], "hazard", quiet);
        }

        ShadeSpawner[] shadeSpawners = Object.FindObjectsByType<ShadeSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shadeSpawners.Length; i++)
        {
            nulls += Require(shadeSpawners[i], "shadePrefab", quiet);
            nulls += Require(shadeSpawners[i], "lantern", quiet);
            nulls += Require(shadeSpawners[i], "player", quiet);
            nulls += Require(shadeSpawners[i], "hazard", quiet);
            nulls += Require(shadeSpawners[i], "hud", quiet);
        }

        Lightning[] lightnings = Object.FindObjectsByType<Lightning>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lightnings.Length; i++)
        {
            nulls += Require(lightnings[i], "flashLight", quiet);
            nulls += Require(lightnings[i], "volume", quiet);
        }

        Rain[] rains = Object.FindObjectsByType<Rain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rains.Length; i++)
        {
            nulls += Require(rains[i], "system", quiet);
            nulls += Require(rains[i], "player", quiet);
        }

        Beacon[] beacons = Object.FindObjectsByType<Beacon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < beacons.Length; i++)
        {
            nulls += Require(beacons[i], "player", quiet);
            nulls += Require(beacons[i], "lantern", quiet);
        }

        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool expectFollow = IsIslandScene() || Object.FindAnyObjectByType<CameraFollow>() != null;
        for (int i = 0; i < players.Length; i++)
        {
            if (expectFollow)
            {
                nulls += Require(players[i], "cameraFollow", quiet);
            }
        }

        CameraFollow[] cameras = Object.FindObjectsByType<CameraFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            nulls += Require(cameras[i], "target", quiet);
        }

        if (needsLantern)
        {
            AudioManager[] audio = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < audio.Length; i++)
            {
                nulls += Require(audio[i], "lantern", quiet);
            }
        }

        if (Object.FindAnyObjectByType<PlayerController>() != null)
        {
            TerrainQuality[] terrainQuality = Object.FindObjectsByType<TerrainQuality>(FindObjectsInactive.Include);
            if (terrainQuality.Length == 0)
            {
                if (!quiet)
                {
                    Debug.LogWarning("Null reference: TerrainQuality missing from GraphicsAppliers");
                }

                nulls++;
            }

            for (int i = 0; i < terrainQuality.Length; i++)
            {
                nulls += Require(terrainQuality[i], "terrain", quiet);
            }

            Renderer[] waterRenderers = FindWaterRenderers();
            if (waterRenderers.Length > 0)
            {
                WaterQuality[] waterQuality = Object.FindObjectsByType<WaterQuality>(FindObjectsInactive.Include);
                if (waterQuality.Length == 0)
                {
                    if (!quiet)
                    {
                        Debug.LogWarning("Null reference: WaterQuality missing from GraphicsAppliers");
                    }

                    nulls++;
                }

                for (int i = 0; i < waterQuality.Length; i++)
                {
                    nulls += RequireArray(waterQuality[i], "waterRenderers", quiet);
                }
            }

            if (IsIslandScene())
            {
                KeeperQuality[] keeperQuality = Object.FindObjectsByType<KeeperQuality>(FindObjectsInactive.Include);
                if (keeperQuality.Length == 0)
                {
                    if (!quiet)
                    {
                        Debug.LogWarning("Null reference: KeeperQuality missing from GraphicsAppliers");
                    }

                    nulls++;
                }

                for (int i = 0; i < keeperQuality.Length; i++)
                {
                    nulls += RequireArray(keeperQuality[i], "keeperRenderers", quiet);
                    nulls += Require(keeperQuality[i], "chestFill", quiet);
                    nulls += Require(keeperQuality[i], "lanternLight", quiet);
                    nulls += Require(keeperQuality[i], "animator", quiet);
                }
            }

            if (IsIslandScene())
            {
                Beacon[] sceneBeacons = Object.FindObjectsByType<Beacon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Firefly[] sceneFireflies = Object.FindObjectsByType<Firefly>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (sceneBeacons.Length > 0 || sceneFireflies.Length > 0)
                {
                    LightQuality[] lightQuality = Object.FindObjectsByType<LightQuality>(FindObjectsInactive.Include);
                    if (lightQuality.Length == 0)
                    {
                        if (!quiet)
                        {
                            Debug.LogWarning("Null reference: LightQuality missing from GraphicsAppliers");
                        }

                        nulls++;
                    }

                    for (int i = 0; i < lightQuality.Length; i++)
                    {
                        if (sceneBeacons.Length > 0)
                        {
                            nulls += RequireArray(lightQuality[i], "beaconLights", quiet);
                        }

                        if (sceneFireflies.Length > 0)
                        {
                            nulls += RequireArray(lightQuality[i], "fireflyLights", quiet);
                        }
                    }
                }
            }
        }

        nulls += ReportSteppingStones(quiet);
        nulls += ReportHorizon(quiet);
        nulls += ReportGraphicsMenus(quiet);
        nulls += ReportLookProfiles(quiet);
        nulls += ReportLookApplier(quiet);
        nulls += ReportVolumes(quiet);
        nulls += ReportMoonRim(quiet);
        return nulls;
    }

    static UnityEngine.Rendering.Volume FindVolume(string objectName)
    {
        UnityEngine.Rendering.Volume[] volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i].gameObject.name == objectName)
            {
                return volumes[i];
            }
        }

        return null;
    }

    // Gameplay islands need LookVolume below EffectsVolume, with LowFuelFX and Lightning writing only to the effects volume.
    static int ReportVolumes(bool quiet)
    {
        LowFuelFX[] fxs = Object.FindObjectsByType<LowFuelFX>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Lightning[] lightnings = Object.FindObjectsByType<Lightning>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        UnityEngine.Rendering.Volume[] volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        string problem = null;
        for (int i = 0; i < volumes.Length && problem == null; i++)
        {
            UnityEngine.Rendering.VolumeProfile profile = volumes[i].sharedProfile;
            if (profile == null || !LowFuelFX.ProfileAlive(profile))
            {
                problem = volumes[i].name + " has a missing or dead profile";
            }
            else if (AssetDatabase.GetAssetPath(profile).Contains("NightVolumeProfile"))
            {
                problem = volumes[i].name + " references NightVolumeProfile";
            }
        }

        if (problem == null && (fxs.Length > 0 || lightnings.Length > 0))
        {
            UnityEngine.Rendering.Volume look = FindVolume("LookVolume");
            UnityEngine.Rendering.Volume effects = FindVolume("EffectsVolume");
            if (look == null || effects == null)
            {
                problem = "LookVolume or EffectsVolume is missing";
            }
            else if (effects.priority <= look.priority)
            {
                problem = "EffectsVolume priority " + effects.priority + " is not above LookVolume priority " + look.priority;
            }

            for (int i = 0; problem == null && i < fxs.Length; i++)
            {
                if (GetObject(fxs[i], "volume") != effects)
                {
                    problem = "LowFuelFX.volume is not EffectsVolume";
                }
            }

            for (int i = 0; problem == null && i < lightnings.Length; i++)
            {
                if (GetObject(lightnings[i], "volume") != effects)
                {
                    problem = "Lightning.volume is not EffectsVolume";
                }
            }
        }

        if (problem == null)
        {
            return 0;
        }

        if (!quiet)
        {
            Debug.LogWarning("Volume problem: " + EditorSceneManager.GetActiveScene().name + ": " + problem);
        }

        return 1;
    }

    static Object GetObject(Object target, string property)
    {
        SerializedProperty serialized = new SerializedObject(target).FindProperty(property);
        return serialized != null ? serialized.objectReferenceValue : null;
    }

    // Each island scene needs exactly one LookApplier whose profile matches the scene's LevelConfig, and the night sky shader.
    static int ReportLookApplier(bool quiet)
    {
        string sceneName = EditorSceneManager.GetActiveScene().name;
        LevelConfig config = null;
        string[] guids = AssetDatabase.FindAssets("t:LevelConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            LevelConfig candidate = AssetDatabase.LoadAssetAtPath<LevelConfig>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (candidate != null && candidate.sceneName == sceneName && sceneName.StartsWith("Island"))
            {
                config = candidate;
                break;
            }
        }

        if (config == null)
        {
            return 0;
        }

        string problem = null;
        LookApplier[] appliers = Object.FindObjectsByType<LookApplier>(FindObjectsInactive.Include);
        if (appliers.Length != 1)
        {
            problem = appliers.Length + " LookApplier components (expected 1)";
        }
        else if (appliers[0].Profile == null || appliers[0].Profile.levelId != config.levelId)
        {
            problem = "LookApplier profile does not match level '" + config.levelId + "'";
        }
        else if (RenderSettings.skybox == null || RenderSettings.skybox.shader.name != "LanternKeeper/NightSky")
        {
            problem = "RenderSettings.skybox is not a LanternKeeper/NightSky material";
        }

        if (problem == null)
        {
            return 0;
        }

        if (!quiet)
        {
            Debug.LogWarning("Look applier problem: " + sceneName + ": " + problem);
        }

        return 1;
    }

    // Every LevelConfig used by a build scene needs a lookProfile whose levelId matches its own.
    static int ReportLookProfiles(bool quiet)
    {
        int problems = 0;
        HashSet<string> buildScenes = new HashSet<string>();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            buildScenes.Add(System.IO.Path.GetFileNameWithoutExtension(scenes[i].path));
        }

        string[] guids = AssetDatabase.FindAssets("t:LevelConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (config == null || !buildScenes.Contains(config.sceneName))
            {
                continue;
            }

            string problem = null;
            if (config.lookProfile == null)
            {
                problem = "no lookProfile";
            }
            else if (config.lookProfile.levelId != config.levelId)
            {
                problem = "lookProfile levelId '" + config.lookProfile.levelId + "' does not match '" + config.levelId + "'";
            }
            else
            {
                config.lookProfile.Validate(out problem);
            }

            if (problem != null)
            {
                if (!quiet)
                {
                    Debug.LogWarning("Look profile problem: " + config.name + ": " + problem, config);
                }

                problems++;
            }
        }

        return problems;
    }

    // Every stepping stone and bank step needs exactly one flat, solid collider whose top matches the visible
    // top (a default primitive cylinder keeps a capsule that becomes a ball). Bank steps must also be always
    // solid (no LightRevealed). Each path, read in order, must climb in rises the player can step over and
    // must start and end next to walkable ground.
    static int ReportSteppingStones(bool quiet)
    {
        int problems = 0;
        GameObject pathRoot = GameObject.Find("HiddenPaths");
        if (pathRoot == null)
        {
            return 0;
        }

        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        Renderer[] water = FindWaterRenderers();
        float waterY = water.Length > 0 ? water[0].transform.position.y : float.MinValue;
        List<Transform> run = new List<Transform>();
        List<List<Transform>> paths = new List<List<Transform>>();
        for (int i = 0; i < pathRoot.transform.childCount; i++)
        {
            Transform stone = pathRoot.transform.GetChild(i);
            if (stone.name == "BankSupport")
            {
                if (stone.GetComponentInChildren<Collider>(true) != null)
                {
                    problems += StoneWarning(stone, "bank support must be visual only (has a collider)", quiet);
                }

                continue;
            }

            bool hidden = stone.name == "SteppingStone";
            bool bank = stone.name == "BankStep";
            if (!hidden && !bank)
            {
                continue;
            }

            string problem = StoneProblem(stone, bank);
            if (problem != null)
            {
                problems += StoneWarning(stone, problem, quiet);
            }

            if (run.Count > 0 && FlatDistance(run[run.Count - 1].position, stone.position) > 1.7f)
            {
                paths.Add(run);
                run = new List<Transform>();
            }

            run.Add(stone);
        }

        if (run.Count > 0)
        {
            paths.Add(run);
        }

        for (int p = 0; p < paths.Count; p++)
        {
            problems += ReportPath(paths[p], p, terrain, waterY, quiet);
        }

        return problems;
    }

    static string StoneProblem(Transform stone, bool bank)
    {
        Collider[] colliders = stone.GetComponents<Collider>();
        Renderer renderer = stone.GetComponent<Renderer>();
        if (colliders.Length != 1)
        {
            return "expected exactly one collider, found " + colliders.Length;
        }

        if (colliders[0].isTrigger)
        {
            return "collider is a trigger";
        }

        if (colliders[0] is CapsuleCollider || colliders[0] is SphereCollider)
        {
            return "collider is round (" + colliders[0].GetType().Name + ")";
        }

        if (renderer == null)
        {
            return "no renderer";
        }

        if (Mathf.Abs(colliders[0].bounds.max.y - renderer.bounds.max.y) > 0.02f)
        {
            return "collider top " + colliders[0].bounds.max.y.ToString("F3") + " vs visible top " + renderer.bounds.max.y.ToString("F3");
        }

        if (bank && stone.GetComponent<LightRevealed>() != null)
        {
            return "bank step must not be light-gated";
        }

        if (bank)
        {
            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                return "bank step has no material";
            }

            if (material.HasProperty("_LKDissolve") || (material.shader != null && material.shader.name == "LanternKeeper/PathReveal"))
            {
                return "bank step material dissolves by lantern distance (" + material.name + "); use a plain lit stone";
            }
        }

        return null;
    }

    static int StoneWarning(Transform stone, string problem, bool quiet)
    {
        if (!quiet)
        {
            Debug.LogWarning("Stepping stone problem: " + stone.name + " at " + stone.position + ": " + problem, stone);
        }

        return 1;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    static float StoneTop(Transform stone)
    {
        Collider collider = stone.GetComponent<Collider>();
        return collider != null ? collider.bounds.max.y : stone.position.y;
    }

    static int ReportPath(List<Transform> path, int index, Terrain terrain, float waterY, bool quiet)
    {
        int problems = 0;
        for (int i = 1; i < path.Count; i++)
        {
            float rise = StoneTop(path[i]) - StoneTop(path[i - 1]);
            if (Mathf.Abs(rise) > 0.4f)
            {
                problems += StoneWarning(path[i], "path " + index + ": rise of " + rise.ToString("F2") + " m from the previous step exceeds 0.4 m", quiet);
            }

            float gap = FlatDistance(path[i].position, path[i - 1].position);
            if (gap > 1.1f)
            {
                problems += StoneWarning(path[i], "path " + index + ": gap of " + gap.ToString("F2") + " m from the previous step", quiet);
            }
        }

        if (terrain != null)
        {
            string problem = EndProblem(path[0], terrain, waterY);
            if (problem != null)
            {
                problems += StoneWarning(path[0], "path " + index + " first step: " + problem, quiet);
            }

            problem = EndProblem(path[path.Count - 1], terrain, waterY);
            if (problem != null)
            {
                problems += StoneWarning(path[path.Count - 1], "path " + index + " last step: " + problem, quiet);
            }
        }

        return problems;
    }

    // The end step must have ground above the water within 1.1 m of its centre (about 0.6 m beyond its edge)
    // that is within 0.35 m of its top.
    static string EndProblem(Transform step, Terrain terrain, float waterY)
    {
        float top = StoneTop(step);
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        for (float radius = 0f; radius <= 1.1f; radius += 0.275f)
        {
            int samples = radius < 0.01f ? 1 : 16;
            for (int s = 0; s < samples; s++)
            {
                float angle = s * Mathf.PI * 2f / samples;
                float x = step.position.x + Mathf.Cos(angle) * radius;
                float z = step.position.z + Mathf.Sin(angle) * radius;
                float u = Mathf.Clamp01((x - origin.x) / size.x);
                float v = Mathf.Clamp01((z - origin.z) / size.z);
                float ground = origin.y + terrain.terrainData.GetInterpolatedHeight(u, v);
                if (ground >= waterY + 0.02f && Mathf.Abs(ground - top) <= 0.35f)
                {
                    return null;
                }
            }
        }

        return "no dry ground within 0.6 m of the step edge and within 0.35 m of its top " + top.ToString("F2");
    }

    static int WireGraphicsMenus()
    {
        int assigned = 0;
        GraphicsMenu[] menus = Object.FindObjectsByType<GraphicsMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            assigned += BindGraphicsMenu(menus[i]);
        }

        HUD[] huds = Object.FindObjectsByType<HUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            GraphicsMenu menu = huds[i].GetComponent<GraphicsMenu>();
            assigned += Set(huds[i], "graphicsMenu", menu);
            Transform pause = FindNamed(huds[i].transform, "PausePanel");
            Transform button = pause != null ? FindNamed(pause, "GraphicsButton") : null;
            assigned += Set(huds[i], "graphicsButton", button != null ? button.GetComponent<Button>() : null);
        }

        return assigned;
    }

    static int BindGraphicsMenu(GraphicsMenu menu)
    {
        if (menu == null)
        {
            return 0;
        }

        Transform panel = FindNamed(menu.transform, "GraphicsPanel");
        Transform readout = FindNamed(menu.transform, "FpsReadout");
        int assigned = 0;
        assigned += Set(menu, "panel", panel != null ? panel.gameObject : null);
        assigned += Set(menu, "lowButton", ButtonNamed(panel, "LowButton"));
        assigned += Set(menu, "mediumButton", ButtonNamed(panel, "MediumButton"));
        assigned += Set(menu, "highButton", ButtonNamed(panel, "HighButton"));
        assigned += Set(menu, "ultraButton", ButtonNamed(panel, "UltraButton"));
        assigned += Set(menu, "vsyncButton", ButtonNamed(panel, "VSyncButton"));
        assigned += Set(menu, "fpsButton", ButtonNamed(panel, "FpsButton"));
        assigned += Set(menu, "backButton", ButtonNamed(panel, "BackButton"));
        Transform status = panel != null ? FindNamed(panel, "GraphicsStatus") : null;
        assigned += Set(menu, "statusLabel", status != null ? status.gameObject : null);
        assigned += Set(menu, "fpsReadout", readout != null ? readout.gameObject : null);
        return assigned;
    }

    static int ReportGraphicsMenus(bool quiet)
    {
        bool expect = Object.FindAnyObjectByType<MainMenu>() != null || Object.FindAnyObjectByType<HUD>() != null;
        GraphicsMenu[] menus = Object.FindObjectsByType<GraphicsMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (menus.Length == 0)
        {
            if (expect && !quiet)
            {
                Debug.LogWarning("Null reference: GraphicsMenu missing");
            }

            return expect ? 1 : 0;
        }

        int nulls = 0;
        for (int i = 0; i < menus.Length; i++)
        {
            nulls += Require(menus[i], "panel", quiet);
            nulls += Require(menus[i], "lowButton", quiet);
            nulls += Require(menus[i], "mediumButton", quiet);
            nulls += Require(menus[i], "highButton", quiet);
            nulls += Require(menus[i], "ultraButton", quiet);
            nulls += Require(menus[i], "vsyncButton", quiet);
            nulls += Require(menus[i], "fpsButton", quiet);
            nulls += Require(menus[i], "backButton", quiet);
            nulls += Require(menus[i], "statusLabel", quiet);
            nulls += Require(menus[i], "fpsReadout", quiet);
        }

        return nulls;
    }

    static Button ButtonNamed(Transform root, string name)
    {
        Transform found = root != null ? FindNamed(root, name) : null;
        return found != null ? found.GetComponent<Button>() : null;
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static int ReportHorizon(bool quiet)
    {
        if (GameObject.Find("Horizon") == null)
        {
            return 0;
        }

        HorizonQuality[] quality = Object.FindObjectsByType<HorizonQuality>(FindObjectsInactive.Include);
        if (quality.Length == 0)
        {
            if (!quiet)
            {
                Debug.LogWarning("Null reference: HorizonQuality missing from GraphicsAppliers");
            }

            return 1;
        }

        int nulls = 0;
        int ranges = HorizonQuality.FindMountainRanges().Length;
        int ridges = HorizonQuality.FindExtraRidges().Length;
        int haze = HorizonQuality.FindHazeLayers().Length;
        int cameras = HorizonQuality.FindGameplayCameras().Length;
        for (int i = 0; i < quality.Length; i++)
        {
            nulls += RequireSized(quality[i], "mountainRanges", ranges, quiet);
            nulls += RequireSized(quality[i], "extraRidges", ridges, quiet);
            nulls += RequireSized(quality[i], "hazeLayers", haze, quiet);
            nulls += RequireSized(quality[i], "cameras", cameras, quiet);
        }

        return nulls;
    }

    static int RequireSized(Object target, string property, int expected, bool quiet)
    {
        if (target == null)
        {
            return 0;
        }

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        bool missing = prop == null || !prop.isArray || prop.arraySize != expected;
        if (!missing)
        {
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    missing = true;
                }
            }
        }

        if (missing && !quiet)
        {
            Debug.LogWarning("Null reference: " + target.GetType().Name + "." + property + " on " + target.name, target);
        }

        return missing ? 1 : 0;
    }

    static int Set(Object target, string property, Object value)
    {
        if (target == null || value == null)
        {
            return 0;
        }

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
        {
            return 0;
        }

        if (prop.objectReferenceValue == value)
        {
            return 0;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    static int SetArray(Object target, string property, Object[] values)
    {
        if (target == null || values == null)
        {
            return 0;
        }

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null || !prop.isArray)
        {
            return 0;
        }

        bool same = prop.arraySize == values.Length;
        for (int i = 0; i < values.Length && same; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue != values[i])
            {
                same = false;
            }
        }

        if (same)
        {
            return 0;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    static int Require(Object target, string property, bool quiet)
    {
        if (target == null)
        {
            return 0;
        }

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null)
        {
            return 0;
        }

        if (!quiet)
        {
            Debug.LogWarning("Null reference: " + target.GetType().Name + "." + property + " on " + target.name, target);
        }

        return 1;
    }

    // The lantern drives reveal, moth drain and shader globals from its own position, so it must sit at the keeper's hand.
    const float MaxLanternKeeperDistance = 1.5f;

    static int RequireNearKeeper(Lantern lantern, bool quiet)
    {
        if (lantern == null)
        {
            return 0;
        }

        PlayerController keeper = lantern.GetComponentInParent<PlayerController>();
        if (keeper == null)
        {
            return 0;
        }

        float distance = Vector3.Distance(lantern.transform.position, keeper.transform.position);
        if (distance <= MaxLanternKeeperDistance)
        {
            return 0;
        }

        if (!quiet)
        {
            Debug.LogWarning("Lantern is " + distance.ToString("0.00") + " m from the keeper (max " + MaxLanternKeeperDistance.ToString("0.0") + " m) on " + lantern.name, lantern);
        }

        return 1;
    }

    static int RequireArray(Object target, string property, bool quiet)
    {
        if (target == null)
        {
            return 0;
        }

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        int missing = 0;
        if (prop == null || !prop.isArray || prop.arraySize == 0)
        {
            missing = 1;
        }
        else
        {
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    missing++;
                }
            }
        }

        if (missing > 0 && !quiet)
        {
            Debug.LogWarning("Null reference: " + target.GetType().Name + "." + property + " on " + target.name, target);
        }

        return missing > 0 ? 1 : 0;
    }
}
}
