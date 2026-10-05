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
            Assert.LessOrEqual(rocks[i].position.y, Top - 0.3f, "rock " + i);
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
}
}
