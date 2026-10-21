using UnityEngine;

namespace LanternKeeper
{
// Per-platform defaults and capabilities that depend only on the platform. Pure, so EditMode tests can cover it.
// Levels are the GraphicsLevel values as ints (Logic cannot see that enum): 0 Low, 1 Medium, 2 High, 3 Ultra.
public static class GraphicsDefaults
{
    public const int LowLevel = 0;
    public const int MediumLevel = 1;
    public const int HighLevel = 2;

    // The preset used when the player has no saved choice: Low (the "Web Low" level) in a mobile browser, Medium everywhere else.
    // The web levels are indexed like the GraphicsLevel values (see GraphicsQuality.QualityName), so Low is 0 there too.
    public static int For(RuntimePlatform platform, bool isMobile)
    {
        return isMobile && IsWeb(platform) ? LowLevel : MediumLevel;
    }

    public static bool IsWeb(RuntimePlatform platform)
    {
        return platform == RuntimePlatform.WebGLPlayer;
    }

    // A browser tab cannot be quit by the game, so the Quit button and the Esc-to-quit path are desktop only.
    public static bool CanQuit(RuntimePlatform platform)
    {
        return !IsWeb(platform);
    }

    // Resolution and window mode belong to the browser (and the page's fullscreen button) on the web.
    public static bool HasDisplayModeOptions(RuntimePlatform platform)
    {
        return !IsWeb(platform);
    }
}
}
