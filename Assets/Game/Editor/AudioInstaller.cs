using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Adds the MusicDirector to each island's Systems object (and AmbienceExtras to Island2) and fills the MusicLibrary. Idempotent.
// The scenes are edited as text (two inserted blocks) because a normal scene save reorders children and makes noise.
public static class AudioInstaller
{
    const string LibraryPath = "Assets/Game/Resources/MusicLibrary.asset";
    const string MusicFolder = "Assets/Game/Audio/Music/";
    const string DirectorScriptPath = "Assets/Game/Scripts/MusicDirector.cs";
    const string ExtrasScriptPath = "Assets/Game/Scripts/AmbienceExtras.cs";
    const string AudioManagerScriptPath = "Assets/Game/Scripts/AudioManager.cs";
    static readonly string[] IslandNames = { "Island1", "Island2", "Island3", "Island4", "Island5" };

    [MenuItem("Lantern Keeper/Install Music Director On Islands")]
    public static void InstallIslands()
    {
        string directorGuid = AssetDatabase.AssetPathToGUID(DirectorScriptPath);
        string managerGuid = AssetDatabase.AssetPathToGUID(AudioManagerScriptPath);
        string extrasGuid = AssetDatabase.AssetPathToGUID(ExtrasScriptPath);
        for (int i = 0; i < CreatureInstaller.IslandScenePaths.Length; i++)
        {
            string path = CreatureInstaller.IslandScenePaths[i];
            // The scene files are edited as text, so a scene that is open in the editor would raise Unity's "modified externally"
            // dialog and could lose edits. Skip it; run the installer again after closing it.
            if (IsSceneLoaded(path))
            {
                Debug.LogWarning("AudioInstaller: skipped " + path + " because it is loaded in the editor. Open another scene and run it again.");
                continue;
            }

            string text = File.ReadAllText(path, new UTF8Encoding(false));
            string result = Insert(text, directorGuid, "MusicDirector", managerGuid, path);
            if (path.EndsWith("Island2.unity", System.StringComparison.Ordinal))
            {
                result = Insert(result, extrasGuid, "AmbienceExtras", managerGuid, path);
            }

            if (result != text)
            {
                File.WriteAllText(path, result, new UTF8Encoding(false));
                Debug.Log("AudioInstaller: added MusicDirector to " + path);
            }
        }

        InstallLibrary();
        AssetDatabase.Refresh();
    }

