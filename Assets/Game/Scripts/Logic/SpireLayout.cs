using System;
using UnityEngine;

namespace LanternKeeper
{
public struct SpireGap
{
    public Vector2 nearXZ, farXZ;
    public bool required;
    public int nearBeacon, farBeacon;
}

// Fixed (seed-free) layout of the Island 5 spiral climb. XZ is relative to the island centre, which is the summit lamp.
public static class SpireLayout
{
    public const float Radius = 45f, SummitHeight = 40f, Turns = 1.75f;

    // Tunables for the terrain task.
    public const float StartRadius = Radius - 4f;      // beach spawn radius at angle 0
    public const float EndRadius = 3f;                 // spiral radius at the summit
    public const float RadiusPower = 1.2f;             // above 1 tightens the upper loops of the spiral
    public const float PathBaseHeight = 0.5f;          // height at the beach end of the path
    public const float PathTopHeight = 38.5f;          // height at the summit end of the path
    public const float RequiredGapWidth = 7.5f;        // XZ width of each required gap (6-9 m allowed)
    public const float SummitPlateauRadius = 6f;       // flat summit disc around the lamp
    public const float GapCentre1 = 0.18f;             // path u of required gap 1
    public const float GapCentre2 = 0.42f;             // path u of required gap 2
    public const float GapCentre3 = 0.66f;             // path u of required gap 3 (broken stair)

    // Path u of the 8 beacons, two per segment (before gap 1, gaps 1-2, gaps 2-3, gap 3 to gap 4). All clear of every gap span.
    static readonly float[] BeaconU = { 0.08f, 0.145f, 0.26f, 0.335f, 0.49f, 0.57f, 0.705f, 0.81f };

    public static Vector2 PathPoint(float u01)
    {
        float u = Mathf.Clamp01(u01);
        float r = EndRadius + (StartRadius - EndRadius) * Mathf.Pow(1f - u, RadiusPower);
        float angle = 2f * Mathf.PI * Turns * u;
        return new Vector2(r * Mathf.Cos(angle), r * Mathf.Sin(angle));
    }

    public static Vector2[] SpiralPath(int samples)
    {
        int n = Math.Max(2, samples);
        Vector2[] path = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            path[i] = PathPoint(i / (float)(n - 1));
        }

        return path;
    }

    // Smootherstep ease from PathBaseHeight to PathTopHeight.
    public static float PathHeight(float u01)
    {
        float t = Mathf.Clamp01(u01);
        float e = t * t * t * (t * (6f * t - 15f) + 10f);
        return PathBaseHeight + (PathTopHeight - PathBaseHeight) * e;
    }

    // Path parameter u of each beacon.
    public static float[] BeaconParameters()
    {
        return (float[])BeaconU.Clone();
    }

    public static Vector2[] BeaconXZ()
    {
        Vector2[] result = new Vector2[BeaconU.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = PathPoint(BeaconU[i]);
        }

        return result;
    }

    // Required gaps 1..4 in spec order (4 = summit span).
    public static SpireGap[] Gaps()
    {
        float[] centres = { GapCentre1, GapCentre2, GapCentre3 };
        SpireGap[] gaps = new SpireGap[4];
        for (int i = 0; i < 3; i++)
        {
            float half = HalfWidthU(centres[i]);
            gaps[i] = new SpireGap
            {
                nearXZ = PathPoint(centres[i] - half),
                farXZ = PathPoint(centres[i] + half),
                required = true,
                nearBeacon = 1 + 2 * i,
                farBeacon = -1
            };
        }

        // Far sides of gaps 1-3 are bridge lanterns and gap 4's is the plateau, so none has a far beacon (-1).
        gaps[3] = new SpireGap
        {
            nearXZ = PathPoint(SummitSpanNearU()),
            farXZ = PathPoint(1f),
            required = true,
            nearBeacon = BeaconU.Length - 1,
            farBeacon = -1
        };

        return gaps;
    }

    // Path u range (x = near side, y = far side) of required gap 0..3, for placing terrain breaks.
    public static Vector2 GapSpanU(int requiredIndex)
    {
        if (requiredIndex == 3)
        {
            return new Vector2(SummitSpanNearU(), 1f);
        }

        float centre = requiredIndex == 0 ? GapCentre1 : (requiredIndex == 1 ? GapCentre2 : GapCentre3);
        float half = HalfWidthU(centre);
        return new Vector2(centre - half, centre + half);
    }

    public static Vector2 LampXZ()
    {
        return Vector2.zero;
    }

    // Half width in u (each side of the centre) whose chord is RequiredGapWidth.
    static float HalfWidthU(float centre)
    {
        float lo = 0f, hi = 0.1f;
        for (int i = 0; i < 40; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (Vector2.Distance(PathPoint(centre - mid), PathPoint(centre + mid)) < RequiredGapWidth)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return 0.5f * (lo + hi);
    }

    // Path u where the chord to the summit end is RequiredGapWidth.
    static float SummitSpanNearU()
    {
        Vector2 end = PathPoint(1f);
        float lo = 0.5f, hi = 1f;
        for (int i = 0; i < 40; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (Vector2.Distance(PathPoint(mid), end) > RequiredGapWidth)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return 0.5f * (lo + hi);
    }
}
}
