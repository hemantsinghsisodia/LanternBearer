using UnityEngine;

namespace LanternKeeper
{
// Pure steering memory for a Shade: which side of an obstacle it is going round, the stuck timer and the smoothed heading.
// Shade does the physics queries and feeds the results in; this class only decides.
public class ShadeSteering
{
    public const float MaxTurnDegreesPerSecond = 300f;
    public const float SideClearSeconds = 0.4f;
    public const float SideFlipCooldown = 0.5f;
    public const float StuckWindowSeconds = 1.5f;
    public const float StuckDistance = 0.3f;
    public const float EscapeSeconds = 1f;
    public const float EscapeDegrees = 75f;

    Vector3 heading = Vector3.forward;
    Vector3 escapeDirection = Vector3.forward;
    Vector3 windowStart;
    bool windowValid;
    float windowTimer;
    float escapeTimer;
    float sinceHit;
    float flipCooldown;
    int side;

    // Unit flat direction the Shade is actually moving along.
    public Vector3 Heading => heading;
    // 0 = no obstacle being rounded, +1 / -1 = the remembered side.
    public int Side => side;
    public bool Escaping => escapeTimer > 0f;
    public Vector3 EscapeDirection => escapeDirection;

    public void Reset(Vector3 forward)
    {
        heading = Flat(forward, Vector3.forward);
        side = 0;
        sinceHit = 0f;
        flipCooldown = 0f;
        escapeTimer = 0f;
        windowValid = false;
        windowTimer = 0f;
    }

    // The direction along the wall on the current side. The first call for an obstacle picks the side that
    // is closest to the way the Shade was already heading. The side is then kept until the obstacle is cleared.
    public Vector3 ProbeDirection(Vector3 flat, Vector3 wallNormal)
    {
        Vector3 tangent = Tangent(wallNormal, flat);
        if (side == 0)
        {
            side = Vector3.Dot(tangent, flat) >= 0f ? 1 : -1;
        }

        return tangent * side;
    }

    // Steers round a wall. sideBlocked says the remembered side is blocked too, which is the only reason to switch.
    public Vector3 Avoid(Vector3 flat, Vector3 wallNormal, bool sideBlocked, float dt)
    {
        ProbeDirection(flat, wallNormal);
        sinceHit = 0f;
        if (flipCooldown > 0f)
        {
            flipCooldown -= dt;
        }
        else if (sideBlocked && dt > 0f)
        {
            side = -side;
            flipCooldown = SideFlipCooldown;
        }

        Vector3 away = new Vector3(wallNormal.x, 0f, wallNormal.z);
        return Tangent(wallNormal, flat) * side + away * 0.35f;
    }

    // No obstacle this frame. After a short while the remembered side is forgotten.
    public void NoHit(float dt)
    {
        if (side == 0)
        {
            return;
        }

        sinceHit += dt;
        if (flipCooldown > 0f)
        {
            flipCooldown -= dt;
        }

        if (sinceHit >= SideClearSeconds)
        {
            side = 0;
        }
    }

    // Call once a frame while the Shade is meant to be travelling. Returns true on the frame it decides it is stuck.
    public bool TickStuck(Vector3 position, float dt, bool eligible)
    {
        if (dt <= 0f)
        {
            return false;
        }

        if (escapeTimer > 0f)
        {
            escapeTimer -= dt;
            windowValid = false;
            return false;
        }

        if (!eligible)
        {
            windowValid = false;
            return false;
        }

        if (!windowValid)
        {
            windowValid = true;
            windowStart = position;
            windowTimer = 0f;
            return false;
        }

        windowTimer += dt;
        if (windowTimer < StuckWindowSeconds)
        {
            return false;
        }

        Vector3 moved = position - windowStart;
        moved.y = 0f;
        windowStart = position;
        windowTimer = 0f;
        return moved.magnitude < StuckDistance;
    }

    // Heads off to one side of the blocked direction for EscapeSeconds. Prefers the walkable side.
    public void BeginEscape(Vector3 blockedDirection, bool leftWalkable, bool rightWalkable)
    {
        Vector3 flat = Flat(blockedDirection, heading);
        float sign;
        if (leftWalkable != rightWalkable)
        {
            sign = rightWalkable ? 1f : -1f;
        }
        else
        {
            sign = side != 0 ? -side : 1f;
        }

        escapeDirection = Quaternion.AngleAxis(EscapeDegrees * sign, Vector3.up) * flat;
        escapeTimer = EscapeSeconds;
        side = 0;
        windowValid = false;
    }

    // Turns the heading toward desired at no more than the max turn rate and returns it.
    public Vector3 Turn(Vector3 desired, float dt)
    {
        Vector3 target = Flat(desired, heading);
        if (dt <= 0f)
        {
            return heading;
        }

        float angle = Vector3.SignedAngle(heading, target, Vector3.up);
        float step = MaxTurnDegreesPerSecond * dt;
        float turn = Mathf.Clamp(angle, -step, step);
        heading = Flat(Quaternion.AngleAxis(turn, Vector3.up) * heading, heading);
        return heading;
    }

    static Vector3 Tangent(Vector3 wallNormal, Vector3 flat)
    {
        Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal);
        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.01f)
        {
            tangent = Vector3.Cross(Vector3.up, flat);
        }

        return tangent.normalized;
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f)
        {
            fallback.y = 0f;
            return fallback.sqrMagnitude < 0.0001f ? Vector3.forward : fallback.normalized;
        }

        return v.normalized;
    }
}
}
