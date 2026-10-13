using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// The story says the storm put the Great Light out, so the distant lighthouse on the horizon is its dark tower. IslandBuilder
// still places the old scenery lighthouse (primitive tower, roof, sweeping beam); this installer turns it into the Great Light:
//  - Islands 1-4: the tower and roof are replaced by Lighthouse.fbx (stone, iron, the cold glint glass), same islet and base height,
//    scaled to the old 14.7 m silhouette. The beam, lamp light and lamp are removed.
//  - Island 5 (it has the real lighthouse): the scenery Lighthouse and the islet mound it stood on are removed.
//  - MainMenu: as Islands 1-4, but the lamp pivot (beam, light, lamp) is kept inactive under the tower and MenuGreatLight lights it
//    when Island 5 has been won.
// Idempotent: the model container is rebuilt on each run. Scenes are edited through EditorSceneManager, never as text.
public static class HorizonLighthouseInstaller
{
    public const string HouseName = "Lighthouse";
    public const string ContainerName = "GreatLight";
    public const string PivotName = "LampPivot";
    public const string BeamName = "GreatLightBeam";
    // The old scenery tower plus roof stood about 14.7 m; the model is 14 m.
    public const float ModelHeight = 14f;
    public const float TargetHeight = 14.7f;
    const string MenuScene = "MainMenu";

    static readonly string[] ScenePaths =
    {
        "Assets/Game/Scenes/Island1.unity",
        "Assets/Game/Scenes/Island2.unity",
        "Assets/Game/Scenes/Island3.unity",
        "Assets/Game/Scenes/Island4.unity",
        "Assets/Game/Scenes/Island5.unity",
        "Assets/Game/Scenes/MainMenu.unity"
    };

