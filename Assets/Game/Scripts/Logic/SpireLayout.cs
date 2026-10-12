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
    public const float EndRadius = 6f;                 // spiral radius at the summit: the edge of the lamp plateau
    public const float RadiusPower = 1.2f;             // above 1 tightens the upper loops of the spiral
    public const float PathBaseHeight = 0.5f;          // height at the beach end of the path
    public const float PathTopHeight = 38.5f;          // height at the summit end of the path
    public const float RequiredGapWidth = 12f;         // XZ width of each required gap: wider than a sprint jump (about 10.8 m)
    public const float GapStep = 3.6f;                 // the far side of each required gap is this much higher (a jump apex is 2.8 m)
    public const float SummitPlateauRadius = 6f;       // flat summit disc around the lamp
    public const float PathWidth = 3.5f;               // flattened walkway width
    public const float GapCentre1 = 0.18f;            // path u of required gap 1
    public const float GapCentre2 = 0.42f;             // path u of required gap 2
    public const float GapCentre3 = 0.64f;             // path u of required gap 3 (broken stair)

    // Path u of the 8 beacons, two per segment (before gap 1, gaps 1-2, gaps 2-3, gap 3 to gap 4). All clear of every gap span.
    static readonly float[] BeaconU = { 0.08f, 0.145f, 0.26f, 0.335f, 0.49f, 0.57f, 0.689f, 0.825f };

    public static Vector2 PathPoint(float u01)
    {
        float u = Mathf.Clamp01(u01);
        float r = EndRadius + (StartRadius - EndRadius) * Mathf.Pow(1f - u, RadiusPower);
        float angle = 2f * Mathf.PI * Turns * u;
        return new Vector2(r * Mathf.Cos(angle), r * Mathf.Sin(angle));
    }

    // Unit direction of travel (increasing u) at u. Central difference; one-sided at the two ends.
    public static Vector2 PathTangent(float u01)
    {
        const float step = 0.0005f;
        float u = Mathf.Clamp01(u01);
        Vector2 d = PathPoint(Mathf.Min(1f, u + step)) - PathPoint(Mathf.Max(0f, u - step));
        return d.sqrMagnitude < 1e-12f ? Vector2.right : d.normalized;
    }

    // Path parameter u of the point on the spiral closest to xz. Brute force over the samples, then refined between the neighbours.
    public static float NearestU(Vector2 xz)
    {
        const int samples = 4000;
        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i <= samples; i++)
        {
            float sqr = (PathPoint(i / (float)samples) - xz).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = i;
            }
        }

        float lo = Mathf.Max(0f, (best - 1) / (float)samples);
        float hi = Mathf.Min(1f, (best + 1) / (float)samples);
        for (int i = 0; i < 24; i++)
        {
            float a = lo + (hi - lo) / 3f;
            float b = hi - (hi - lo) / 3f;
            if ((PathPoint(a) - xz).sqrMagnitude < (PathPoint(b) - xz).sqrMagnitude)
            {
                hi = b;
            }
            else
            {
                lo = a;
            }
        }

        return 0.5f * (lo + hi);
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

    // Smootherstep ease from PathBaseHeight to PathTopHeight, less the four gap steps, plus a GapStep rise in the middle of each
    // required gap: the far side of every gap is a cliff GapStep higher than the near side, so a jump cannot land on it.
    public static float PathHeight(float u01)
    {
        float t = Mathf.Clamp01(u01);
        int steps = 0;
        float[] mids = StepMids();
        for (int i = 0; i < mids.Length; i++)
        {
            steps += t >= mids[i] ? 1 : 0;
        }

        return PathBaseHeight + SmoothRise(t) + GapStep * steps;
    }

    // The same height with each step eased over about 0.08 of u, for shaping the hillside around the path (never for the walkway).
    public static float PathHeightSmooth(float u01)
    {
        float t = Mathf.Clamp01(u01);
        float steps = 0f;
        float[] mids = StepMids();
        for (int i = 0; i < mids.Length; i++)
        {
            steps += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mids[i] - 0.04f, mids[i] + 0.04f, t));
        }

        return PathBaseHeight + SmoothRise(t) + GapStep * steps;
    }

    static float SmoothRise(float t)
    {
        float e = t * t * t * (t * (6f * t - 15f) + 10f);
        return (PathTopHeight - PathBaseHeight - 4f * GapStep) * e;
    }

    static float[] stepMids;

    static float[] StepMids()
    {
        if (stepMids == null)
        {
            float[] mids = new float[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 span = GapSpanU(i);
                mids[i] = 0.5f * (span.x + span.y);
            }

            stepMids = mids;
        }

        return stepMids;
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
