using System;

namespace LanternKeeper
{
// UI code asks for a cue by name; the AudioManager (if present) plays it.
public static class UiSound
{
    public static event Action<string> Requested;

    public static void Play(string cue)
    {
        Action<string> handler = Requested;
        if (handler != null)
        {
            handler(cue);
        }
    }
}
}
