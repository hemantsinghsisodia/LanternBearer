using UnityEngine;

namespace LanternKeeper
{
// Where the four other islands' silhouettes stand around Island 5 for the finale.
public static class FinaleLayout
{
    public const float SilhouetteDistance = 520f;
    const float FirstBearing = 200f, BearingStep = 45f;

    // XZ relative to the Island 5 centre. The bearing is measured from +Z, clockwise (towards +X).
    public static Vector2 SilhouetteXZ(int island)
    {
        float a = (FirstBearing + BearingStep * Mathf.Clamp(island, 0, 3)) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * SilhouetteDistance;
    }

    public static float SilhouetteScale(int island) { return 1f; }
}
}
