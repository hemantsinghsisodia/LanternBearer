using UnityEngine;

namespace LanternKeeper
{
// Timing of the Island 5 light bridges. The beam arc runs first, then the planks grow from both ends; the bridge is
// walkable only when the grow is complete.
public static class BridgeTimings
{
    public const float BeamSeconds = 1.0f;
    public const float GrowSeconds = 1.5f;
    public const float TotalSeconds = BeamSeconds + GrowSeconds;
    // The light columns that rise from each beacon before the summit span grows.
    public const float ColumnSeconds = 1.2f;

    // 0..1 position of the beam head along its arc, seconds after the bridge was triggered.
    public static float BeamHead(float since)
    {
        return Mathf.Clamp01(since / BeamSeconds);
    }

    // 0..1 grow progress, seconds after the bridge was triggered (0 during the beam).
    public static float Progress(float since)
    {
        return Mathf.Clamp01((since - BeamSeconds) / GrowSeconds);
    }

    // True when the plank at u (0 at the near end, 1 at the far end) is visible at this grow progress: planks appear at both
    // ends first and meet in the middle.
    public static bool PlankVisible(float u, float grow)
    {
        float fromEnd = Mathf.Min(u, 1f - u) * 2f;
        return fromEnd <= grow + 1e-5f;
    }
}

// The safe-light volume of a built bridge: a capsule round the walkway line (flat distance) with a height band along the ramp.
public struct BridgeZone
{
    public const float Margin = 1.0f;
    public const float Below = 3.0f;
    public const float Above = 4.0f;

    public Vector3 start;
    public Vector3 end;
    public float halfWidth;

    public float Radius
    {
        get { return halfWidth + Margin; }
    }

    // Parameter 0..1 of the closest point on the flat segment, and that point (at the ramp height there).
    public float Closest(Vector3 p, out Vector3 closest)
    {
        Vector2 a = new Vector2(start.x, start.z);
        Vector2 d = new Vector2(end.x - start.x, end.z - start.z);
        float sqr = d.sqrMagnitude;
        float t = sqr < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(new Vector2(p.x, p.z) - a, d) / sqr);
        closest = Vector3.Lerp(start, end, t);
        return t;
    }

    public bool Contains(Vector3 p)
    {
        Vector3 c;
        Closest(p, out c);
        float flat = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
        return flat < Radius && p.y > c.y - Below && p.y < c.y + Above;
    }

    // The nearest point just outside the zone, at the same height as p (p unchanged when outside).
    public Vector3 PushOut(Vector3 p)
    {
        if (!Contains(p))
        {
            return p;
        }

        Vector3 c;
        Closest(p, out c);
        Vector2 away = new Vector2(p.x - c.x, p.z - c.z);
        if (away.sqrMagnitude < 0.0001f)
        {
            Vector2 d = new Vector2(end.x - start.x, end.z - start.z);
            away = new Vector2(-d.y, d.x);
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector2.right;
            }
        }

        away.Normalize();
        return new Vector3(c.x + away.x * (Radius + 0.3f), p.y, c.z + away.y * (Radius + 0.3f));
    }

    // A soft sideways push (up to 3.2) for creatures steering near the zone, reaching 2.5 m beyond it, like a beacon's avoid push.
    public Vector3 Repel(Vector3 p)
    {
        Vector3 c;
        Closest(p, out c);
        if (p.y < c.y - Below || p.y > c.y + Above)
        {
            return Vector3.zero;
        }

        Vector2 away = new Vector2(p.x - c.x, p.z - c.z);
        float dist = away.magnitude;
        float radius = Radius;
        if (dist >= radius + 2.5f || dist < 0.01f)
        {
            return Vector3.zero;
        }

        float push = 1f - Mathf.Clamp01((dist - radius) / 2.5f);
        away /= dist;
        return new Vector3(away.x, 0f, away.y) * (3.2f * push);
    }
}
}
