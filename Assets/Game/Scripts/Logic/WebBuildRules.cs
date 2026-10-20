namespace LanternKeeper
{
// Rules for producing the browser (WebGL) player. Pure, so EditMode tests can cover it.
public static class WebBuildRules
{
    // A WebGL player must be built while WebGL is already the Editor's active build target. If BuildPlayer is called while
    // another target (Windows) is active, Unity switches target inside the build call and URP's shader-variant stripping
    // then runs against the wrong platform's quality levels and pipeline assets. The player gets built without the
    // Forward lit variants the web renderer needs: the terrain, trees, rocks and keeper vanish into the fog colour and
    // some objects turn magenta. The build itself reports no error.
    public static bool IsSafeToBuild(bool buildingWebGL, bool webGLIsActiveTarget)
    {
        return !buildingWebGL || webGLIsActiveTarget;
    }

    public const string UnsafeMessage =
        "Lantern Keeper: switch the Editor to the WebGL platform first (File > Build Profiles > Web > Switch Platform, " +
        "or Lantern Keeper/Web/Switch To WebGL) and wait for it to finish, then build. Building WebGL while another " +
        "platform is active produces a player whose lit surfaces are invisible (wrong shader stripping).";
}
}
