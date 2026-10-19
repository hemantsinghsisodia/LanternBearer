using UnityEngine;

namespace LanternKeeper
{
// The authored flight of the finale camera, as pure data and math (no scene, no state, no allocation per call).
// Everything is in metres relative to the great lamp's base (x east, z north, y up; the sea is about 40 m below it, the flame 11.9 m above).
//
// Position and look target are both a Catmull-Rom spline through timed knots (cubic Hermite with the central-difference tangent
// (p[i+1] - p[i-1]) / (t[i+1] - t[i-1]), so the speed is continuous; the first and last knots have zero tangent so the flight eases in and
// comes to rest). The look target is smoothed a little more (a short three-tap average) so the aim never jerks.
//
//   0-6 s     the lantern room, a slow 3/4 view (the ignition)
//   6-15 s    rises and swings round the far side of the lighthouse, the storm breaking around the lit beam
//   15-21 s   sweeps off to the island chain, looking from the lamp to Island 1
//   21-39 s   glides along the inner arc past Islands 1-4, each one centred in turn as its beacons flare (150-220 m away, 60-90 m above the sea)
//   39-60 s   swings back to the south-west of the lighthouse and turns to the dawn: the lighthouse in the foreground against the sun
//   60-66 s   a slow push-in, the lighthouse low in the frame, silhouetted against the sun
public static class FinaleFlightPath
{
    public const int KnotCount = 13;
    // The lamp's flame above the lamp base.
    public const float FlameHeight = 11.9f;
    // How far ahead the sky window is placed from the camera: the radius of the island ring.
    public const float WindowRadius = 300f;
    public const float MaxDownPitch = 20f;
    // Island 5 dawn sun (_MoonDir) bearing.
    public const float SunBearing = 35f;
    // Height of the aim point above each island's flare point: the beacon, its glow and the foot of the light column fill the frame.
    public const float AimHeight = 26f;
    const float LookSmoothSeconds = 0.4f;

    static readonly float[] Times = { 0f, 6f, 10f, 15f, 18f, 21f, 27f, 33f, 39f, 45f, 50f, 60f, 66f };

    static readonly Vector3[] Positions =
    {
        new Vector3(-11.8f, 6f, -5.5f),
        new Vector3(-13.1f, 8.5f, 9.2f),
        new Vector3(-4.4f, 26f, 49.8f),
        new Vector3(79f, 28f, -14f),
        new Vector3(35f, 22f, -85f),
        new Vector3(-37f, 26f, -122f),
        new Vector3(-152f, 28f, -178f),
        new Vector3(-108f, 26f, -85f),
        new Vector3(-39f, 28f, 87f),
        new Vector3(-133f, 28f, 75f),
        new Vector3(-96.6f, 22f, -25.9f),
        new Vector3(-40f, 4f, -57.3f),
        new Vector3(-32.1f, 3.5f, -45.9f),
    };

    // Declared after Positions; Aim() is a pure function so the static initialiser order does not matter.
    static readonly Vector3[] Looks =
    {
        new Vector3(0f, FlameHeight, 0f),
        new Vector3(0f, FlameHeight, 0f),
        new Vector3(0f, FlameHeight, 0f),
        new Vector3(-20f, 18f, -28f),
        new Vector3(-40f, 8f, -150f),
        Aim(0),
        Aim(1),
        Aim(2),
        Aim(3),
        new Vector3(-99f, 22.3f, 169f),
        new Vector3(-20f, 17f, 38.4f),
        new Vector3(43.5f, 40.3f, 61.9f),
        new Vector3(43.5f, 40.3f, 61.9f),
    };

    static Vector3 Aim(int island)
    {
        return FlarePoint(island) + Vector3.up * AimHeight;
    }

    // The flare point (the highest beacon) of island 0..3, relative to the lamp base (measured from the baked finale islands). These are measured
    // constants, not derived from FinaleLayout (the bake decides which beacon is highest): FinaleSequenceTest.FlightFramesTheLampAndPassesEachIsland
    // compares each against the installed island within 6 m, so a layout or bake change fails there instead of silently missing an island.
    public static Vector3 FlarePoint(int island)
    {
        switch (island)
        {
            case 0: return new Vector3(-85.94f, -32.42f, -291.11f);
            case 1: return new Vector3(-328.37f, -29.91f, -177.64f);
            case 2: return new Vector3(-204.03f, -34.93f, 62.93f);
            default: return new Vector3(-110.77f, -32.54f, 248.24f);
        }
    }

    // The camera position (relative to the lamp base) at finale time t.
    public static Vector3 Position(float t)
    {
        return Evaluate(Positions, t);
    }

    // The raw look target, before the extra smoothing.
    public static Vector3 RawLook(float t)
    {
        return Evaluate(Looks, t);
    }

    // The smoothed point the camera looks at.
    public static Vector3 LookTarget(float t)
    {
        return RawLook(t - LookSmoothSeconds) * 0.25f + RawLook(t) * 0.5f + RawLook(t + LookSmoothSeconds) * 0.25f;
    }

    // The unit view direction.
    public static Vector3 Forward(float t)
    {
        Vector3 f = LookTarget(t) - Position(t);
        return f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;
    }

    // Degrees below level (negative when the camera looks up).
    public static float DownPitch(float t)
    {
        return -Mathf.Asin(Mathf.Clamp(Forward(t).y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    // The bearing (degrees clockwise from +Z, seen from the lamp) of the sky the camera looks through: where the view ray reaches the island
    // ring. The cloud window is centred here, so it always sits in the middle of the picture.
    public static float LookBearing(float t)
    {
        Vector3 p = Position(t);
        Vector3 f = Forward(t);
        float fx = f.x, fz = f.z;
        float flat = Mathf.Sqrt(fx * fx + fz * fz);
        if (flat < 1e-4f)
        {
            return Mathf.Repeat(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg + 180f, 360f);
        }

        fx /= flat;
        fz /= flat;
        float b = p.x * fx + p.z * fz;
        float c = p.x * p.x + p.z * p.z - WindowRadius * WindowRadius;
        float s = -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
        return Mathf.Repeat(Mathf.Atan2(p.x + fx * s, p.z + fz * s) * Mathf.Rad2Deg, 360f);
    }

    static Vector3 Evaluate(Vector3[] values, float t)
    {
        t = Mathf.Clamp(t, Times[0], Times[KnotCount - 1]);
        int i = 0;
        while (i < KnotCount - 2 && t > Times[i + 1])
        {
            i++;
        }

        float t0 = Times[i], t1 = Times[i + 1];
        float h = t1 - t0;
        float s = (t - t0) / h;
        Vector3 m0 = Tangent(values, i);
        Vector3 m1 = Tangent(values, i + 1);
        float s2 = s * s, s3 = s2 * s;
        return values[i] * (2f * s3 - 3f * s2 + 1f)
            + m0 * (h * (s3 - 2f * s2 + s))
            + values[i + 1] * (-2f * s3 + 3f * s2)
            + m1 * (h * (s3 - s2));
    }

    static Vector3 Tangent(Vector3[] values, int i)
    {
        if (i <= 0 || i >= KnotCount - 1)
        {
            return Vector3.zero;
        }

        return (values[i + 1] - values[i - 1]) / (Times[i + 1] - Times[i - 1]);
    }
}
}
