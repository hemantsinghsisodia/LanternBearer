using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Whether the player is currently using touch. Starts from the device type, then follows the last input used:
/// any touch turns it on, a key press or a real mouse move turns it off.
/// </summary>
public static class TouchMode
{
    /// <summary>A mouse move this soon after a touch is treated as the browser's emulated mouse event.</summary>
    public const float EmulatedMouseWindow = 0.5f;

    /// <summary>Mouse delta magnitude (pixels) that counts as a real move.</summary>
    public const float MouseMoveThreshold = 0.5f;

    public static bool Active { get; private set; }

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Active = Initial(Application.isMobilePlatform, BrowserSaysTouch());

        if (GameObject.Find("TouchModeTracker") != null)
        {
            return;
        }

        GameObject host = new GameObject("TouchModeTracker");
        host.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<TouchModeTracker>();
    }

    sealed class TouchModeTracker : MonoBehaviour
    {
        float lastTouchTime = -100f;

        void Update()
        {
            bool touched = false;
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
            {
                var touches = screen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.wasPressedThisFrame)
                    {
                        touched = true;
                        break;
                    }
                }
            }

            if (touched)
            {
                lastTouchTime = Time.unscaledTime;
            }

            bool key = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                key = keyboard.anyKey.wasPressedThisFrame;
            }

            bool mouseMoved = false;
            Mouse mouse = Mouse.current;
            if (mouse != null && Time.unscaledTime - lastTouchTime > EmulatedMouseWindow)
            {
                mouseMoved = mouse.delta.ReadValue().magnitude > MouseMoveThreshold;
            }

            Set(Next(Active, touched, key, mouseMoved));
        }
    }
}
