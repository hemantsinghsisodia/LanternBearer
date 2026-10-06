using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CliffCladdingPlannerTests
{
    const int N = 64;
    const float WorldSize = 128f;
    const float HeightScale = 10f;
    const float Top = 6f;
    static readonly Vector2 Origin = new Vector2(-64f, -64f);

    static float[,] Plateau()
    {
        float[,] h = new float[N, N];
        for (int z = 20; z < 44; z++)
        {
            for (int x = 20; x < 44; x++)
            {
                h[z, x] = Top / HeightScale;
            }
        }

        return h;
    }

    static List<CladdingRock> Run(System.Func<Vector2, bool> excluded = null, int seed = 7)
    {
        return CliffCladdingPlanner.Plan(Plateau(), WorldSize, HeightScale, Origin, excluded, seed);
    }

    [Test]
    public void PlanCoversEdge()
    {
        float[,] h = Plateau();
        List<CladdingRock> rocks = Run();
        Assert.Greater(rocks.Count, 10);
        float cell = WorldSize / (N - 1);
        int steepCells = 0;
        for (int z = 1; z < N - 1; z++)
        {
            for (int x = 1; x < N - 1; x++)
            {
                float dx = (h[z, x + 1] - h[z, x - 1]) * HeightScale / (2f * cell);
                float dz = (h[z + 1, x] - h[z - 1, x]) * HeightScale / (2f * cell);
                if (new Vector2(dx, dz).magnitude <= 1f)
                {
                    continue;
                }

                steepCells++;
                Vector2 c = new Vector2(Origin.x + x * cell, Origin.y + z * cell);
                bool covered = false;
                for (int i = 0; i < rocks.Count && !covered; i++)
                {
                    covered = (new Vector2(rocks[i].position.x, rocks[i].position.z) - c).magnitude <= 3f;
                }

                Assert.IsTrue(covered, "steep cell " + x + "," + z + " has no rock within 3 m");
            }
        }

        Assert.Greater(steepCells, 0);
    }

    [Test]
    public void PlanIsDeterministic()
    {
        List<CladdingRock> a = Run(null, 11);
        List<CladdingRock> b = Run(null, 11);
        Assert.Greater(a.Count, 0);
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].position, b[i].position);
            Assert.AreEqual(a[i].variant, b[i].variant);
            Assert.AreEqual(a[i].scale, b[i].scale);
        }
    }

    [Test]
    public void PlanRespectsExclusion()
    {
        List<CladdingRock> rocks = Run(p => p.x < 0f);
        Assert.Greater(rocks.Count, 0);
        for (int i = 0; i < rocks.Count; i++)
        {
            Assert.GreaterOrEqual(rocks[i].position.x, 0f);
        }
    }

    [Test]
    public void PlanNeverAboveTop()
    {
        List<CladdingRock> rocks = Run();
        Assert.Greater(rocks.Count, 0);
        for (int i = 0; i < rocks.Count; i++)
        {
            Assert.LessOrEqual(rocks[i].position.y, Top - 0.3f, "rock base " + i);
            // The top of the rock (base plus scaled height) may overhang the rim by RimOverhang at most, plus a small tolerance.
            float rockTop = rocks[i].position.y + rocks[i].scale * CliffCladdingPlanner.HeightPerScale;
            Assert.LessOrEqual(rockTop, Top + CliffCladdingPlanner.RimOverhang + 0.05f, "rock top " + i);
        }
    }

    [Test]
    public void PlanScalesInRange()
    {
        List<CladdingRock> rocks = Run();
        Assert.Greater(rocks.Count, 0);
        for (int i = 0; i < rocks.Count; i++)
        {
            Assert.GreaterOrEqual(rocks[i].scale, 2f);
            Assert.LessOrEqual(rocks[i].scale, 6f);
            Assert.That(rocks[i].variant, Is.InRange(0, 2));
        }
    }

    [Test]
    public void PlanStacksRowsUpATallFace()
    {
        List<CladdingRock> rocks = Run();
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < rocks.Count; i++)
        {
            min = Mathf.Min(min, rocks[i].position.y);
            max = Mathf.Max(max, rocks[i].position.y);
        }

        Assert.Greater(max - min, 3f, "a 6 m face needs rocks stacked up it");
    }

    // Trail strip along the foot of the south face (z -29 .. -24.9, x -6 .. 6): faces beside and under it still get cladding.
    static bool InStrip(Vector2 p)
    {
        return p.x >= -6f && p.x <= 6f && p.y >= -29f && p.y <= -24.9f;
    }

    static float Reserved(Vector2 p)
    {
        return InStrip(p) ? 0f : float.NaN;
    }

    [Test]
    public void PlanCladsFacesBesideAndUnderATrail()
    {
        float[,] h = Plateau();
        List<CladdingRock> rocks = CliffCladdingPlanner.Plan(h, WorldSize, HeightScale, Origin, null, Reserved, 7);
        Assert.Greater(rocks.Count, 10);
        for (int i = 0; i < rocks.Count; i++)
        {
            // A rock stands on the trail when its base is within walking height of the trail ground (0 m here). Higher rows
            // are mid-face and only overhang the strip's xz.
            bool onTrail = InStrip(new Vector2(rocks[i].position.x, rocks[i].position.z)) && rocks[i].position.y < CliffCladdingPlanner.WalkerHeight;
            Assert.IsFalse(onTrail, "rock " + i + " sits on the trail strip at y " + rocks[i].position.y);
        }

        float cell = WorldSize / (N - 1);
        int checkedCells = 0;
        for (int z = 1; z < N - 1; z++)
        {
            for (int x = 1; x < N - 1; x++)
            {
                float dx = (h[z, x + 1] - h[z, x - 1]) * HeightScale / (2f * cell);
                float dz = (h[z + 1, x] - h[z - 1, x]) * HeightScale / (2f * cell);
                if (new Vector2(dx, dz).magnitude <= 1f)
                {
                    continue;
                }

                Vector2 c = new Vector2(Origin.x + x * cell, Origin.y + z * cell);
                if (c.y > -20f || Mathf.Abs(c.x) > 9f)
                {
                    continue;
                }

                checkedCells++;
                bool covered = false;
                for (int i = 0; i < rocks.Count && !covered; i++)
                {
                    covered = (new Vector2(rocks[i].position.x, rocks[i].position.z) - c).magnitude <= 3f;
                }

                Assert.IsTrue(covered, "steep cell " + x + "," + z + " near the trail has no rock within 3 m");
            }
        }

        Assert.Greater(checkedCells, 0);
    }

    [Test]
    public void NearTrailRocksStayInRange()
    {
        List<CladdingRock> rocks = CliffCladdingPlanner.Plan(Plateau(), WorldSize, HeightScale, Origin, null, Reserved, 7);
        for (int i = 0; i < rocks.Count; i++)
        {
            Assert.GreaterOrEqual(rocks[i].scale, CliffCladdingPlanner.NearMinScale);
            Assert.LessOrEqual(rocks[i].scale, CliffCladdingPlanner.MaxScale);
        }
    }
}
}
