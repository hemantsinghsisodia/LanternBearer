using UnityEngine;

namespace LanternKeeper
{
/// <summary>
/// The floating joystick's maths and state, free of Unity UI so EditMode tests can drive it. Offsets are in reference
/// pixels (the overlay canvas's 1920x1080 space), positive y up. The overlay feeds it the finger and reads Output.
/// </summary>
public sealed class VirtualStick
{
    public const float Radius = 110f;
    public const float DeadZone = 0.12f;
    public const float AutoSprintFraction = 0.9f;
    public const float AutoSprintSeconds = 0.4f;

    Vector2 origin;
    Vector2 offset;
    float pushedSeconds;

    public bool Active { get; private set; }

    /// <summary>Where the finger first touched down (the base of the stick).</summary>
    public Vector2 Origin => origin;

    /// <summary>Finger offset from the origin, clamped to Radius (the knob position).</summary>
    public Vector2 KnobOffset => ClampOffset(offset, Radius);

    /// <summary>Movement vector for ExternalMove: zero inside the dead zone, magnitude 0..1 outside it.</summary>
    public Vector2 Output => Active ? Evaluate(offset, Radius, DeadZone) : Vector2.zero;

    public bool AutoSprint => Active && pushedSeconds >= AutoSprintSeconds;

    public void Down(Vector2 position)
    {
        Active = true;
        origin = position;
        offset = Vector2.zero;
        pushedSeconds = 0f;
    }

    public void Drag(Vector2 position)
    {
        if (Active)
        {
            offset = position - origin;
        }
    }

    /// <summary>Advances the auto-sprint timer. Call once per frame with the frame's delta time.</summary>
    public void Tick(float deltaTime)
    {
        pushedSeconds = Active ? NextPushTimer(pushedSeconds, offset, Radius, deltaTime) : 0f;
    }

    public void Up()
    {
        Active = false;
        offset = Vector2.zero;
        pushedSeconds = 0f;
    }

    public static Vector2 ClampOffset(Vector2 offset, float radius)
    {
        return Vector2.ClampMagnitude(offset, radius);
    }

    /// <summary>Raw push 0..1: offset length over the radius, clamped. No dead zone.</summary>
    public static float Push(Vector2 offset, float radius)
    {
        return radius <= 0f ? 0f : Mathf.Min(offset.magnitude / radius, 1f);
    }

    /// <summary>Dead zone and radius clamp. Below the dead zone the result is zero; above it the magnitude is rescaled so it ramps from 0 to 1.</summary>
    public static Vector2 Evaluate(Vector2 offset, float radius, float deadZone)
    {
        float push = Push(offset, radius);
        if (push < deadZone || offset.sqrMagnitude < 1e-8f)
        {
            return Vector2.zero;
        }

        float scaled = deadZone >= 1f ? 1f : (push - deadZone) / (1f - deadZone);
        return offset.normalized * Mathf.Clamp01(scaled);
    }

    /// <summary>Seconds the stick has been held past AutoSprintFraction: grows while pushed that far, resets otherwise.</summary>
    public static float NextPushTimer(float timer, Vector2 offset, float radius, float deltaTime)
    {
        return Push(offset, radius) >= AutoSprintFraction ? timer + deltaTime : 0f;
    }
}
}
