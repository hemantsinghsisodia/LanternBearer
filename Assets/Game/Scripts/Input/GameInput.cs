using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
/// <summary>
/// Single place for the button-style actions that keyboard and touch share. A touch press stays pending until the first
/// reader looks at it; every reader in that frame sees it, and it is consumed on any later frame. The setter may run
/// before or after the readers (Update order, LateUpdate, callbacks). A press nobody reads within PressLifetime
/// seconds expires, so a tap made while nothing listens does not fire later.
/// </summary>
public static class GameInput
{
    public const float PressLifetime = 0.25f;

    /// <summary>Frame counter source. Replaced by tests.</summary>
    public static Func<int> FrameSource = DefaultFrame;

    /// <summary>Real-time clock source (seconds). Replaced by tests.</summary>
    public static Func<float> TimeSource = DefaultTime;

    const int None = int.MinValue;

    struct Press
    {
        public bool Pending;
        public int SeenFrame;
        public float SetTime;
    }

    static Press interact;
    static Press pause;
    static Press jump;
    static bool touchSprint;

    static int DefaultFrame()
    {
        return Time.frameCount;
    }

    static float DefaultTime()
    {
        return Time.realtimeSinceStartup;
    }

    static void Set(ref Press press)
    {
        press.Pending = true;
        press.SeenFrame = None;
        press.SetTime = TimeSource();
    }

    static bool Read(ref Press press)
    {
        if (!press.Pending)
        {
            return false;
        }

        int now = FrameSource();
        if (press.SeenFrame == None)
        {
            if (TimeSource() - press.SetTime > PressLifetime)
            {
                press = default(Press);
                return false;
            }

            press.SeenFrame = now;
        }

        if (now == press.SeenFrame)
        {
            return true;
        }

        press = default(Press);
        return false;
    }

    public static void PressInteract()
    {
        Set(ref interact);
    }

    public static void PressPause()
    {
        Set(ref pause);
    }

    public static void PressJump()
    {
        Set(ref jump);
    }

    /// <summary>True when any finger went down on the touchscreen this frame (dismisses cards, skips the credits).</summary>
    public static bool TouchPressedThisFrame()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null)
        {
            return false;
        }

        var touches = screen.touches;
        for (int i = 0; i < touches.Count; i++)
        {
            if (touches[i].press.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    public static void SetTouchSprint(bool held)
    {
        touchSprint = held;
    }

    public static bool InteractPressed()
    {
        Keyboard keyboard = Keyboard.current;
        bool key = keyboard != null && keyboard.eKey.wasPressedThisFrame;
        return Read(ref interact) || key;
    }

    public static bool PausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        bool key = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame);
        return Read(ref pause) || key;
    }

    public static bool JumpPressed()
    {
        Keyboard keyboard = Keyboard.current;
        bool key = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        return Read(ref jump) || key;
    }

    public static bool SprintHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
        {
            return true;
        }

        return touchSprint;
    }

    /// <summary>Test hook: clears all touch state and restores the real frame and time sources.</summary>
    public static void ResetForTests()
    {
        FrameSource = DefaultFrame;
        TimeSource = DefaultTime;
        interact = default(Press);
        pause = default(Press);
        jump = default(Press);
        touchSprint = false;
    }
}
}
