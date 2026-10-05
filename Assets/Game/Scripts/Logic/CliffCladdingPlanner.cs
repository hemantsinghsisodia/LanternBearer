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
    // A face continues down/up while the slope stays steeper than about 35 degrees, so stepped walls count as one face.
    public const float FaceGradient = 0.7f;
    public const float MinScale = 2f;
    public const float MaxScale = 6f;
    public const float NearMinScale = 1.5f;
    public const float NearMaxScale = 3f;
    public const float FootprintRadius = 0.5f;
    public const float NearFootprintRadius = 0.35f;
    public const float WalkerHeight = 1.8f;
    public const float MinSpacing = 2f;
    public const float MaxSpacing = 2.2f;
    // Every face over 2 m must be clad; shorter steps down to 0.8 m get small rocks too, since they show the same stretched texture.
    public const float MinFaceHeight = 0.8f;
    public const float SmallFaceHeight = 2.5f;
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
        return Plan(heights, worldSize, heightScale, originXZ, excluded, null, seed);
    }

    // excluded: hard no-go (shore); a rock whose position is excluded drops its whole column.
    // reserved: walkable ground (trail, beacon ring); returns the walkway height at that point, or NaN when the point is free. Faces beside it are still clad, with smaller rocks pushed into the face,
    // and a rock is rejected only when its footprint would sit on reserved ground at a height it would block.
    public static List<CladdingRock> Plan(float[,] heights, float worldSize, float heightScale, Vector2 originXZ, Func<Vector2, bool> excluded, Func<Vector2, float> reserved, int seed)
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
            AddColumn(result, window, face, up, excluded, reserved, heights, heightScale, worldSize, originXZ, random);
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
                if (Gradient(heights, heightScale, cell, px, pz).magnitude <= FaceGradient)
                {
                    break;
                }
            }
        }

        return points;
    }

    // Stacks rocks from the foot of the face to its rim.
    static void AddColumn(List<CladdingRock> result, List<Vector3> window, float face, Vector2 up, Func<Vector2, bool> excluded, Func<Vector2, float> reserved,
        float[,] heights, float heightScale, float worldSize, Vector2 originXZ, System.Random random)
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
        Vector2 outwardDir = -up;
        for (int row = 0; row < MaxRows && level < hi - 0.3f; row++)
        {
            bool small = face < SmallFaceHeight;
            float minScale = small ? NearMinScale : MinScale;
            float scale = small ? NearMinScale + (float)random.NextDouble() * (NearMaxScale - NearMinScale) : MinScale + (float)random.NextDouble() * (MaxScale - MinScale);
            float remaining = hi - level;
            if (remaining < 0.7f * scale * HeightPerScale - RimOverhang)
            {
                scale = Mathf.Max(minScale, (remaining + RimOverhang) / (0.7f * HeightPerScale));
            }

            float sinkFraction = 0.3f + (float)random.NextDouble() * 0.2f;
            Vector2 jitter = new Vector2((float)(random.NextDouble() * 2 - 1) * Jitter, (float)(random.NextDouble() * 2 - 1) * Jitter);
            CladdingRock rock;
            bool placed = TryPlace(window, lo, hi, level, scale, sinkFraction, jitter, 0f, up, false, reserved, heights, heightScale, worldSize, originXZ, out rock);
            if (!placed && reserved != null)
            {
                // Near-trail variant: smaller rock, pushed into (or away from) the face until its footprint clears the walkway.
                scale = NearMinScale + (float)random.NextDouble() * (NearMaxScale - NearMinScale);
                float remainingNear = hi - level;
                if (remainingNear < 0.7f * scale * HeightPerScale - RimOverhang)
                {
                    scale = Mathf.Max(NearMinScale, (remainingNear + RimOverhang) / (0.7f * HeightPerScale));
                }

                float[] shifts = new float[] { 0f, 0.25f, 0.5f, -0.25f, -0.5f };
                for (int k = 0; k < shifts.Length && !placed; k++)
                {
                    placed = TryPlace(window, lo, hi, level, scale, sinkFraction, jitter, shifts[k] * scale, up, true, reserved, heights, heightScale, worldSize, originXZ, out rock);
                }
            }

            level += RowStep * scale * HeightPerScale;
            if (!placed)
            {
                continue;
            }

            if (excluded != null && excluded(new Vector2(rock.position.x, rock.position.z)))
            {
                // A gap in the stack would leave rocks above it hanging, so the whole column is dropped.
                return;
            }

            float outward = Mathf.Atan2(outwardDir.x, outwardDir.y) * Mathf.Rad2Deg;
            rock.yaw = outward + ((float)random.NextDouble() * 2f - 1f) * 50f;
            rock.tilt = (float)random.NextDouble() * 12f;
            rock.variant = random.Next(0, VariantCount);
            column.Add(rock);
        }

        result.AddRange(column);
    }

    // shiftUp > 0 moves the rock into the face (uphill), < 0 away from it.
    static bool TryPlace(List<Vector3> window, float lo, float hi, float level, float scale, float sinkFraction, Vector2 jitter, float shiftUp, Vector2 up, bool adapt,
        Func<Vector2, float> reserved, float[,] heights, float heightScale, float worldSize, Vector2 originXZ, out CladdingRock rock)
    {
        float rockHeight = scale * HeightPerScale;
        float target = Mathf.Clamp(level + 0.3f * rockHeight, lo, hi);
        Vector3 best = window[0];
        for (int i = 1; i < window.Count; i++)
        {
            if (Mathf.Abs(window[i].y - target) < Mathf.Abs(best.y - target))
            {
                best = window[i];
            }
        }

        Vector2 xz = new Vector2(best.x, best.z) - up * (0.05f * scale) + jitter + up * shiftUp;
        float baseY = target - sinkFraction * rockHeight;
        if (level + RowStep * rockHeight >= hi - 0.3f)
        {
            // Last row: lift it so its top reaches the rim, otherwise a strip of stretched texture shows above the stack.
            baseY = Mathf.Max(baseY, hi - rockHeight + 0.15f);
        }

        rock = new CladdingRock();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            float walkway = BlockingWalkway(reserved, heights, heightScale, worldSize, originXZ, xz, scale, baseY, adapt);
            if (float.IsNaN(walkway))
            {
                // Sit at the target height, not the nearest profile sample: a near-vertical face has samples only at its foot and rim.
                rock.position = new Vector3(xz.x, baseY, xz.y);
                rock.scale = scale;
                return true;
            }

            // A near-trail rock may be sunk deeper so its top ends just under a walkway that runs along the rim, instead of being refused.
            float lowered = walkway - rockHeight - 0.05f;
            if (!adapt || walkway <= baseY || lowered < lo - 0.5f * rockHeight)
            {
                break;
            }

            baseY = lowered;
        }

        rock.position = new Vector3(xz.x, baseY, xz.y);
        rock.scale = scale;
        return false;
    }

    // Highest walkway height under the rock's footprint whose walker column the rock body overlaps; NaN when none.
    static float BlockingWalkway(Func<Vector2, float> reserved, float[,] heights, float heightScale, float worldSize, Vector2 originXZ, Vector2 xz, float scale, float baseY, bool adapt)
    {
        float result = float.NaN;
        if (reserved == null)
        {
            return result;
        }

        float rockHeight = scale * HeightPerScale;
        float radius = (adapt ? NearFootprintRadius : FootprintRadius) * scale;
        for (int i = -1; i < 20; i++)
        {
            Vector2 p = xz;
            if (i >= 0)
            {
                float angle = (i % 12) * Mathf.PI / 6f;
                float ring = i < 12 ? radius : radius * 0.5f;
                p += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
            }

            float walkway = reserved(p);
            if (float.IsNaN(walkway))
            {
                continue;
            }

            // The rock blocks when its body overlaps the walker's column above the walkway at that point.
            if (walkway > baseY - WalkerHeight && walkway < baseY + rockHeight && (float.IsNaN(result) || walkway > result))
            {
                result = walkway;
            }
        }

        return result;
    }

    public static float SurfaceY(float[,] heights, float heightScale, float worldSize, Vector2 originXZ, Vector2 xz)
    {
        int rows = heights.GetLength(0);
        int cols = heights.GetLength(1);
        float cell = worldSize / (cols - 1);
        float fx = Mathf.Clamp((xz.x - originXZ.x) / cell, 0f, cols - 1.001f);
        float fz = Mathf.Clamp((xz.y - originXZ.y) / cell, 0f, rows - 1.001f);
        int x0 = (int)fx;
        int z0 = (int)fz;
        float tx = fx - x0;
        float tz = fz - z0;
        float a = Mathf.Lerp(heights[z0, x0], heights[z0, x0 + 1], tx);
        float b = Mathf.Lerp(heights[z0 + 1, x0], heights[z0 + 1, x0 + 1], tx);
        return Mathf.Lerp(a, b, tz) * heightScale;
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
                if (Gradient(heights, heightScale, cell, px, pz).magnitude <= FaceGradient)
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
