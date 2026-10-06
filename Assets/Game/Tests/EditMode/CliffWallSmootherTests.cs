using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CliffWallSmootherTests
{
    const int N = 64;
    const float Cell = 0.25f;
    const float HeightScale = 10f;
    const float Top = 6f;
    const int Iterations = 40;
    const float MaxDelta = 0.5f;

    // A 6 m plateau whose rim runs at 30 degrees to the grid. The rim column is rounded per row, so the wall is a stair-step
    // sawtooth (an aliased edge), and the wall itself is three cells wide.
    static float[,] Staircase()
    {
        float[,] h = new float[N, N];
        for (int y = 0; y < N; y++)
        {
            int edge = Mathf.RoundToInt(16f + y * Mathf.Tan(30f * Mathf.Deg2Rad));
            for (int x = 0; x < N; x++)
            {
                float t = Mathf.Clamp01((x - edge + 1) / 3f);
                h[y, x] = Top / HeightScale * t;
            }
        }

        return h;
    }

    static bool Extra(int x, int y)
    {
        // A "trail" block on the wall's foot side and a spot far from the wall.
        return (x >= 10 && x <= 14 && y >= 40 && y <= 44) || (x == 50 && y == 10);
    }

    static float[,] Run(float[,] h)
    {
        return CliffWallSmoother.Smooth(h, Cell, HeightScale, Extra, Iterations, MaxDelta);
    }

    // Circular variance (1 - mean resultant length) of the horizontal gradient direction over the wall cells.
    static float DirectionVariance(float[,] h, bool[,] wall)
    {
        double sx = 0, sy = 0;
        int n = 0;
        for (int y = 1; y < N - 1; y++)
        {
            for (int x = 1; x < N - 1; x++)
            {
                if (!wall[y, x])
                {
                    continue;
                }

                double gx = h[y, x + 1] - h[y, x - 1];
                double gy = h[y + 1, x] - h[y - 1, x];
                double len = System.Math.Sqrt(gx * gx + gy * gy);
                if (len < 1e-9)
                {
                    continue;
                }

                sx += gx / len;
                sy += gy / len;
                n++;
            }
        }

        return n == 0 ? 0f : (float)(1.0 - System.Math.Sqrt(sx * sx + sy * sy) / n);
    }

    [Test]
    public void LockedCellsUnchanged()
    {
        float[,] input = Staircase();
        float[,] output = Run(input);
        int locked = 0;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                bool gentle = CliffWallSmoother.Gradient(input, x, y, Cell, HeightScale) <= 1f;
                if (gentle || Extra(x, y))
                {
                    locked++;
                    Assert.AreEqual(input[y, x], output[y, x], 0f, "cell " + x + "," + y);
                }
            }
        }

        Assert.Greater(locked, 1000);
    }

    [Test]
    public void ExtraNeighboursAreLockedToo()
    {
        float[,] input = Staircase();
        float[,] output = CliffWallSmoother.Smooth(input, Cell, HeightScale, (x, y) => x == 17 && y == 10, Iterations, MaxDelta);
        for (int y = 9; y <= 11; y++)
        {
            for (int x = 16; x <= 18; x++)
            {
                Assert.AreEqual(input[y, x], output[y, x], 0f, "cell " + x + "," + y);
            }
        }
    }

    [Test]
    public void WallActuallyChanges()
    {
        float[,] input = Staircase();
        float[,] output = Run(input);
        int changed = 0;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                if (input[y, x] != output[y, x])
                {
                    changed++;
                }
            }
        }

        Assert.Greater(changed, 100);
    }

    [Test]
    public void ChangeIsBounded()
    {
        float[,] input = Staircase();
        float[,] output = Run(input);
        float max = 0f;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                max = Mathf.Max(max, Mathf.Abs(output[y, x] - input[y, x]) * HeightScale);
            }
        }

        Assert.LessOrEqual(max, MaxDelta + 1e-4f);
        Assert.Greater(max, 0.1f);
    }

    [Test]
    public void SawtoothReduced()
    {
        float[,] input = Staircase();
        bool[,] wall = CliffWallSmoother.WallCells(input, Cell, HeightScale, Extra);
        float before = DirectionVariance(input, wall);
        float after = DirectionVariance(Run(input), wall);
        Debug.Log("Sawtooth direction variance before " + before + " after " + after);
        Assert.Greater(before, 0.01f);
        Assert.LessOrEqual(after, before * 0.5f, "facet direction variance must drop by at least 50% (" + before + " -> " + after + ")");
    }

    [Test]
    public void Deterministic()
    {
        float[,] a = Run(Staircase());
        float[,] b = Run(Staircase());
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                Assert.AreEqual(a[y, x], b[y, x], 0f);
            }
        }
    }

    [Test]
    public void FlatMapUnchanged()
    {
        float[,] flat = new float[N, N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                flat[y, x] = 0.3f;
            }
        }

        float[,] output = Run(flat);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                Assert.AreEqual(0.3f, output[y, x], 0f);
            }
        }
    }

    [Test]
    public void InputIsNotModified()
    {
        float[,] input = Staircase();
        float[,] copy = Staircase();
        Run(input);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                Assert.AreEqual(copy[y, x], input[y, x], 0f);
            }
        }
    }
}
}
