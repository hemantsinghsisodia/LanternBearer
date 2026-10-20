using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace LanternKeeper
{
// Refuses to build a WebGL player unless WebGL is already the active Editor platform. See WebBuildRules.
public class WebBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return int.MinValue; }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        bool web = report.summary.platform == BuildTarget.WebGL;
        bool active = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
        if (!WebBuildRules.IsSafeToBuild(web, active))
        {
            throw new BuildFailedException(WebBuildRules.UnsafeMessage);
        }
    }

    [MenuItem("Lantern Keeper/Web/Switch To WebGL")]
    public static void SwitchToWebGL()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
    }
}
}
