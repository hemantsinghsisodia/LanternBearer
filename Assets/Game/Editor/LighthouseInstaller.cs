using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Replaces the visuals of the Island 5 GreatLamp (placed by BridgeInstaller) with the lighthouse model
// (ArtSource/Blender/lighthouse_build.py -> Art/Lighthouse/Lighthouse.fbx). The GreatLamp object keeps its component, its SummitGate
// link and its point light, so the prompt, the gate check, the ignition and WinNow work unchanged.
// What it builds under the GreatLamp root: the model children (stone, iron, glass, FlameRoot, the _L1 LODs), a few flame quads
// inside FlameRoot (copied from the Beacon prefab, scaled up), the point light at the FlameRoot height, a mesh collider on the stone,
// and the beam (GreatLampBeam). The door faces the end of the summit span. Idempotent: it rebuilds all of it each run. The scene is
// edited through EditorSceneManager, never as text.
public static class LighthouseInstaller
{
    public const string ModelPath = "Assets/Game/Art/Lighthouse/Lighthouse.fbx";
    public const string BeamMeshPath = "Assets/Game/Art/Lighthouse/LampBeam.asset";
    public const string LampName = "GreatLamp";
    public const string BeamName = "LampBeam";
    public const float InteractRange = 4.5f;
    // A point this far from the tower axis (on the summit span side) is where the keeper can stand to light the lamp.
    public const float ApproachDistance = 4.0f;
    public const float FlameHeight = 11.9f;
    public const float BeamWidth = 3.5f;
    public const float BeamLength = 60f;
    const float BeamStart = 0.15f;          // BeaconBeam.shader BeamStart: the cards begin this far from the axis
    const float BeamStartHalfHeight = 0.6f;
    const float FlameScale = 5f;            // the Beacon flame pose scaled to fill the lantern room (2.6 m tall, 2.6 m across)
    const string BeaconPrefabPath = "Assets/Game/Prefabs/Gameplay/Beacon.prefab";

