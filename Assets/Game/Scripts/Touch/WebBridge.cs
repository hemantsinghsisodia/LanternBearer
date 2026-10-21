using UnityEngine;

namespace LanternKeeper
{
// A tiny object the web page can reach with unityInstance.SendMessage("LKWebBridge", "SetPortrait", 0 or 1).
// The page raises SetPortrait(1) while it shows the "Rotate your phone to landscape" card. On an island, while playing, that opens
// the pause screen through GameManager.Pause (the same path as the Esc key). Turning back to landscape does not resume: the player
// does that from the pause screen. If the phone is already in portrait when an island loads, the pause is applied on the first frame it can be.
public sealed class WebBridge : MonoBehaviour
{
    public const string ObjectName = "LKWebBridge";

    static WebBridge instance;

    bool portrait;
    bool applied;

    public bool Portrait { get { return portrait; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Application.isPlaying || instance != null)
        {
            return;
        }

        GameObject host = new GameObject(ObjectName);
        DontDestroyOnLoad(host);
        instance = host.AddComponent<WebBridge>();
    }

    // Called from JavaScript. SendMessage delivers a JS number as a float (an int parameter is not guaranteed), so this takes a float.
    public void SetPortrait(float value)
    {
        portrait = value > 0.5f;
        if (!portrait)
        {
            applied = false;
            return;
        }

        TryPause();
    }

    void Update()
    {
        if (portrait && !applied)
        {
            TryPause();
        }
    }

    void TryPause()
    {
        GameManager manager = GameManager.Instance;
        bool onIsland = manager != null;
        if (!PortraitPause.ShouldPause(portrait, onIsland, onIsland && manager.IsPaused, onIsland && manager.IntroShowing,
                onIsland && manager.IsRoundOver, onIsland && manager.DawnPlaying, onIsland && manager.IsDying))
        {
            return;
        }

        manager.Pause();
        applied = true;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
}
