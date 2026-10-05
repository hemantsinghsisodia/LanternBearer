using System;
using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public struct CladdingRock
{
    public Vector3 position;
    public float yaw;
    public float tilt;
    public float scale;
    public int variant;
}

// Pure planner: places rock-mesh cladding along steep terrain faces so stretched ground textures are covered.
public static class CliffCladdingPlanner
{
    public const float SteepGradient = 1f;          // tan(45 degrees)
    public const float MinScale = 2f;
    public const float MaxScale = 6f;
    public const float MinSpacing = 2f;
    public const float MaxSpacing = 2.2f;
    public const float MinFaceHeight = 2f;
    public const float Jitter = 0.25f;
    public const float WindowRadius = 0.8f;
    public const float RowStep = 0.6f;
    public const int MaxRows = 10;
    public const float RimOverhang = 0.8f;
    // Rock_Medium is about 1.2 m tall for a 1.5 m wide footprint, so sinking is a fraction of 0.8 * scale.
    public const float HeightPerScale = 0.8f;
    public const int VariantCount = 3;

    public static List<CladdingRock> Plan(float[,] heights, float worldSize, float heightScale, Vector2 originXZ, Func<Vector2, bool> excluded, int seed)
    {
        List<CladdingRock> result = new List<CladdingRock>();
        if (heights == null)
        {
            return result;
        }

        int rows = heights.GetLength(0);
        int cols = heights.GetLength(1);
        if (rows < 3 || cols < 3)
        {
            return result;
        }

        float cell = worldSize / (cols - 1);
        System.Random random = new System.Random(seed);

        List<int> steep = new List<int>();
        for (int z = 1; z < rows - 1; z++)
        {
            for (int x = 1; x < cols - 1; x++)
            {
                if (Gradient(heights, heightScale, cell, x, z).magnitude > SteepGradient)
                {
                    steep.Add(z * cols + x);
                }
            }
        }

        for (int i = steep.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            int t = steep[i];
            steep[i] = steep[j];
            steep[j] = t;
        }

        Dictionary<long, List<Vector2>> firstRow = new Dictionary<long, List<Vector2>>();
        const float hashCell = MaxSpacing;
        for (int n = 0; n < steep.Count; n++)
        {
            int x = steep[n] % cols;
            int z = steep[n] / cols;
            Vector2 anchor = new Vector2(originXZ.x + x * cell, originXZ.y + z * cell);
            if (IsExcluded(excluded, anchor))
            {
                continue;
            }

            float spacing = MinSpacing + (float)random.NextDouble() * (MaxSpacing - MinSpacing);
            if (Near(firstRow, anchor, spacing, hashCell))
            {
                continue;
            }

            float face = FaceHeight(heights, heightScale, cell, x, z);
            if (face < MinFaceHeight)
            {
                continue;
            }

            Vector2 up = Gradient(heights, heightScale, cell, x, z).normalized;
            Add(firstRow, anchor, hashCell);
            List<Vector3> window = Window(heights, heightScale, cell, originXZ, x, z, up, anchor);
            AddColumn(result, window, up, anchor, excluded, random);
        }

        return result;
    }

    static bool IsExcluded(Func<Vector2, bool> excluded, Vector2 p)
    {
        return excluded != null && excluded(p);
    }

    // Profile points (x, height, z) along the face through the anchor, limited to a small horizontal window.
    static List<Vector3> Window(float[,] heights, float heightScale, float cell, Vector2 originXZ, int x, int z, Vector2 up, Vector2 anchor)
    {
        int rows = heights.GetLength(0);
        int cols = heights.GetLength(1);
        List<Vector3> points = new List<Vector3>();
        points.Add(new Vector3(anchor.x, heights[z, x] * heightScale, anchor.y));
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 p = new Vector2(x, z);
            for (int step = 0; step < 400; step++)
            {
                p += up * side;
                int px = Mathf.RoundToInt(p.x);
                int pz = Mathf.RoundToInt(p.y);
                if (px < 1 || pz < 1 || px >= cols - 1 || pz >= rows - 1)
                {
                    break;
                }

                Vector2 world = new Vector2(originXZ.x + px * cell, originXZ.y + pz * cell);
                if ((world - anchor).magnitude > Mathf.Max(WindowRadius, cell * 1.5f))
                {
                    break;
                }

                points.Add(new Vector3(world.x, heights[pz, px] * heightScale, world.y));
                if (Gradient(heights, heightScale, cell, px, pz).magnitude <= SteepGradient)
                {
                    break;
                }
            }
        }

