using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single place for the button-style actions that keyboard and touch share. Touch presses are stamped with the frame they
/// happened in: every reader in that frame sees the press, and it is gone the next frame (no consume-once, so script
/// order between readers does not matter). The touch overlay must therefore set presses before the readers run.
/// </summary>
public static class GameInput
{
    /// <summary>Frame counter source. Replaced by tests.</summary>
    public static Func<int> FrameSource = DefaultFrame;

    const int None = int.MinValue;

    static int interactFrame = None;
    static int pauseFrame = None;
    static int jumpFrame = None;
    static bool touchSprint;

    static int DefaultFrame()
    {
        return Time.frameCount;
    }

    public static void PressInteract()
    {
        interactFrame = FrameSource();
    }

    public static void PressPause()
    {
        pauseFrame = FrameSource();
    }

    public static void PressJump()
    {
        jumpFrame = FrameSource();
    }

    public static void SetTouchSprint(bool held)
    {
        touchSprint = held;
    }

    public static bool InteractPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return interactFrame == FrameSource();
    }

    public static bool PausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
        {
            return true;
        }

        return pauseFrame == FrameSource();
    }

    public static bool JumpPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        return jumpFrame == FrameSource();
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

    /// <summary>Test hook: clears all touch state and restores the real frame source.</summary>
    public static void ResetForTests()
    {
        FrameSource = DefaultFrame;
        interactFrame = None;
        pauseFrame = None;
        jumpFrame = None;
        touchSprint = false;
    }
}
