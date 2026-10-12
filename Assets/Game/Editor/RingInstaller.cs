using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Adds the Island 5 height-ring systems: RingZones, RingHazards, ThinAir, the ring look driver, and the storm (Wind, Shade patrol,
// Lightning, Rain) switched off until RingHazards turns it on. Also points the Moth spawner at the walkway. Idempotent: it removes
// the "Rings" root and builds it again. The scene is edited through EditorSceneManager (opened if it is not already), never as text.
public static class RingInstaller
{
    public const string ScenePath = "Assets/Game/Scenes/Island5.unity";
    const string RootName = "Rings";
    const string ShadePrefabPath = "Assets/Game/Prefabs/Gameplay/Shade.prefab";
    const string RainMaterialPath = "Assets/Game/Materials/Generated/SkyUnlit.mat";
    const int Seed = 5505;
    const float WindStrength = 0.6f;
    const int ShadeCount = 2;
    // The walkable island is about 41 m across from the centre; the config radius (45 m) reaches the sea.
    const float SpawnRadius = 41f;

    [MenuItem("Lantern Keeper/Install Height Rings On Island 5")]
    public static void InstallMenu()
    {
        Install();
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        Remove(scene, RootName);

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        WaterHazard hazard = Object.FindAnyObjectByType<WaterHazard>();
        GameObject water = GameObject.Find("Water");
        GameObject effects = GameObject.Find("EffectsVolume");
        Volume effectsVolume = effects != null ? effects.GetComponent<Volume>() : null;
        if (player == null || water == null)
        {
            Debug.LogError("RingInstaller: Island 5 needs the Keeper and the Water. Build Island 5 first.");
            return;
        }

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        GameObject windHost = Child(root, "Wind", false);
        Wind wind = windHost.AddComponent<Wind>();
        SerializedObject windData = new SerializedObject(wind);
        windData.FindProperty("strength").floatValue = WindStrength;
        windData.FindProperty("seed").intValue = Seed;
        windData.FindProperty("player").objectReferenceValue = player;
        windData.FindProperty("hazard").objectReferenceValue = hazard;
        windData.ApplyModifiedPropertiesWithoutUndo();

        GameObject shadeHost = Child(root, "ShadeSpawner", false);
        ShadeSpawner shades = shadeHost.AddComponent<ShadeSpawner>();
        SerializedObject shadeData = new SerializedObject(shades);
        shadeData.FindProperty("shadePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ShadePrefabPath);
        shadeData.FindProperty("baseCount").intValue = ShadeCount;
        shadeData.FindProperty("islandRadius").floatValue = SpawnRadius;
        shadeData.FindProperty("walkwayOnly").boolValue = true;
        shadeData.FindProperty("player").objectReferenceValue = player;
        shadeData.FindProperty("lantern").objectReferenceValue = player.GetComponentInChildren<Lantern>();
        shadeData.FindProperty("hazard").objectReferenceValue = hazard;
        shadeData.ApplyModifiedPropertiesWithoutUndo();

        GameObject lightningHost = Child(root, "Lightning", false);
        Lightning lightning = lightningHost.AddComponent<Lightning>();
        GameObject lightObject = new GameObject("FlashLight");
        lightObject.transform.SetParent(lightningHost.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        Light flash = lightObject.AddComponent<Light>();
        flash.type = LightType.Directional;
        flash.color = new Color(0.78f, 0.86f, 1f);
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;
        flash.enabled = false;
        SerializedObject lightningData = new SerializedObject(lightning);
        lightningData.FindProperty("seed").intValue = Seed;
        lightningData.FindProperty("flashLight").objectReferenceValue = flash;
        lightningData.FindProperty("volume").objectReferenceValue = effectsVolume;
        lightningData.ApplyModifiedPropertiesWithoutUndo();

        GameObject rainHost = Child(root, "Rain", false);
        BuildRain(rainHost, player);

        GameObject rings = Child(root, "RingControl", true);
        RingZones zones = rings.AddComponent<RingZones>();
        SerializedObject zonesData = new SerializedObject(zones);
        zonesData.FindProperty("player").objectReferenceValue = player;
        zonesData.FindProperty("seaLevel").floatValue = water.transform.position.y;
        zonesData.ApplyModifiedPropertiesWithoutUndo();
        RingHazards hazards = rings.AddComponent<RingHazards>();
        SerializedObject hazardData = new SerializedObject(hazards);
        hazardData.FindProperty("wind").objectReferenceValue = windHost;
        hazardData.FindProperty("shadeSpawner").objectReferenceValue = shadeHost;
        hazardData.FindProperty("lightning").objectReferenceValue = lightningHost;
        hazardData.FindProperty("rain").objectReferenceValue = rainHost;
        hazardData.ApplyModifiedPropertiesWithoutUndo();
        rings.AddComponent<ThinAir>();
        rings.AddComponent<Island5Look>();

        MothSpawner moths = Object.FindAnyObjectByType<MothSpawner>();
        if (moths != null)
        {
            SerializedObject mothData = new SerializedObject(moths);
            mothData.FindProperty("islandRadius").floatValue = SpawnRadius;
            mothData.FindProperty("walkwayOnly").boolValue = true;
            mothData.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("RingInstaller: placed the height rings, thin air, ring look and the switched-off storm in " + scene.name + ".");
    }

    static GameObject Child(GameObject root, string name, bool active)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.SetActive(active);
        return child;
    }

    // Falling streaks in a box above the camera, as on Island 4 (Rain.cs moves the box with the camera).
    static void BuildRain(GameObject host, PlayerController player)
    {
        ParticleSystem system = host.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = true;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 0.9f;
        main.startSpeed = 20f;
        main.startSize = 0.05f;
        main.startColor = new Color(0.72f, 0.8f, 0.92f, 0.35f);
        main.maxParticles = 1200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 600f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 34f, 0.5f);
        shape.rotation = new Vector3(90f, 0f, 0f);
        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.04f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Rain rain = host.AddComponent<Rain>();
        SerializedObject rainData = new SerializedObject(rain);
        rainData.FindProperty("system").objectReferenceValue = system;
        rainData.FindProperty("player").objectReferenceValue = player;
        rainData.ApplyModifiedPropertiesWithoutUndo();
    }

    static Scene OpenScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == ScenePath)
            {
                SceneManager.SetActiveScene(loaded);
                return loaded;
            }
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    static void Remove(Scene scene, string rootName)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == rootName)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
}
