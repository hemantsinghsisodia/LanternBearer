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
        UnityEngine.Rendering.Volume volume = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        Transform player = body != null ? body.transform : null;

        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager != null)
        {
            assigned += Set(manager, "lantern", lantern);
            assigned += Set(manager, "hud", hud);
            assigned += Set(manager, "dawn", dawn);
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
        return assigned;
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

        nulls += ReportHorizon(quiet);
        nulls += ReportGraphicsMenus(quiet);
        return nulls;
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
