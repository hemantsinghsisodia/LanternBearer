namespace LanternKeeper
{
// When the phone is held in portrait the web page covers the game with a "Rotate your phone" card. This decides whether the
// game should also be paused: only on an island, while playing. Pure, so EditMode tests can cover it.
public static class PortraitPause
{
    public static bool ShouldPause(bool portrait, bool onIsland, bool paused, bool introShowing, bool roundOver, bool dawnPlaying, bool dying)
    {
        return portrait && onIsland && !paused && !introShowing && !roundOver && !dawnPlaying && !dying;
    }
}
}