    // Island 5's step in IslandBuilder.Island5PostSteps. The build leaves the Island 5 scene open, and InstallIslands skips loaded
    // scenes (it edits the file as text), so this adds the MusicDirector through EditorSceneManager instead. Idempotent.
    public static void InstallIsland5()
    {
        const string scenePath = "Assets/Game/Scenes/Island5.unity";
        UnityEngine.SceneManagement.Scene scene = default(UnityEngine.SceneManagement.Scene);
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene loaded = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == scenePath)
            {
                scene = loaded;
                break;
            }
        }

        if (!scene.IsValid())
        {
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        AudioManager manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            manager = root.GetComponentInChildren<AudioManager>(true);
            if (manager != null)
            {
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogWarning("AudioInstaller: no AudioManager in " + scenePath);
        }
        else if (manager.GetComponent<MusicDirector>() == null)
        {
            manager.gameObject.AddComponent<MusicDirector>();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("AudioInstaller: added MusicDirector to " + scenePath);
        }

        InstallLibrary();
    }

    static bool IsSceneLoaded(string path)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isLoaded && string.Equals(scene.path, path, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Fills any empty island slot from Music_<Island>_1.ogg and Tension_<Island>_1.ogg in the music folder.
    public static void InstallLibrary()
    {
        MusicLibrary library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(LibraryPath);
        if (library == null)
        {
            Debug.LogError("AudioInstaller: missing MusicLibrary.");
            return;
        }

        bool changed = false;
        if (library.islands == null)
        {
            library.islands = new MusicLibrary.IslandMusic[IslandNames.Length];
            changed = true;
        }
        else if (library.islands.Length != IslandNames.Length)
        {
            // Grow or shrink in place so the entries already filled for earlier islands are kept.
            System.Array.Resize(ref library.islands, IslandNames.Length);
            changed = true;
        }

        for (int i = 0; i < IslandNames.Length; i++)
        {
            if (library.islands[i] == null)
            {
                library.islands[i] = new MusicLibrary.IslandMusic();
                changed = true;
            }

            if (library.islands[i].scene != IslandNames[i])
            {
                library.islands[i].scene = IslandNames[i];
                changed = true;
            }

            if (library.islands[i].track == null)
            {
                library.islands[i].track = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Music_" + IslandNames[i] + "_1.ogg");
                changed = true;
            }

            if (library.islands[i].tension == null)
            {
                AudioClip tension = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Tension_" + IslandNames[i] + "_1.ogg");
                if (tension != null)
                {
                    library.islands[i].tension = tension;
                    changed = true;
                }
            }

            // Island 5 only has a summit hush (Hush_Island5_1.ogg); the other islands have no such file, so their slot stays empty.
            if (library.islands[i].hush == null)
            {
                AudioClip hush = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Hush_" + IslandNames[i] + "_1.ogg");
                if (hush != null)
                {
                    library.islands[i].hush = hush;
                    changed = true;
                }
            }
        }

        // Fallback only: Island 5 has its own Music_Island5_1 / Tension_Island5_1 files, but if they are missing it reuses Island 4's clips.
        int island5 = System.Array.IndexOf(IslandNames, "Island5");
        int island4 = System.Array.IndexOf(IslandNames, "Island4");
        if (island5 > 0 && island4 >= 0)
        {
            if (library.islands[island5].track == null && library.islands[island4].track != null)
            {
                library.islands[island5].track = library.islands[island4].track;
                changed = true;
            }

            if (library.islands[island5].tension == null && library.islands[island4].tension != null)
            {
                library.islands[island5].tension = library.islands[island4].tension;
                changed = true;
            }
        }

        // Finale cue for the Island 5 ending (Finale_1.ogg); falls back to the menu track if the file is missing.
        AudioClip realFinale = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Finale_1.ogg");
        AudioClip menuTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Music_Menu_1.ogg");
        // A stale fallback (the menu track) is upgraded once the real cue exists.
        if (library.finaleCue == null || (realFinale != null && library.finaleCue == menuTrack && library.finaleCue != realFinale))
        {
            AudioClip finale = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Finale_1.ogg");
            if (finale == null)
            {
                finale = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "Music_Menu_1.ogg");
            }

            if (finale != null)
            {
                library.finaleCue = finale;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }

    static string Insert(string text, string scriptGuid, string className, string managerGuid, string path)
    {
        if (text.Contains("guid: " + scriptGuid))
        {
            return text;
        }

        string original = text;
        string managerId = null;
        string gameObjectId = null;
        int managerStart = -1;
        MatchCollection docs = Regex.Matches(text, @"^--- !u!114 &(\d+)\n", RegexOptions.Multiline);
        foreach (Match doc in docs)
        {
            int end = text.IndexOf("\n--- ", doc.Index + 1, System.StringComparison.Ordinal);
            string body = text.Substring(doc.Index, (end < 0 ? text.Length : end) - doc.Index);
            if (body.Contains("guid: " + managerGuid + ","))
            {
                Match go = Regex.Match(body, @"m_GameObject: \{fileID: (\d+)\}");
                managerId = doc.Groups[1].Value;
                gameObjectId = go.Groups[1].Value;
                managerStart = doc.Index;
                break;
            }
        }

        if (managerId == null)
        {
            Debug.LogWarning("AudioInstaller: no AudioManager in " + path);
            return text;
        }

        long max = 0;
        foreach (Match anchor in Regex.Matches(text, @"^--- !u!\d+ &(\d+)", RegexOptions.Multiline))
        {
            long id;
            if (long.TryParse(anchor.Groups[1].Value, out id) && id > max && id < 4000000000000000000L)
            {
                max = id;
            }
        }

        string newId = (max + 1).ToString();
        string block = "--- !u!114 &" + newId + "\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            + "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: " + gameObjectId + "}\n"
            + "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}\n"
            + "  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::LanternKeeper." + className + "\n";

        // New block goes right after the AudioManager block (before the next document).
        int next = text.IndexOf("\n--- !u!", managerStart + 1, System.StringComparison.Ordinal);
        next = next < 0 ? text.Length : next + 1;
        text = text.Substring(0, next) + block + text.Substring(next);

        // Register the component on the GameObject, after the AudioManager's entry.
        string entry = "  - component: {fileID: " + managerId + "}\n";
        int goStart = text.IndexOf("--- !u!1 &" + gameObjectId + "\n", System.StringComparison.Ordinal);
        int entryAt = goStart < 0 ? -1 : text.IndexOf(entry, goStart, System.StringComparison.Ordinal);
        if (entryAt < 0)
        {
            Debug.LogWarning("AudioInstaller: could not register component in " + path);
            return original;
        }

        entryAt += entry.Length;
        return text.Substring(0, entryAt) + "  - component: {fileID: " + newId + "}\n" + text.Substring(entryAt);
    }
}
}
