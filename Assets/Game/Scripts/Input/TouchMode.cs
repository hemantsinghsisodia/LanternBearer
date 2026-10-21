using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace LanternKeeper
{
/// <summary>
/// Whether the player is currently using touch. Starts from the device type, then follows the last input used:
/// any touch turns it on, a key press or a real mouse move turns it off.
/// </summary>
public static class TouchMode
{
    /// <summary>A mouse move this soon after a touch (or while one is held) is treated as the browser's emulated mouse event.</summary>
    public const float EmulatedMouseWindow = 0.5f;

    /// <summary>Mouse delta magnitude (pixels) that counts as a real move.</summary>
    public const float MouseMoveThreshold = 0.5f;

    public static bool Active { get; private set; }

    /// <summary>Whether the device looked like a touch device at startup (mobile platform or touch browser). Fixed for the session.</summary>
    public static bool StartedAsTouch { get; private set; }

    public static event Action OnChanged;

    public static bool Initial(bool isMobilePlatform, bool browserSaysTouch)
    {
        return isMobilePlatform || browserSaysTouch;
    }

    public static bool Next(bool current, bool touchedThisFrame, bool keyPressedThisFrame, bool mouseMovedWithoutRecentTouch)
    {
        if (touchedThisFrame)
        {
            return true;
        }

        if (keyPressedThisFrame || mouseMovedWithoutRecentTouch)
        {
            return false;
        }

        return current;
    }

    /// <summary>
    /// The last-touch time after this frame: now while a finger is down or went down, otherwise unchanged
    /// (so the guard window runs from the moment the last finger lifted).
    /// </summary>
    public static float UpdateLastTouchTime(float lastTouchTime, float now, bool touchDownThisFrame, bool anyTouchInProgress)
    {
        return (touchDownThisFrame || anyTouchInProgress) ? now : lastTouchTime;
    }

    /// <summary>True for a real mouse move: big enough and outside the emulated-mouse window after the last touch.</summary>
    public static bool MouseMovedWithoutRecentTouch(float mouseDeltaMagnitude, float now, float lastTouchTime)
    {
        return mouseDeltaMagnitude > MouseMoveThreshold && now - lastTouchTime > EmulatedMouseWindow;
    }

    static void Set(bool value)
    {
        if (Active == value)
        {
            return;
        }

        Active = value;
        Action handler = OnChanged;
        if (handler != null)
        {
            handler();
        }
    }

    /// <summary>Test hook: forces the mode (fires OnChanged on change).</summary>
    public static void SetActiveForTests(bool value)
    {
        Set(value);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern int LK_IsTouchDevice();
#endif

    static bool BrowserSaysTouch()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return LK_IsTouchDevice() != 0;
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
    }

    static TouchModeTracker tracker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        StartedAsTouch = Initial(Application.isMobilePlatform, BrowserSaysTouch());
        Set(StartedAsTouch);

        // Domain reload may be disabled, so a tracker from an earlier play session can still be referenced here.
        if (tracker != null)
        {
            UnityEngine.Object.Destroy(tracker.gameObject);
            tracker = null;
        }

        GameObject host = new GameObject("TouchModeTracker");
        host.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(host);
        tracker = host.AddComponent<TouchModeTracker>();
    }

    sealed class TouchModeTracker : MonoBehaviour
    {
        float lastTouchTime = -100f;

        void Update()
        {
            bool touchDown = false;
            bool inProgress = false;
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
            {
                var touches = screen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.press.wasPressedThisFrame)
                    {
                        touchDown = true;
                    }

                    if (touch.isInProgress)
                    {
                        inProgress = true;
                    }
                }
            }

            float now = Time.unscaledTime;
            lastTouchTime = UpdateLastTouchTime(lastTouchTime, now, touchDown, inProgress);

            bool key = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                key = keyboard.anyKey.wasPressedThisFrame;
            }

            bool mouseMoved = false;
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                mouseMoved = MouseMovedWithoutRecentTouch(mouse.delta.ReadValue().magnitude, now, lastTouchTime);
            }

            Set(Next(Active, touchDown, key, mouseMoved));
        }
    }
}
}
