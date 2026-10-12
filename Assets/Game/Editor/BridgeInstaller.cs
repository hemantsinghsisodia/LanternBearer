using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Places the Island 5 light bridges (one per SpireMarkers/GapN marker), the bridge lanterns, the summit gate and the great lamp
// placeholder. Idempotent: it removes what it placed before and builds it again. The scene is edited through EditorSceneManager
// (opened if it is not already, modified, saved), never as text.
public static class BridgeInstaller
{
    public const string ScenePath = "Assets/Game/Scenes/Island5.unity";
    const string BeaconPrefabPath = "Assets/Game/Prefabs/Gameplay/Beacon.prefab";
    const string LanternPrefabPath = "Assets/Game/Prefabs/Gameplay/BridgeLantern.prefab";
    const string DotPath = "Assets/Game/Textures/Generated/BridgeDot.png";
    const string MaterialFolder = "Assets/Game/Materials/Generated";
    const string RootName = "LightBridges";
    const string GateName = "SummitGate";
    const string LampName = "GreatLamp";
    const float DeckWidth = 3.4f;
    const float LampScale = 2.2f;
    const float BridgeLanternScale = 0.62f;

    [MenuItem("Lantern Keeper/Install Light Bridges On Island 5")]
    public static void InstallMenu()
    {
        Install();
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        EnsureAssets();
        Material deck = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/LightBridge.mat");
        Material ember = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/BridgeEmber.mat");
        Material beam = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/BridgeBeam.mat");
        Material column = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/SummitColumn.mat");
        GameObject lanternPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefabPath);

        Remove(scene, RootName);
        Remove(scene, GateName);
        Remove(scene, LampName);

        GameObject markers = FindRoot(scene, "SpireMarkers");
        GameObject beacons = FindRoot(scene, "Beacons");
        Terrain terrain = Terrain.activeTerrain;
        if (markers == null || beacons == null || terrain == null)
        {
            Debug.LogError("BridgeInstaller: Island 5 needs SpireMarkers, Beacons and a terrain. Build Island 5 first.");
            return;
        }

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        SpireGap[] gaps = SpireLayout.Gaps();
        for (int i = 0; i < gaps.Length; i++)
        {
            Transform marker = markers.transform.Find("Gap" + i);
            if (marker == null)
            {
                Debug.LogError("BridgeInstaller: missing SpireMarkers/Gap" + i);
                continue;
            }

            Vector3 near = Surface(terrain, marker.Find("Near").position, marker.Find("Far").position);
            Vector3 far = Surface(terrain, marker.Find("Far").position, marker.Find("Near").position);
            Vector3[] line = WalkwayLine(i, near, far);
            Vector3 dir = (far - line[line.Length - 2]).normalized;

            GameObject bridgeObject = new GameObject("LightBridge_Gap" + i);
            bridgeObject.transform.SetParent(root.transform, false);
            bridgeObject.transform.position = 0.5f * (near + far);
            LightBridge bridge = bridgeObject.AddComponent<LightBridge>();
            Transform beaconTransform = beacons.transform.Find("Beacon_" + gaps[i].nearBeacon);
            bridge.NearBeacon = beaconTransform != null ? beaconTransform.GetComponent<Beacon>() : null;
            bridge.FarBeacon = null;
            bridge.Required = gaps[i].required;
            bridge.SummitSpan = i == 3;
            Transform lantern = null;
            if (i < 3 && lanternPrefab != null)
            {
                lantern = PlaceLantern(terrain, lanternPrefab, root.transform, "BridgeLantern_Gap" + i, far, dir);
            }

            bridge.Configure(line, DeckWidth, lantern, deck, ember, beam);
            EditorUtility.SetDirty(bridge);
        }

        GameObject gateObject = new GameObject(GateName);
        SceneManager.MoveGameObjectToScene(gateObject, scene);
        SummitGate gate = gateObject.AddComponent<SummitGate>();
        SerializedObject gateData = new SerializedObject(gate);
        gateData.FindProperty("columnMaterial").objectReferenceValue = column;
        gateData.FindProperty("required").intValue = beacons.transform.childCount;
        gateData.ApplyModifiedPropertiesWithoutUndo();

