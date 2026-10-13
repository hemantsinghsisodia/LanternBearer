using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
// Installs Island 5's rising storm surge on the built scene (BuildIsland5 runs it after the finale, before the audio):
//  - the material Assets/Game/Art/Surge/StormSurge.mat (shader LanternKeeper/StormSurge, the finale's StormCloud.png noise);
//  - the "StormSurge" object (the StormSurge component; it builds its disc at runtime) at the scene root;
//  - the "SurgeChip" in the Island 5 HUD: a copy of the wind chip's backing and word, one row under it, wired for SurgeFeed.
// Idempotent: the object and the chip are rebuilt each run. The scene is edited through EditorSceneManager, never as text.
public static class StormSurgeInstaller
{
    public const string Folder = "Assets/Game/Art/Surge";
    public const string RootName = "StormSurge";
    public const string ChipName = "SurgeChip";
    public const string MaterialPath = Folder + "/StormSurge.mat";
    const string CloudTexturePath = FinaleInstaller.Folder + "/StormCloud.png";
    // UIBuilder.HudChipRowHeight + HudRowGap: the chip sits one row under the wind / tide chip.
    const float ChipRowStep = 44f + 8f;

    [MenuItem("Lantern Keeper/Install Storm Surge (Island 5)")]
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
            Debug.Log("StormSurgeInstaller: installed the storm surge in " + scene.name + ".");
        }
    }

    // Rebuilds the surge in an open Island 5 scene without saving it (the material is written). False, and the scene untouched, if something is missing.
    public static bool Rebuild(Scene scene)
    {
        Shader shader = Shader.Find("LanternKeeper/StormSurge");
        Texture2D cloud = AssetDatabase.LoadAssetAtPath<Texture2D>(CloudTexturePath);
        GameObject lampObject = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == LighthouseInstaller.LampName)
            {
                lampObject = root;
            }
        }

        HUD hud = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (hud == null)
            {
                hud = root.GetComponentInChildren<HUD>(true);
            }
        }

        StormChip windChip = hud != null ? hud.GetComponentInChildren<StormChip>(true) : null;
        if (shader == null || cloud == null || lampObject == null || windChip == null)
        {
            Debug.LogError("StormSurgeInstaller: Island 5 lacks the StormSurge shader, the finale's StormCloud.png, the GreatLamp or the HUD wind chip. Run the earlier post steps first.");
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
        material.name = "StormSurge";
        material.SetTexture("_CloudTex", cloud);
        material.SetColor("_DeepColor", new Color(0.012f, 0.014f, 0.035f, 1f));
        material.SetColor("_DarkColor", new Color(0.065f, 0.07f, 0.15f, 1f));
        material.SetColor("_LitColor", new Color(0.55f, 0.62f, 1f, 1f));
        material.SetFloat("_Alpha", 1f);
        material.SetFloat("_Flash", 0f);
        material.SetFloat("_Detail", 1f);
        material.SetFloat("_Tiling", 0.018f);
        EditorUtility.SetDirty(material);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName)
            {
                Object.DestroyImmediate(root);
            }
        }

        GameObject surgeObject = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(surgeObject, scene);
        surgeObject.transform.position = new Vector3(lampObject.transform.position.x, 0f, lampObject.transform.position.z);
        StormSurge surge = surgeObject.AddComponent<StormSurge>();
        SerializedObject data = new SerializedObject(surge);
        data.FindProperty("material").objectReferenceValue = material;
        data.ApplyModifiedPropertiesWithoutUndo();

        BuildChip(windChip);
        return true;
    }

    // A copy of the wind chip without its gauge and thunder, one row lower.
    static void BuildChip(StormChip windChip)
    {
        Transform parent = windChip.transform.parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (parent.GetChild(i).name == ChipName)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        GameObject chip = Object.Instantiate(windChip.gameObject, parent);
        chip.name = ChipName;
        Object.DestroyImmediate(chip.GetComponent<StormChip>());
        ThemedLabel word = null;
        Image backing = null;
        for (int i = chip.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = chip.transform.GetChild(i);
            if (child.name == "Word")
            {
                word = child.GetComponent<ThemedLabel>();
            }
            else if (child.name == "Backing")
            {
                backing = child.GetComponent<Image>();
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        RectTransform rect = (RectTransform)chip.transform;
        rect.anchoredPosition = windChip.GetComponent<RectTransform>().anchoredPosition + new Vector2(0f, -ChipRowStep);
        SurgeChip surgeChip = chip.AddComponent<SurgeChip>();
        SerializedObject chipData = new SerializedObject(surgeChip);
        chipData.FindProperty("label").objectReferenceValue = word;
        chipData.FindProperty("backing").objectReferenceValue = backing;
        chipData.ApplyModifiedPropertiesWithoutUndo();
        if (word != null)
        {
            word.Text.text = "Storm rising: 25 m below";
        }

        chip.SetActive(false);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Game/Art", "Surge");
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
