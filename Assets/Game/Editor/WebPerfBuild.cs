using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace LanternKeeper
{
// Dev tool for the stutter measurement builds: builds the WebGL player with extra scripting defines (for example LK_WEBPROBE,
// the dev-only WebPerfProbe) without touching PlayerSettings, and writes the outcome to <outputDir>.result.txt.
// WebGL must already be the active platform (WebBuildGuard).
public static class WebPerfBuild
{
    // Builds immediately and blocks the editor until the build ends.
    public static void RunNow(string outputDir, bool development, string extraDefines)
    {
        string resultPath = outputDir.TrimEnd('/', (char)92) + ".result.txt";
        File.WriteAllText(resultPath, "running\n");
        try
        {
            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            options.locationPathName = outputDir;
            options.target = BuildTarget.WebGL;
            options.options = development ? BuildOptions.Development : BuildOptions.None;
            if (!string.IsNullOrEmpty(extraDefines))
            {
                options.extraScriptingDefines = extraDefines.Split(';');
            }

            BuildReport report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText(resultPath, report.summary.result + " bytes=" + report.summary.totalSize
                + " seconds=" + report.summary.totalTime.TotalSeconds.ToString("0") + " errors=" + report.summary.totalErrors + "\n");
        }
        catch (Exception e)
        {
            File.WriteAllText(resultPath, "EXCEPTION " + e + "\n");
        }
    }
}
}