        return points;
    }

    // Stacks rocks from the foot of the face to its rim. Each rock sits on the profile point nearest its target height.
    static void AddColumn(List<CladdingRock> result, List<Vector3> window, Vector2 up, Vector2 anchor, Func<Vector2, bool> excluded, System.Random random)
    {
        float lo = float.MaxValue;
        float hi = float.MinValue;
        for (int i = 0; i < window.Count; i++)
        {
            lo = Mathf.Min(lo, window[i].y);
            hi = Mathf.Max(hi, window[i].y);
        }

        List<CladdingRock> column = new List<CladdingRock>();
        float level = lo;
        for (int row = 0; row < MaxRows && level < hi - 0.3f; row++)
        {
            float scale = MinScale + (float)random.NextDouble() * (MaxScale - MinScale);
            float rockHeight = scale * HeightPerScale;
            float remaining = hi - level;
            if (remaining < 0.7f * rockHeight - RimOverhang)
            {
                scale = Mathf.Max(MinScale, (remaining + RimOverhang) / (0.7f * HeightPerScale));
                rockHeight = scale * HeightPerScale;
            }

            float target = Mathf.Clamp(level + 0.3f * rockHeight, lo, hi);
            Vector3 best = window[0];
            for (int i = 1; i < window.Count; i++)
            {
                if (Mathf.Abs(window[i].y - target) < Mathf.Abs(best.y - target))
                {
                    best = window[i];
                }
            }

            float sink = (0.3f + (float)random.NextDouble() * 0.2f) * rockHeight;
            Vector2 outwardDir = -up;
            Vector2 xz = new Vector2(best.x, best.z) + outwardDir * (0.05f * scale)
                + new Vector2((float)(random.NextDouble() * 2 - 1) * Jitter, (float)(random.NextDouble() * 2 - 1) * Jitter);
            level += RowStep * rockHeight;
            if (excluded != null && excluded(xz))
            {
                // A gap in the stack would leave rocks above it hanging, so the whole column is dropped.
                return;
            }

            CladdingRock rock = new CladdingRock();
            // Sit at the target height, not the nearest profile sample: a near-vertical face has samples only at its foot and rim.
            rock.position = new Vector3(xz.x, target - sink, xz.y);
            float outward = Mathf.Atan2(outwardDir.x, outwardDir.y) * Mathf.Rad2Deg;
            rock.yaw = outward + ((float)random.NextDouble() * 2f - 1f) * 50f;
            rock.tilt = (float)random.NextDouble() * 12f;
            rock.scale = scale;
            rock.variant = random.Next(0, VariantCount);
            column.Add(rock);
        }

        result.AddRange(column);
    }

    static Vector2 Gradient(float[,] heights, float heightScale, float cell, int x, int z)
    {
        float dx = (heights[z, x + 1] - heights[z, x - 1]) * heightScale / (2f * cell);
        float dz = (heights[z + 1, x] - heights[z - 1, x]) * heightScale / (2f * cell);
        return new Vector2(dx, dz);
    }

    static float FaceHeight(float[,] heights, float heightScale, float cell, int x, int z)
    {
        int rows = heights.GetLength(0);
        int cols = heights.GetLength(1);
        Vector2 dir = Gradient(heights, heightScale, cell, x, z).normalized;
        float h = heights[z, x] * heightScale;
        float max = h;
        float min = h;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 p = new Vector2(x, z);
            for (int step = 0; step < 200; step++)
            {
                p += dir * side;
                int px = Mathf.RoundToInt(p.x);
                int pz = Mathf.RoundToInt(p.y);
                if (px < 1 || pz < 1 || px >= cols - 1 || pz >= rows - 1)
                {
                    break;
                }

                float ph = heights[pz, px] * heightScale;
                max = Mathf.Max(max, ph);
                min = Mathf.Min(min, ph);
                if (Gradient(heights, heightScale, cell, px, pz).magnitude <= SteepGradient)
                {
                    break;
                }
            }
        }

        return max - min;
    }

    static long Key(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }

    static void Add(Dictionary<long, List<Vector2>> map, Vector2 p, float hashCell)
    {
        long key = Key(Mathf.FloorToInt(p.x / hashCell), Mathf.FloorToInt(p.y / hashCell));
        List<Vector2> list;
        if (!map.TryGetValue(key, out list))
        {
            list = new List<Vector2>();
            map[key] = list;
        }

        list.Add(p);
    }

    static bool Near(Dictionary<long, List<Vector2>> map, Vector2 p, float distance, float hashCell)
    {
        int cx = Mathf.FloorToInt(p.x / hashCell);
        int cz = Mathf.FloorToInt(p.y / hashCell);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                List<Vector2> list;
                if (!map.TryGetValue(Key(cx + dx, cz + dz), out list))
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if ((list[i] - p).sqrMagnitude < distance * distance)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
}
