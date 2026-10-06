using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CliffSkinPlannerTests
{
    const int N = 64;
    const float Cell = 0.11f;                 // matches the real islands (0.11 to 0.18 m per heightmap cell)
    const float WorldSize = (N - 1) * Cell;
    const float HeightScale = 10f;
    const float Top = 6f;
    const float Threshold = CliffSkinPlanner.StepThresholdM;
    static readonly Vector2 Origin = new Vector2(-3f, -3f);

    // A 6 m plateau shaped as a rectangle rotated 30 degrees against the grid, so all four rims are aliased one-cell staircases.
    static readonly Vector2 Dir = new Vector2(Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad));
    static readonly Vector2 Perp = new Vector2(-Dir.y, Dir.x);
    const float HalfLong = 18f;   // cells
    const float HalfShort = 10f;  // cells

    static float[,] Plateau()
    {
        float[,] h = new float[N, N];
        for (int z = 0; z < N; z++)
        {
            for (int x = 0; x < N; x++)
            {
                Vector2 d = new Vector2(x - 32, z - 32);
                if (Mathf.Abs(Vector2.Dot(d, Dir)) <= HalfLong && Mathf.Abs(Vector2.Dot(d, Perp)) <= HalfShort)
                {
                    h[z, x] = Top / HeightScale;
                }
            }
        }

        return h;
    }

    static List<SkinStrip> Run(float[,] h = null)
    {
        return CliffSkinPlanner.Plan(h ?? Plateau(), WorldSize, HeightScale, Origin, Threshold);
    }

    static Vector2 CellCentre(int x, int z)
    {
        return new Vector2(Origin.x + x * Cell, Origin.y + z * Cell);
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    static float DistanceToStrips(Vector2 p, List<SkinStrip> strips)
    {
        float best = float.MaxValue;
        for (int s = 0; s < strips.Count; s++)
        {
            List<SkinPoint> pts = strips[s].points;
            for (int i = 1; i < pts.Count; i++)
            {
                best = Mathf.Min(best, DistanceToSegment(p, pts[i - 1].position, pts[i].position));
            }

            if (strips[s].closed && pts.Count > 1)
            {
                best = Mathf.Min(best, DistanceToSegment(p, pts[pts.Count - 1].position, pts[0].position));
            }
        }

        return best;
    }

    [Test]
    public void Deterministic()
    {
        List<SkinStrip> a = Run();
        List<SkinStrip> b = Run();
        Assert.Greater(a.Count, 0);
        Assert.AreEqual(a.Count, b.Count);
        for (int s = 0; s < a.Count; s++)
        {
            Assert.AreEqual(a[s].points.Count, b[s].points.Count);
            Assert.AreEqual(a[s].closed, b[s].closed);
            for (int i = 0; i < a[s].points.Count; i++)
            {
                Assert.AreEqual(a[s].points[i].position, b[s].points[i].position);
                Assert.AreEqual(a[s].points[i].foot, b[s].points[i].foot);
                Assert.AreEqual(a[s].points[i].rim, b[s].points[i].rim);
            }
        }
    }

    [Test]
    public void CoversEveryStepEdge()
    {
        float[,] h = Plateau();
        List<SkinStrip> strips = Run(h);
        Assert.Greater(strips.Count, 0);
        int edges = 0;
        for (int z = 1; z < N - 1; z++)
        {
            for (int x = 1; x < N - 1; x++)
            {
                Vector2 c = CellCentre(x, z);
                if (Mathf.Abs(h[z, x + 1] - h[z, x]) * HeightScale > Threshold)
                {
                    edges++;
                    Vector2 mid = (c + CellCentre(x + 1, z)) * 0.5f;
                    Assert.LessOrEqual(DistanceToStrips(mid, strips), 0.5f, "x-edge " + x + "," + z);
                }

                if (Mathf.Abs(h[z + 1, x] - h[z, x]) * HeightScale > Threshold)
                {
                    edges++;
                    Vector2 mid = (c + CellCentre(x, z + 1)) * 0.5f;
                    Assert.LessOrEqual(DistanceToStrips(mid, strips), 0.5f, "z-edge " + x + "," + z);
                }
            }
        }

        Assert.Greater(edges, 40);
    }

    [Test]
    public void IsSmooth()
    {
        List<SkinStrip> strips = Run();
        Assert.Greater(strips.Count, 0);
        float worst = 0f;
        for (int s = 0; s < strips.Count; s++)
        {
            List<SkinPoint> pts = strips[s].points;
            int count = pts.Count;
            Assert.GreaterOrEqual(count, 4);
            int first = strips[s].closed ? 0 : 1;
            int last = strips[s].closed ? count : count - 1;
            for (int i = first; i < last; i++)
            {
                Vector2 a = pts[(i - 1 + count) % count].position;
                Vector2 b = pts[i].position;
                Vector2 c = pts[(i + 1) % count].position;
                float turn = Vector2.Angle(b - a, c - b);
                worst = Mathf.Max(worst, turn);
            }
        }

        Assert.Less(worst, 25f, "max turning angle " + worst);

        // One long staircase side, away from the corners, must come out as a near-straight line.
        List<Vector2> side = new List<Vector2>();
        Vector2 centre = CellCentre(32, 32);
        for (int s = 0; s < strips.Count; s++)
        {
            for (int i = 0; i < strips[s].points.Count; i++)
            {
                Vector2 d = strips[s].points[i].position - centre;
                if (Vector2.Dot(d, Perp) > 0f && Mathf.Abs(Vector2.Dot(d, Dir)) < 1.0f)
                {
                    side.Add(strips[s].points[i].position);
                }
            }
        }

        Assert.Greater(side.Count, 10);
        Vector2 mean = Vector2.zero;
        for (int i = 0; i < side.Count; i++)
        {
            mean += side[i];
        }

        mean /= side.Count;
        Vector2 normal = Perp;
        float maxDeviation = 0f;
        for (int i = 0; i < side.Count; i++)
        {
            maxDeviation = Mathf.Max(maxDeviation, Mathf.Abs(Vector2.Dot(side[i] - mean, normal)));
        }

        Assert.Less(maxDeviation, 0.06f, "staircase side deviates " + maxDeviation + " m from a straight line");
    }

    [Test]
    public void StaysProudButClose()
    {
        float[,] h = Plateau();
        List<SkinStrip> strips = Run(h);
        Assert.Greater(strips.Count, 0);
        float limit = Top / HeightScale - 0.01f;
        float half = Cell * 0.5f;
        for (int s = 0; s < strips.Count; s++)
        {
            for (int i = 0; i < strips[s].points.Count; i++)
            {
                Vector2 p = strips[s].points[i].position;
                float nearest = float.MaxValue;
                for (int z = 0; z < N; z++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        if (h[z, x] < limit)
                        {
                            continue;
                        }

                        Vector2 c = CellCentre(x, z);
                        float dx = Mathf.Max(Mathf.Abs(p.x - c.x) - half, 0f);
                        float dz = Mathf.Max(Mathf.Abs(p.y - c.y) - half, 0f);
                        nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
                    }
                }

                Assert.Greater(nearest, 0f, "strip " + s + " point " + i + " lies inside the high cells");
                Assert.LessOrEqual(nearest, 0.4f, "strip " + s + " point " + i + " is " + nearest + " m beyond the face");
            }
        }
    }

    [Test]
    public void NoStripOnFlatMap()
    {
        Assert.AreEqual(0, Run(new float[N, N]).Count);
        float[,] ramp = new float[N, N];
        for (int z = 0; z < N; z++)
        {
            for (int x = 0; x < N; x++)
            {
                ramp[z, x] = x * 0.01f; // 0.1 m of rise per 0.11 m cell: steep, but never a step
            }
        }

        Assert.AreEqual(0, Run(ramp).Count);
    }

    [Test]
    public void FootAndRimHeights()
    {
        List<SkinStrip> strips = Run();
        Assert.Greater(strips.Count, 0);
        float[,] h = Plateau();
        for (int s = 0; s < strips.Count; s++)
        {
            for (int i = 0; i < strips[s].points.Count; i++)
            {
                SkinPoint pt = strips[s].points[i];
                float low = float.MaxValue;
                float high = float.MinValue;
                for (int z = 0; z < N; z++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        if ((CellCentre(x, z) - pt.position).magnitude <= 0.6f)
                        {
                            low = Mathf.Min(low, h[z, x] * HeightScale);
                            high = Mathf.Max(high, h[z, x] * HeightScale);
                        }
                    }
                }

                Assert.AreEqual(low, pt.foot, 0.2f, "foot " + s + "/" + i);
                Assert.AreEqual(high, pt.rim, 0.2f, "rim " + s + "/" + i);
                Assert.AreEqual(1f, pt.normal.magnitude, 0.01f);
            }
        }
    }
}
}
