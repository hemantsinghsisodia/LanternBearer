namespace LanternKeeper
{
// Which shaders the WebGL pre-warm (WebShaderPrewarm) should draw once at scene start. Pure so it can be tested.
public static class WebPrewarmRules
{
    public static bool ShouldWarm(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName))
        {
            return false;
        }

        // Post-process and blit shaders are compiled by the pipeline itself; terrain shaders need terrain data and are drawn from the first frame.
        if (shaderName.StartsWith("Hidden/", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (shaderName.Contains("/Terrain/"))
        {
            return false;
        }

        return true;
    }
}
}
