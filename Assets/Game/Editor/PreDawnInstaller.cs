using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Sets DawnSequence.reach on Islands 1-4 (PreDawn.ReachFor) through EditorSceneManager. Island 5 keeps 1. Nothing else in the scenes is touched;
// IslandBuilder.CreateGameplay sets the same value on a rebuild.
public static class PreDawnInstaller
{
    public static readonly string[] SceneNames = { "Island1", "Island2", "Island3", "Island4" };

    [MenuItem("Lantern Keeper/Install Pre-Dawn (Islands 1-4)")]
    public static void InstallMenu()
    {
        Install();
    }

    public static string LevelIdFor(int island)
    {
        return "island" + island;
    }

    public static void Install()
    {
        for (int i = 0; i < SceneNames.Length; i++)
        {
            string path = "Assets/Game/Scenes/" + SceneNames[i] + ".unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Apply(scene, LevelIdFor(i + 1)))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log("PreDawnInstaller: reach set on Islands 1-4.");
    }

    // Sets the scene's DawnSequence.reach for levelId. True if it changed (the scene is marked dirty, not saved).
    public static bool Apply(Scene scene, string levelId)
    {
        float reach = PreDawn.ReachFor(levelId);
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DawnSequence[] dawns = root.GetComponentsInChildren<DawnSequence>(true);
            for (int i = 0; i < dawns.Length; i++)
            {
                SerializedObject data = new SerializedObject(dawns[i]);
                SerializedProperty property = data.FindProperty("reach");
                if (!Mathf.Approximately(property.floatValue, reach))
                {
                    property.floatValue = reach;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    changed = true;
                }
            }
        }

        return changed;
    }
}
}