    [MenuItem("Lantern Keeper/Install Horizon Lighthouse")]
    public static void InstallMenu()
    {
        Install();
    }

    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("HorizonLighthouseInstaller: exit Play mode first.");
            return;
        }

        EditorSceneManager.SaveOpenScenes();
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
            if (Apply(scene))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (setup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("HorizonLighthouseInstaller: done.");
    }

    // Island 5's step in IslandBuilder.Island5PostSteps: removes the scenery lighthouse from the Island 5 scene (the build leaves it open).
    // PlaceLighthouse already skips spire builds, so on a fresh build this finds nothing. Idempotent.
    public static void InstallIsland5()
    {
        const string scenePath = "Assets/Game/Scenes/Island5.unity";
        Scene scene = default(Scene);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == scenePath)
            {
                scene = loaded;
                break;
            }
        }

        if (!scene.IsValid())
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (Apply(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // Edits the open scene in memory (the caller saves). False if there was nothing to do or an asset was missing.
    public static bool Apply(Scene scene)
    {
        return Apply(scene, scene.name == MenuScene, scene.name == "Island5");
    }

    // The same for a scene whose name is not the shipped one (a scratch build): the caller says whether it is the menu or the spire.
    public static bool Apply(Scene scene, bool menu, bool spire)
    {
        Transform house = FindHouse(scene);
        if (house == null)
        {
            return spire && RemoveEmptyLighthouseIslet(scene);
        }

        if (spire)
        {
            // The lighthouse and the islet mound it stood on (the islet's pine goes with it): the horizon has no empty islet on Island 5.
            Transform islet = house.parent;
            Object.DestroyImmediate(islet != null ? islet.gameObject : house.gameObject);
            return true;
        }

        LighthouseInstaller.ConfigureModel();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(LighthouseInstaller.ModelPath);
        Material iron = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconIronMaterialPath);
        if (model == null || iron == null)
        {
            Debug.LogError("HorizonLighthouseInstaller: missing the lighthouse model or the beacon iron material.");
            return false;
        }

        Material cairn = LighthouseInstaller.EnsureStoneMaterial();
        Material glass = LighthouseInstaller.EnsureGlassMaterial();
        Material wood = LighthouseInstaller.EnsureWoodMaterial();
        Material strap = LighthouseInstaller.EnsureStrapMaterial();
        Material doorGlow = LighthouseInstaller.EnsureDoorGlowMaterial();
        if (cairn == null || glass == null || wood == null || strap == null || doorGlow == null)
        {
            return false;
        }

        // The base height on the islet: from the old tower, or from the container of an earlier run.
        float baseY;
        Transform container = house.Find(ContainerName);
        Transform tower = house.Find("Tower");
        if (container != null)
        {
            baseY = container.localPosition.y;
        }
        else if (tower != null)
        {
            baseY = tower.localPosition.y - tower.localScale.y;
        }
        else
        {
            Debug.LogError("HorizonLighthouseInstaller: the Lighthouse has neither a Tower nor a GreatLight.");
            return false;
        }

        Transform pivot = house.Find(PivotName);
        float lampY = baseY + LighthouseInstaller.FlameHeight * Scale();
        // Remove everything of the old scenery tower; the pivot is kept for the menu only.
        for (int i = house.childCount - 1; i >= 0; i--)
        {
            Transform child = house.GetChild(i);
            if (menu && child == pivot)
            {
                continue;
            }

            Object.DestroyImmediate(child.gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = ContainerName;
        instance.transform.SetParent(house, false);
        instance.transform.localPosition = new Vector3(0f, baseY, 0f);
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * Scale();
        // Dark: no flame, no door glow.
        for (int i = instance.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = instance.transform.GetChild(i);
            if (child.name == "FlameRoot" || child.name == "LighthouseDoorGlow")
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        List<Renderer> lod0 = new List<Renderer>();
        List<Renderer> lod1 = new List<Renderer>();
        LighthouseInstaller.AssignModelMaterials(instance.transform, cairn, iron, glass, wood, strap, doorGlow, lod0, lod1);
        // A silhouette 170 m out: no shadows.
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        LODGroup group = instance.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.08f, lod0.ToArray()), new LOD(0f, lod1.ToArray()) });
        group.size = 15f;
        group.fadeMode = LODFadeMode.None;

        MenuGreatLight existing = house.GetComponent<MenuGreatLight>();
        if (menu && pivot == null)
        {
            Debug.LogError("HorizonLighthouseInstaller: the menu Lighthouse has no " + PivotName + "; the menu Great Light stays dark. Rebuild the menu (IslandBuilder.BuildMainMenu).");
        }
        else if (menu)
        {
            pivot.localPosition = new Vector3(0f, lampY, 0f);
            GreatLampBeam beam = ConvertToAmberBeam(pivot);
            pivot.gameObject.SetActive(false);
            MenuGreatLight light = existing != null ? existing : house.gameObject.AddComponent<MenuGreatLight>();
            SerializedObject data = new SerializedObject(light);
            data.FindProperty("litRoot").objectReferenceValue = pivot.gameObject;
            data.FindProperty("beam").objectReferenceValue = beam;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        return true;
    }

    // The lit menu Great Light looks like the finale's: the pale sweeping cards of the old scenery beam are replaced by the lighthouse's own
    // amber BeaconBeam cards (GreatLampBeam, which turns itself), at twice the plateau lamp's size for the 170 m distance. The pivot keeps the
    // lamp and its light. Idempotent.
    static GreatLampBeam ConvertToAmberBeam(Transform pivot)
    {
        for (int i = pivot.childCount - 1; i >= 0; i--)
        {
            Transform child = pivot.GetChild(i);
            if (child.name == "BeamCore" || child.name == "BeamSoft" || child.name == BeamName)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        LighthouseBeam old = pivot.GetComponent<LighthouseBeam>();
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconBeamMaterialPath);
        if (material == null)
        {
            Debug.LogError("HorizonLighthouseInstaller: BeaconBeam.mat is missing.");
            return null;
        }

        GameObject beamObject = new GameObject(BeamName);
        beamObject.transform.SetParent(pivot, false);
        beamObject.transform.localPosition = Vector3.zero;
        beamObject.transform.localRotation = Quaternion.identity;
        beamObject.transform.localScale = Vector3.one * 2f;
        beamObject.AddComponent<MeshFilter>().sharedMesh = LighthouseInstaller.EnsureBeamMesh();
        MeshRenderer renderer = beamObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        GreatLampBeam beam = beamObject.AddComponent<GreatLampBeam>();
        SerializedObject data = new SerializedObject(beam);
        data.FindProperty("beamRenderer").objectReferenceValue = renderer;
        data.ApplyModifiedPropertiesWithoutUndo();
        return beam;
    }

    // Island 5 scenes saved before the islet was removed with the tower keep an islet that has no lighthouse. The lighthouse's islet is the one
    // with a single pine (a Pine and a PineTip child; the others have two or three pines), under the Horizon root. Nothing is found once it is gone.
    static bool RemoveEmptyLighthouseIslet(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Horizon")
            {
                continue;
            }

            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name.StartsWith("Islet") && child.childCount == 2 && child.Find(HouseName) == null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    return true;
                }
            }
        }

        return false;
    }

    static float Scale()
    {
        return TargetHeight / ModelHeight;
    }

    static Transform FindHouse(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == HouseName && t.parent != null && t.parent.name.StartsWith("Islet"))
                {
                    return t;
                }
            }
        }

        return null;
    }
}
}