        Transform slot = markers.transform.Find("GreatLampSlot");
        if (slot != null && lanternPrefab != null)
        {
            GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(lanternPrefab, scene);
            lamp.name = LampName;
            Vector3 position = slot.position;
            position.y = Mathf.Max(position.y, terrain.SampleHeight(position) + terrain.transform.position.y);
            lamp.transform.position = position;
            lamp.transform.localScale = Vector3.one * LampScale;
            GreatLamp component = lamp.AddComponent<GreatLamp>();
            component.Configure(gate);
            SerializedObject lampData = new SerializedObject(component);
            lampData.ApplyModifiedPropertiesWithoutUndo();
        }

        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager != null)
        {
            SerializedObject managerData = new SerializedObject(manager);
            managerData.FindProperty("deferWinToGreatLamp").boolValue = true;
            managerData.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("BridgeInstaller: placed " + gaps.Length + " light bridges, the summit gate and the great lamp in " + scene.name + ".");
    }

    // The straight chord for gaps 1-3; the summit span follows the spiral (its chord would cut through the plateau), with the height
    // rising evenly along its length.
    static Vector3[] WalkwayLine(int gap, Vector3 near, Vector3 far)
    {
        if (gap < 3)
        {
            return new[] { near, far };
        }

        Vector2 span = SpireLayout.GapSpanU(gap);
        const int segments = 8;
        Vector3[] line = new Vector3[segments + 1];
        float total = 0f;
        float[] cumulative = new float[segments + 1];
        for (int k = 0; k <= segments; k++)
        {
            Vector2 p = SpireLayout.PathPoint(Mathf.Lerp(span.x, span.y, k / (float)segments));
            line[k] = new Vector3(p.x, 0f, p.y);
            if (k > 0)
            {
                total += Vector3.Distance(line[k - 1], line[k]);
            }

            cumulative[k] = total;
        }

        for (int k = 0; k <= segments; k++)
        {
            line[k].y = Mathf.Lerp(near.y, far.y, cumulative[k] / total);
        }

        line[0] = near;
        line[segments] = far;
        return line;
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

    static GameObject FindRoot(Scene scene, string rootName)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == rootName)
            {
                return go;
            }
        }

        return null;
    }

    static void Remove(Scene scene, string rootName)
    {
        GameObject existing;
        while ((existing = FindRoot(scene, rootName)) != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    // The walkway surface at a marker: the higher of the terrain at the marker and half a metre inside the walkway (away from the gap),
    // so a sample on the cliff edge itself cannot lower the bridge end.
    static Vector3 Surface(Terrain terrain, Vector3 marker, Vector3 otherEnd)
    {
        Vector3 inward = marker - otherEnd;
        inward.y = 0f;
        inward = inward.sqrMagnitude > 0.0001f ? inward.normalized : Vector3.forward;
        float offset = terrain.transform.position.y;
        float at = terrain.SampleHeight(marker) + offset;
        float inside = terrain.SampleHeight(marker + inward * 0.5f) + offset;
        return new Vector3(marker.x, Mathf.Max(at, inside), marker.z);
    }

    // The bridge lantern stands just past the far end of the deck, on the walkway edge whose ground matches the deck end.
    static Transform PlaceLantern(Terrain terrain, GameObject prefab, Transform parent, string objectName, Vector3 far, Vector3 dir)
    {
        Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, flat);
        float offset = terrain.transform.position.y;
        Vector3 best = far;
        float bestError = float.MaxValue;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Vector3 candidate = far + flat * 1.2f + side * (sign * 1.2f);
            float y = terrain.SampleHeight(candidate) + offset;
            float error = Mathf.Abs(y - far.y);
            if (error < bestError)
            {
                bestError = error;
                best = new Vector3(candidate.x, y, candidate.z);
            }
        }

        GameObject lantern = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        lantern.name = objectName;
        lantern.transform.position = best;
        lantern.transform.localScale = Vector3.one * BridgeLanternScale;
        return lantern.transform;
    }

    // Texture, materials and the lantern prefab. Each is created once and left alone afterwards.
    public static void EnsureAssets()
    {
        if (!Directory.Exists(MaterialFolder))
        {
            Directory.CreateDirectory(MaterialFolder);
        }

        Texture2D dot = EnsureDot();
        Color amber = LookPalette.FromHex(LookPalette.LanternAmber);
        Color core = LookPalette.FromHex(LookPalette.GlowCore);
        Shader bridgeShader = Shader.Find("LanternKeeper/LightBridge");
        Shader additive = Shader.Find("LanternKeeper/AdditiveUnlit");

        Material deck = EnsureMaterial("LightBridge", bridgeShader);
        deck.SetColor("_BaseColor", amber);
        deck.SetColor("_CoreColor", core);
        deck.enableInstancing = true;
        deck.renderQueue = 3000;
        EditorUtility.SetDirty(deck);

        Material ember = EnsureMaterial("BridgeEmber", additive);
        ember.SetTexture("_BaseMap", dot);
        ember.SetColor("_BaseColor", new Color(amber.r, amber.g, amber.b, 0.9f));
        ember.enableInstancing = true;
        ember.renderQueue = 3000;
        EditorUtility.SetDirty(ember);

        Material beam = EnsureMaterial("BridgeBeam", additive);
        beam.SetTexture("_BaseMap", dot);
        beam.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
        beam.renderQueue = 3000;
        EditorUtility.SetDirty(beam);

        Material column = EnsureMaterial("SummitColumn", additive);
        column.SetTexture("_BaseMap", dot);
        column.SetColor("_BaseColor", new Color(amber.r, amber.g, amber.b, 0.45f));
        column.renderQueue = 3000;
        EditorUtility.SetDirty(column);

        AssetDatabase.SaveAssets();
        EnsureLanternPrefab();
    }

    static Texture2D EnsureDot()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
        if (existing != null)
        {
            return existing;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - r, 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DotPath));
        File.WriteAllBytes(DotPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(DotPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(DotPath);
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
    }

    static Material EnsureMaterial(string materialName, Shader shader)
    {
        string path = MaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        return material;
    }

    // A small storm lantern with a flame that starts out: the cairn, iron frame and glass of the beacon, and its flame and light.
    static void EnsureLanternPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefabPath);
        if (existing != null && existing.transform.Find("BeaconBeams") != null)
        {
            return;
        }

        GameObject source = PrefabUtility.LoadPrefabContents(BeaconPrefabPath);
        GameObject root = new GameObject("BridgeLantern");
        string[] visual = { "BeaconCairn", "BeaconIron", "BeaconGlass", "BeaconBeams" };
        for (int i = 0; i < visual.Length; i++)
        {
            Transform part = source.transform.Find("Visual/" + visual[i]);
            if (part != null)
            {
                GameObject clone = Object.Instantiate(part.gameObject, root.transform, false);
                clone.name = visual[i];
            }
        }

        Transform flame = source.transform.Find("FlameRoot");
        if (flame != null)
        {
            GameObject clone = Object.Instantiate(flame.gameObject, root.transform, false);
            clone.name = "FlameRoot";
            string[] drop = { "Sparks", "Smoke" };
            for (int i = 0; i < drop.Length; i++)
            {
                Transform extra = clone.transform.Find(drop[i]);
                if (extra != null)
                {
                    Object.DestroyImmediate(extra.gameObject);
                }
            }

            Light light = clone.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.shadows = LightShadows.None;
                light.range = 9f;
                light.intensity = 3f;
                light.enabled = false;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, LanternPrefabPath);
        Object.DestroyImmediate(root);
        PrefabUtility.UnloadPrefabContents(source);
    }
}
}
