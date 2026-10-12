using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Island 5 performance pass, installed on top of the built scene (BuildIsland5 runs it after the bridges and rings).
// Profile (editor, Ultra, probe pose): the 11 cliff-skin chunks (155k triangles, one LOD, always inside the shadow range) were drawn
// into every shadow cascade, about 790k of the 2.27M triangles. The skin hugs the terrain's own cliff steps, so the terrain keeps casting
// the same shadow silhouette; the skin keeps receiving shadows. Only the skin's shadow casting is switched off.
// Idempotent, and it touches only Island 5's CliffSkin renderers. The scene is edited through EditorSceneManager, never as text.
public static class Island5PerfInstaller
{
    public const string ScenePath = "Assets/Game/Scenes/Island5.unity";

    [MenuItem("Lantern Keeper/Install Performance Pass On Island 5")]
    public static void InstallMenu()
    {
        Install();
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        int changed = 0;
        int skins = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Props")
            {
                continue;
            }

            Transform skin = root.transform.Find(IslandBuilder.CliffSkinName);
            if (skin == null)
            {
                continue;
            }

            Renderer[] renderers = skin.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                skins++;
                if (renderers[i].shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                    EditorUtility.SetDirty(renderers[i]);
                    changed++;
                }
            }
        }

        if (skins == 0)
        {
            Debug.LogError("Island5PerfInstaller: no CliffSkin renderers under Props. Build Island 5 first.");
            return;
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("Island5PerfInstaller: cliff skin shadow casting off on " + changed + " of " + skins + " chunks in " + scene.name + ".");
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
}
}
