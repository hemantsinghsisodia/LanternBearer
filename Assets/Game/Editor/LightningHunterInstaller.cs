using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Installs Island 5's hunting lightning and scarce fuel (BuildIsland5 runs it after the storm surge, before the audio):
//  - the material Assets/Game/Art/Strike/StrikeWarning.mat (shader LanternKeeper/StrikeWarning);
//  - the "LightningHunter" object under the "Rings" root, switched off, wired into RingHazards (which switches it on in the Mid ring and above);
//  - the "LevelTuning" object (beacon refund and lantern drain multiplier, copied from Island5.asset);
//  - half the firefly swarms: every second pickup under "Fireflies" (by sibling order) is switched off.
// Idempotent: the object is rebuilt, the swarms are all switched on first and then every second one off. The scene is edited through
// EditorSceneManager, never as text.
public static class LightningHunterInstaller
{
    public const string Folder = "Assets/Game/Art/Strike";
    public const string RootName = "LightningHunter";
    public const string MaterialPath = Folder + "/StrikeWarning.mat";
    public const string LevelPath = "Assets/Game/Levels/Island5.asset";
    public const string TuningName = "LevelTuning";
    const string RingsName = "Rings";
    const string FirefliesName = "Fireflies";

    [MenuItem("Lantern Keeper/Install Hunting Lightning (Island 5)")]
    public static void InstallMenu()
    {
        Install();
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        if (Rebuild(scene))
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("LightningHunterInstaller: installed the hunting lightning, the beacon refund and half the swarms in " + scene.name + ".");
        }
    }

    // Edits an open Island 5 scene without saving it (the material is written). False, and the scene untouched, if something is missing.
    public static bool Rebuild(Scene scene)
    {
        Shader shader = Shader.Find("LanternKeeper/StrikeWarning");
        GameObject rings = null;
        GameObject fireflies = null;
        GameObject beacons = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RingsName)
            {
                rings = root;
            }
            else if (root.name == FirefliesName)
            {
                fireflies = root;
            }
            else if (root.name == "Beacons")
            {
                beacons = root;
            }
        }

        RingHazards hazards = rings != null ? rings.GetComponentInChildren<RingHazards>(true) : null;
        LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
        if (shader == null || hazards == null || fireflies == null || beacons == null || config == null)
        {
            Debug.LogError("LightningHunterInstaller: Island 5 lacks the StrikeWarning shader, the Rings (RingHazards), the Fireflies, the Beacons or Island5.asset. Run the earlier post steps first.");
            return false;
        }

        EnsureFolder();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        material.name = "StrikeWarning";
        material.SetColor("_Color", new Color(0.737f, 0.824f, 1f, 1f));
        material.SetFloat("_Progress", 0f);
        material.SetFloat("_Pulse", 1f);
        material.SetFloat("_Alpha", 1f);
        EditorUtility.SetDirty(material);

        Transform old = rings.transform.Find(RootName);
        while (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
            old = rings.transform.Find(RootName);
        }

        GameObject host = new GameObject(RootName);
        host.transform.SetParent(rings.transform, false);
        host.SetActive(false);
        LightningHunter hunter = host.AddComponent<LightningHunter>();
        SerializedObject hunterData = new SerializedObject(hunter);
        hunterData.FindProperty("warningMaterial").objectReferenceValue = material;
        hunterData.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject hazardData = new SerializedObject(hazards);
        hazardData.FindProperty("hunter").objectReferenceValue = host;
        hazardData.ApplyModifiedPropertiesWithoutUndo();

        // The beacon refund and the lantern drain come from the level config, the single source, through one scene object.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == TuningName)
            {
                Object.DestroyImmediate(root);
            }
        }

        GameObject tuningObject = new GameObject(TuningName);
        SceneManager.MoveGameObjectToScene(tuningObject, scene);
        LevelTuning tuning = tuningObject.AddComponent<LevelTuning>();
        SerializedObject tuningData = new SerializedObject(tuning);
        tuningData.FindProperty("beaconRefund01").floatValue = config.beaconRefund01;
        tuningData.FindProperty("drainMultiplier").floatValue = config.drainMultiplier;
        tuningData.ApplyModifiedPropertiesWithoutUndo();

        HalveSwarms(fireflies.transform);
        return true;
    }

    // Every second child of "Fireflies" (by sibling order: the 2nd, 4th, ...) is switched off, the others on.
    public static int HalveSwarms(Transform fireflies)
    {
        int off = 0;
        for (int i = 0; i < fireflies.childCount; i++)
        {
            bool keep = i % 2 == 0;
            fireflies.GetChild(i).gameObject.SetActive(keep);
            if (!keep)
            {
                off++;
            }
        }

        return off;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Game/Art", "Strike");
        }
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
