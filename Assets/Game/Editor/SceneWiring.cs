using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        WaterHazard[] water = Object.FindObjectsByType<WaterHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < water.Length; i++)
        {
            assigned += Set(water[i], "player", body);
            assigned += Set(water[i], "lantern", lantern);
            assigned += Set(water[i], "hud", hud);
            assigned += Set(water[i], "cameraFollow", follow);
        }

        LightRevealed[] revealed = Object.FindObjectsByType<LightRevealed>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < revealed.Length; i++)
        {
            assigned += Set(revealed[i], "lantern", lantern);
        }

        assigned += WireTerrainQuality();
        assigned += WireWaterQuality();
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
            nulls += Require(huds[i], "fadeOverlay", quiet);
            nulls += Require(huds[i], "fuelMeter", quiet);
        }

        Lantern[] lanterns = Object.FindObjectsByType<Lantern>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lanterns.Length; i++)
        {
            nulls += Require(lanterns[i], "lanternLight", quiet);
        }

        Beacon[] beacons = Object.FindObjectsByType<Beacon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < beacons.Length; i++)
        {
            nulls += Require(beacons[i], "player", quiet);
            nulls += Require(beacons[i], "lantern", quiet);
        }

        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            nulls += Require(players[i], "cameraFollow", quiet);
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
        }

        return nulls;
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
