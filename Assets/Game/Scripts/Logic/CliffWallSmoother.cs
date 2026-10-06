using System;

namespace LanternKeeper
{
// Pure smoother for cliff walls. Plateau and ridge walls in the heightmap are aliased stair-steps; this relaxes the wall
// cells toward their neighbourhood mean so a wall becomes one continuous surface, while every other cell stays bit-identical.
public static class CliffWallSmoother
{
    public const float LockedGradient = 1f;   // tan(45 degrees): cells at or below this slope on the input are locked
    public const int DefaultRadius = 2;       // 5 x 5 neighbourhood

    // heights01: Unity heights [row, column] in 0..1. cellSizeM: horizontal size of one cell. heightScaleM: metres at height 1.
    // lockedExtra(column, row): true for cells that must not move (trail, beacon ring, hidden path, spawn, shore); their 1-cell
    // neighbours are locked too. Returns a new array. A wall cell is any cell that is not locked; each moves at most maxDeltaM.
    public static float[,] Smooth(float[,] heights01, float cellSizeM, float heightScaleM, Func<int, int, bool> lockedExtra, int iterations, float maxDeltaM, int radius = DefaultRadius)
    {
        int rows = heights01.GetLength(0);
        int cols = heights01.GetLength(1);
        float[,] source = (float[,])heights01.Clone();
        bool[,] wall = WallCells(heights01, cellSizeM, heightScaleM, lockedExtra);
        float maxDelta = heightScaleM > 0f ? maxDeltaM / heightScaleM : 0f;
        float[,] current = (float[,])heights01.Clone();
        float[,] next = (float[,])heights01.Clone();
        for (int it = 0; it < iterations; it++)
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (!wall[y, x])
                    {
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= rows)
                        {
                            continue;
                        }

                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= cols)
                            {
                                continue;
                            }

                            sum += current[yy, xx];
                            count++;
                        }
                    }

                    float target = sum / count;
                    float lo = source[y, x] - maxDelta;
                    float hi = source[y, x] + maxDelta;
                    next[y, x] = target < lo ? lo : (target > hi ? hi : target);
                }
            }

            float[,] swap = current;
            current = next;
            next = swap;
        }

        return current;
    }

    // Wall cells: not locked. Locked = slope at or below 45 degrees on the input, lockedExtra, and the 1-cell neighbours of lockedExtra.
    public static bool[,] WallCells(float[,] heights01, float cellSizeM, float heightScaleM, Func<int, int, bool> lockedExtra)
    {
        int rows = heights01.GetLength(0);
        int cols = heights01.GetLength(1);
        bool[,] locked = new bool[rows, cols];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (Gradient(heights01, x, y, cellSizeM, heightScaleM) <= LockedGradient)
                {
                    locked[y, x] = true;
                }

                if (lockedExtra != null && lockedExtra(x, y))
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int yy = y + dy;
                            int xx = x + dx;
                            if (yy >= 0 && yy < rows && xx >= 0 && xx < cols)
                            {
                                locked[yy, xx] = true;
                            }
                        }
                    }
                }
            }
        }

        bool[,] wall = new bool[rows, cols];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                wall[y, x] = !locked[y, x];
            }
        }

        return wall;
    }

    // Slope as rise over run (tan of the slope angle), central differences, one-sided at the border.
    public static float Gradient(float[,] h, int x, int y, float cellSizeM, float heightScaleM)
    {
        int rows = h.GetLength(0);
        int cols = h.GetLength(1);
        int x0 = x > 0 ? x - 1 : x;
        int x1 = x < cols - 1 ? x + 1 : x;
        int y0 = y > 0 ? y - 1 : y;
        int y1 = y < rows - 1 ? y + 1 : y;
        float gx = x1 > x0 ? (h[y, x1] - h[y, x0]) * heightScaleM / ((x1 - x0) * cellSizeM) : 0f;
        float gy = y1 > y0 ? (h[y1, x] - h[y0, x]) * heightScaleM / ((y1 - y0) * cellSizeM) : 0f;
        return (float)Math.Sqrt(gx * gx + gy * gy);
    }
}
}
