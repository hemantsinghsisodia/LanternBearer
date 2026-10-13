using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LanternKeeper
{
// Copies the repo's CREDITS.txt to Assets/Game/Resources/Credits.txt, the file the credits roll reads at run time. FinaleInstaller runs it;
// CreditsSyncTests fails if the copy is stale. The file is only rewritten when the text differs (ignoring line endings).
public static class CreditsSync
{
    public const string ResourcePath = "Assets/Game/Resources/Credits.txt";

    public static string SourcePath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "CREDITS.txt")); }
    }

    [MenuItem("Lantern Keeper/Sync Credits")]
    public static void Sync()
    {
        string source = SourcePath;
        if (!File.Exists(source))
        {
            Debug.LogError("CreditsSync: CREDITS.txt not found at " + source);
            return;
        }

        string text = File.ReadAllText(source);
        string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ResourcePath));
        if (File.Exists(target) && Normalise(File.ReadAllText(target)) == Normalise(text))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.WriteAllText(target, text);
        AssetDatabase.ImportAsset(ResourcePath, ImportAssetOptions.ForceUpdate);
        Debug.Log("CreditsSync: wrote " + ResourcePath);
    }

    static string Normalise(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}

// A player build never ships a stale Resources/Credits.txt: the copy is refreshed first.
public class CreditsBuildSync : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        CreditsSync.Sync();
    }
}
}
