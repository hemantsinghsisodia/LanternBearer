using UnityEngine;

namespace LanternKeeper
{
// Island 5 spawn rule for Moths and Shades: on the walkway (within SpireLayout.PathWidth of the path), at the keeper's reached ring or below.
public static class WalkwaySpawn
{
    // Highest path u whose walkway height is still inside `reached` or a lower ring. Summit allows the whole path.
    public static float MaxU(Ring reached)
    {
        int index = (int)reached;
        if (maxU[index] < 0f)
        {
            maxU[index] = ComputeMaxU(reached);
        }

        return maxU[index];
    }

    // The layout is fixed, so each ring's limit is solved once.
    static readonly float[] maxU = { -1f, -1f, -1f, -1f };

    static float ComputeMaxU(Ring reached)
    {
        float top;
        switch (reached)
        {
            case Ring.Low:
                top = RingLogic.LowTop;
                break;
            case Ring.Mid:
                top = RingLogic.MidTop;
                break;
            case Ring.High:
                top = RingLogic.HighTop;
                break;
            default:
                return 1f;
        }

        float lo = 0f;
        float hi = 1f;
        for (int i = 0; i < 30; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (SpireLayout.PathHeight(mid) < top)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    public static bool OnWalkway(Vector2 xz)
    {
        float u = SpireLayout.NearestU(xz);
        return (SpireLayout.PathPoint(u) - xz).magnitude <= SpireLayout.PathWidth;
    }

    // One roll: r1 picks the position along the allowed stretch of path, r2 the side offset. Both in 0-1.
    public static bool TryRoll(Ring reached, float r1, float r2, out Vector2 xz)
    {
        float u = Mathf.Clamp01(r1) * MaxU(reached);
        Vector2 centre = SpireLayout.PathPoint(u);
        Vector2 tangent = SpireLayout.PathTangent(u);
        Vector2 normal = new Vector2(-tangent.y, tangent.x);
        xz = centre + normal * ((Mathf.Clamp01(r2) * 2f - 1f) * SpireLayout.PathWidth * 0.5f);
        // u is already known and capped at the ring, and the side offset is under PathWidth, so the point is on the walkway by construction.
        return true;
    }

    public static bool Allowed(Vector2 xz, Ring reached)
    {
        float u = SpireLayout.NearestU(xz);
        if ((SpireLayout.PathPoint(u) - xz).magnitude > SpireLayout.PathWidth)
        {
            return false;
        }

        return RingLogic.Classify(SpireLayout.PathHeight(u)) <= reached;
    }
}
}