    [MenuItem("Lantern Keeper/Install Lighthouse (Island 5)")]
    public static void InstallMenu()
    {
        Install();
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        ConfigureModel();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Material cairn = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconCairnMaterialPath);
        Material iron = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconIronMaterialPath);
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconGlassMaterialPath);
        Material beamMaterial = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconBeamMaterialPath);
        GameObject beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeaconPrefabPath);
        if (model == null || cairn == null || iron == null || glass == null || beamMaterial == null || beaconPrefab == null)
        {
            Debug.LogError("LighthouseInstaller: missing the lighthouse model, the beacon materials or the Beacon prefab. Run lighthouse_build.py and the creature installer first.");
            return;
        }

        GreatLamp lamp = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == LampName)
            {
                lamp = root.GetComponent<GreatLamp>();
            }
        }

        if (lamp == null)
        {
            Debug.LogError("LighthouseInstaller: Island 5 has no GreatLamp. Run BridgeInstaller (Build Island 5) first.");
            return;
        }

        GameObject lampObject = lamp.gameObject;
        for (int i = lampObject.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(lampObject.transform.GetChild(i).gameObject);
        }

        foreach (Component component in lampObject.GetComponents<Component>())
        {
            if (component is Transform || component is GreatLamp)
            {
                continue;
            }

            Object.DestroyImmediate(component);
        }

        Vector2 toSpan = SpireLayout.PathPoint(1f) - SpireLayout.LampXZ();
        Vector3 doorDirection = toSpan.sqrMagnitude > 1e-6f ? new Vector3(toSpan.x, 0f, toSpan.y).normalized : Vector3.back;
        lampObject.transform.localScale = Vector3.one;
        // The model door faces its +Z (Blender -Y after the axis-baked export), so the door looks at the span end.
        lampObject.transform.rotation = Quaternion.LookRotation(doorDirection, Vector3.up);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        for (int i = instance.transform.childCount - 1; i >= 0; i--)
        {
            instance.transform.GetChild(i).SetParent(lampObject.transform, false);
        }

        Object.DestroyImmediate(instance);

        Renderer glassRenderer = null;
        System.Collections.Generic.List<Renderer> lod0 = new System.Collections.Generic.List<Renderer>();
        System.Collections.Generic.List<Renderer> lod1 = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer renderer in lampObject.GetComponentsInChildren<Renderer>(true))
        {
            string name = renderer.name;
            bool low = name.EndsWith("_L1");
            bool shadows = true;
            if (name.StartsWith("LighthouseStone"))
            {
                renderer.sharedMaterial = cairn;
            }
            else if (name.StartsWith("LighthouseIron"))
            {
                renderer.sharedMaterial = iron;
            }
            else if (name == "BeaconGlass")
            {
                renderer.sharedMaterial = glass;
                glassRenderer = renderer;
                shadows = false;
            }

            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (name == "BeaconGlass")
            {
                lod0.Add(renderer);
                lod1.Add(renderer);
            }
            else if (low)
            {
                lod1.Add(renderer);
            }
            else
            {
                lod0.Add(renderer);
            }
        }

        Transform flameRoot = lampObject.transform.Find("FlameRoot");
        Transform stone = lampObject.transform.Find("LighthouseStone");
        if (flameRoot == null || stone == null || glassRenderer == null)
        {
            Debug.LogError("LighthouseInstaller: the lighthouse model lacks FlameRoot, LighthouseStone or BeaconGlass. Rebuild it with lighthouse_build.py.");
            return;
        }

        flameRoot.localPosition = new Vector3(0f, FlameHeight, 0f);
        flameRoot.localRotation = Quaternion.identity;
        CopyFlame(beaconPrefab.transform.Find("FlameRoot"), flameRoot);

        MeshCollider collider = stone.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = stone.GetComponent<MeshFilter>().sharedMesh;

        GameObject lightObject = new GameObject("LampLight");
        lightObject.transform.SetParent(lampObject.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, FlameHeight, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.26f);
        light.range = 34f;
        light.intensity = 8f;
        light.shadows = LightShadows.None;

        GameObject beamObject = new GameObject(BeamName);
        beamObject.transform.SetParent(lampObject.transform, false);
        beamObject.transform.localPosition = new Vector3(0f, FlameHeight, 0f);
        beamObject.AddComponent<MeshFilter>().sharedMesh = EnsureBeamMesh();
        MeshRenderer beamRenderer = beamObject.AddComponent<MeshRenderer>();
        beamRenderer.sharedMaterial = beamMaterial;
        beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;
        beamRenderer.enabled = false;
        GreatLampBeam beam = beamObject.AddComponent<GreatLampBeam>();
        SerializedObject beamData = new SerializedObject(beam);
        beamData.FindProperty("beamRenderer").objectReferenceValue = beamRenderer;
        beamData.ApplyModifiedPropertiesWithoutUndo();
        lod0.Add(beamRenderer);
        lod1.Add(beamRenderer);

        LODGroup group = lampObject.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.08f, lod0.ToArray()), new LOD(0f, lod1.ToArray()) });
        group.size = 15f;
        group.fadeMode = LODFadeMode.None;

        SerializedObject lampData = new SerializedObject(lamp);
        lampData.FindProperty("interactRange").floatValue = InteractRange;
        lampData.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LighthouseInstaller: placed the lighthouse at " + lampObject.transform.position + ", door facing " + doorDirection + ".");
    }

    static void ConfigureModel()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("LighthouseInstaller: missing " + ModelPath + ". Run ArtSource/Blender/lighthouse_build.py first.");
            return;
        }

        bool dirty = importer.materialImportMode != ModelImporterMaterialImportMode.None
            || importer.bakeAxisConversion || importer.importCameras || importer.importLights
            || importer.importAnimation || importer.isReadable;
        if (!dirty)
        {
            return;
        }

        // lighthouse_build.py bakes the axis conversion into the FBX like beacon_build.py: the mesh is already in Unity space.
        importer.globalScale = 1f;
        importer.bakeAxisConversion = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    // The Beacon flame quads, enlarged to burn inside the lantern room. GreatLamp grows FlameRoot from 0 to 1 on ignition.
    static void CopyFlame(Transform source, Transform target)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.childCount; i++)
        {
            Transform part = source.GetChild(i);
            if (part.GetComponent<MeshRenderer>() == null)
            {
                continue;
            }

            GameObject copy = Object.Instantiate(part.gameObject);
            copy.name = part.name;
            copy.transform.SetParent(target, false);
            copy.transform.localPosition = part.localPosition * FlameScale;
            copy.transform.localRotation = part.localRotation;
            copy.transform.localScale = part.localScale * FlameScale;
            MeshRenderer renderer = copy.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    // Two opposed vertical cards along +X and -X from the lantern axis. UV.x is 0 at the lantern and 1 at the tip, UV.y across: the
    // BeaconBeam shader takes the outward direction from the XZ position. The cards fan out from about 1.2 m to BeamWidth in height.
    static Mesh EnsureBeamMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
        bool create = mesh == null;
        if (create)
        {
            mesh = new Mesh();
        }

        mesh.Clear();
        mesh.name = "LampBeam";
        Vector3[] vertices = new Vector3[8];
        Vector2[] uvs = new Vector2[8];
        int[] triangles = new int[12];
        float tip = BeamWidth * 0.5f;
        for (int card = 0; card < 2; card++)
        {
            float side = card == 0 ? 1f : -1f;
            int b = card * 4;
            vertices[b] = new Vector3(side * BeamStart, -BeamStartHalfHeight, 0f);
            vertices[b + 1] = new Vector3(side * BeamStart, BeamStartHalfHeight, 0f);
            vertices[b + 2] = new Vector3(side * (BeamStart + BeamLength), tip, 0f);
            vertices[b + 3] = new Vector3(side * (BeamStart + BeamLength), -tip, 0f);
            uvs[b] = new Vector2(0f, 0f);
            uvs[b + 1] = new Vector2(0f, 1f);
            uvs[b + 2] = new Vector2(1f, 1f);
            uvs[b + 3] = new Vector2(1f, 0f);
            int t = card * 6;
            triangles[t] = b;
            triangles[t + 1] = b + 1;
            triangles[t + 2] = b + 2;
            triangles[t + 3] = b;
            triangles[t + 4] = b + 2;
            triangles[t + 5] = b + 3;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        if (create)
        {
            AssetDatabase.CreateAsset(mesh, BeamMeshPath);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    static Scene OpenScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == BridgeInstaller.ScenePath)
            {
                SceneManager.SetActiveScene(loaded);
                return loaded;
            }
        }

        return EditorSceneManager.OpenScene(BridgeInstaller.ScenePath, OpenSceneMode.Single);
    }
}
}
