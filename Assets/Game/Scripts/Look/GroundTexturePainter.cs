using UnityEngine;

namespace LanternKeeper
{
// Paints tileable ground textures from an island palette. Each layer is three close tones blended by soft low-frequency noise,
// then painted over with surface detail at several scales (pebbles and trodden streaks on dirt, grain and ripples on sand,
// cracks and facets on rock, clumps and litter on grass and moss). A height map carries the relief and the normal map is derived from it.
// Everything wraps at the texture edges and is seeded, so a rebuild is byte-identical. Runtime-safe so tests and the editor builder share it.
public static class GroundTexturePainter
{
    public const int Size = 512;
    // Normal map slope multiplier applied to the painted height map (about 1.0 to 2.0 reads well under the moon and lantern).
    public const float NormalStrength = 1.5f;

    // Pure pixel generation (no assets touched), so tests can check tiling, determinism and tone.
    public static void Generate(string layerName, Color baseColour, int seed, out Color32[] albedo, out Color32[] normal)
    {
        int count = Size * Size;
        float[] blob = BuildNoise(seed);
        Color[] tones = LookMapping.GroundTones(baseColour);
        float[] tone = new float[count];
        float[] height = new float[count];
        Paint(layerName, seed, tone, height);

        // Medium-scale colour variation (about 1 to 3 m at the layer tile size), on top of the large blobs.
        float[] medium = Fbm(5, 3, seed * 3 + 5);
        float[] mediumLarge = Fbm(3, 2, seed * 5 + 11);
        albedo = new Color32[count];
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Clamp01(blob[i] + (medium[i] - 0.5f) * 0.45f + (mediumLarge[i] - 0.5f) * 0.3f);
            Color c = t < 0.5f ? Color.Lerp(tones[0], tones[1], t * 2f) : Color.Lerp(tones[1], tones[2], (t - 0.5f) * 2f);
            float m = Mathf.Max(0f, 1f + tone[i]);
            albedo[i] = new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), 1f);
        }

        normal = new Color32[count];
        float scale = NormalStrength * 2f;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float l = height[y * Size + (x + Size - 1) % Size];
                float r = height[y * Size + (x + 1) % Size];
                float d = height[((y + Size - 1) % Size) * Size + x];
                float u = height[((y + 1) % Size) * Size + x];
                Vector3 n = new Vector3((l - r) * scale, (d - u) * scale, 1f).normalized;
                normal[y * Size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }
    }

    // Adds detail to tone (a multiplier offset around 0) and height (relief, roughly 0..1).
    static void Paint(string layer, int seed, float[] tone, float[] height)
    {
        int s = seed * 17 + 3;
        switch (layer)
        {
            case "dirt":
                PaintDirt(s, tone, height);
                break;
            case "sand":
                PaintSand(s, tone, height);
                break;
            case "rock":
                PaintRock(s, tone, height);
                break;
            case "moss":
                PaintGround(s, tone, height, 14, 0.8f);
                break;
            default:
                PaintGround(s, tone, height, 11, 1f);
                break;
        }
    }

    static void PaintDirt(int seed, float[] tone, float[] height)
    {
        float[] soft = Fbm(6, 3, seed + 1);
        float[] brushA = LatticeXY(18, 4, seed + 2);
        float[] brushB = LatticeXY(5, 22, seed + 3);
        float[] streak = LatticeXY(3, 40, seed + 4);
        float[] streakFine = LatticeXY(6, 90, seed + 5);
        for (int i = 0; i < tone.Length; i++)
        {
            float trod = SmoothStep(0.55f, 0.85f, streak[i]);
            float trodFine = SmoothStep(0.62f, 0.9f, streakFine[i]);
            tone[i] = (soft[i] - 0.5f) * 0.2f + (brushA[i] - 0.5f) * 0.2f + (brushB[i] - 0.5f) * 0.2f - trod * 0.2f - trodFine * 0.1f;
            height[i] = 0.25f + (soft[i] - 0.5f) * 0.15f - trod * 0.12f + (brushA[i] - 0.5f) * 0.05f;
        }

        System.Random rng = new System.Random(seed + 6);
        Scatter(tone, height, rng, 420, 2.2f, 6f, 0.4f, 0.45f, 0.55f);
        Scatter(tone, height, rng, 900, 0.8f, 2f, 0.32f, 0.5f, 0.3f);
    }

    static void PaintSand(int seed, float[] tone, float[] height)
    {
        float[] dunes = Fbm(4, 3, seed + 1);
        float[] warp = Fbm(3, 2, seed + 2);
        float[] fine = Lattice(170, seed + 3);
        float[] mid = Lattice(64, seed + 4);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                float phase = (y * 8f + x * 2f) / Size + (warp[i] - 0.5f) * 1.6f + (dunes[i] - 0.5f) * 0.6f;
                float ripple = Mathf.Sin(phase * Mathf.PI * 2f);
                float band = SmoothStep(-0.2f, 0.9f, ripple);
                float grain = Hash01(x, y, seed) - 0.5f;
                tone[i] = ripple * 0.07f + (dunes[i] - 0.5f) * 0.18f + grain * 0.14f + (fine[i] - 0.5f) * 0.1f + (mid[i] - 0.5f) * 0.08f;
                height[i] = 0.2f + band * 0.35f + (dunes[i] - 0.5f) * 0.15f + grain * 0.1f + (fine[i] - 0.5f) * 0.1f;
            }
        }

        System.Random rng = new System.Random(seed + 5);
        Scatter(tone, height, rng, 140, 1f, 2.6f, 0.3f, 0.5f, 0.4f);
    }

    // Natural rough ground: mottled earth and eroded rock patches, clustered stones with lit tops and dark undersides, short broken cracks.
    static void PaintRock(int seed, float[] tone, float[] height)
    {
        float[] warpA = Fbm(3, 2, seed + 1);
        float[] warpB = Fbm(3, 2, seed + 2);
        float[] patch = Fbm(4, 4, seed + 3);
        float[] lumps = Fbm(11, 3, seed + 4);
        float[] erode = Fbm(26, 2, seed + 5);
        float[] brush = LatticeXY(14, 5, seed + 6);
        float[] fine = Lattice(150, seed + 7);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                // Warped patch mask: soft rock areas inside earth.
                int sx = Wrap(x + Mathf.RoundToInt((warpA[i] - 0.5f) * 120f));
                int sy = Wrap(y + Mathf.RoundToInt((warpB[i] - 0.5f) * 120f));
                float m = SmoothStep(0.38f, 0.66f, patch[sy * Size + sx]);
                float rockiness = m * (0.6f + 0.4f * SmoothStep(0.35f, 0.7f, erode[i]));
                tone[i] = (m - 0.5f) * 0.22f + (lumps[i] - 0.5f) * 0.24f + (erode[i] - 0.5f) * 0.2f * (0.5f + m) + (brush[i] - 0.5f) * 0.12f + (fine[i] - 0.5f) * 0.1f;
                height[i] = 0.25f + rockiness * 0.22f + (lumps[i] - 0.5f) * 0.4f + (erode[i] - 0.5f) * 0.12f + (fine[i] - 0.5f) * 0.1f;
            }
        }

        System.Random rng = new System.Random(seed + 8);
        // Clusters of stones, plus a thin even sprinkle.
        for (int c = 0; c < 22; c++)
        {
            float cx = (float)rng.NextDouble() * Size;
            float cy = (float)rng.NextDouble() * Size;
            float spread = 30f + (float)rng.NextDouble() * 50f;
            int n = 8 + rng.Next(14);
            for (int k = 0; k < n; k++)
            {
                float px = cx + Gauss(rng) * spread;
                float py = cy + Gauss(rng) * spread;
                float r = 2.5f + (float)rng.NextDouble() * (float)rng.NextDouble() * 16f;
                Stone(tone, height, rng, px, py, r);
            }
        }

        for (int k = 0; k < 90; k++)
        {
            Stone(tone, height, rng, (float)rng.NextDouble() * Size, (float)rng.NextDouble() * Size, 2f + (float)rng.NextDouble() * 5f);
        }

        Scatter(tone, height, rng, 500, 0.8f, 2f, 0.28f, 0.5f, 0.3f);
        for (int k = 0; k < 22; k++)
        {
            Crack(tone, height, rng, (float)rng.NextDouble() * Size, (float)rng.NextDouble() * Size, (float)rng.NextDouble() * Mathf.PI * 2f, 22f + (float)rng.NextDouble() * 40f, 0);
        }
    }

    static float Gauss(System.Random rng)
    {
        return (float)(rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5);
    }

    // One irregular stone: lit on its upper side, dark underneath, with a small contact shadow below it.
    static void Stone(float[] tone, float[] height, System.Random rng, float cx, float cy, float r)
    {
        float squash = 0.65f + (float)rng.NextDouble() * 0.35f;
        float p1 = (float)rng.NextDouble() * 6.28f;
        float p2 = (float)rng.NextDouble() * 6.28f;
        float own = ((float)rng.NextDouble() - 0.5f) * 0.3f;
        float top = 0.3f + (float)rng.NextDouble() * 0.15f;
        int ir = Mathf.CeilToInt(r * 1.4f) + 2;
        for (int oy = -ir; oy <= ir; oy++)
        {
            for (int ox = -ir; ox <= ir; ox++)
            {
                int px = Mathf.FloorToInt(cx) + ox;
                int py = Mathf.FloorToInt(cy) + oy;
                float dx = px + 0.5f - cx;
                float dy = py + 0.5f - cy;
                float ang = Mathf.Atan2(dy, dx);
                float rr = r * (1f + 0.18f * Mathf.Sin(3f * ang + p1) + 0.1f * Mathf.Sin(5f * ang + p2));
                float u = Mathf.Sqrt(dx * dx + dy * dy / (squash * squash)) / rr;
                int i = Wrap(py) * Size + Wrap(px);
                if (u >= 1f)
                {
                    // Contact shadow just below the stone (texture y is up, so below is smaller y).
                    if (u < 1.35f && dy < 0f)
                    {
                        tone[i] -= 0.1f * (1.35f - u) / 0.35f;
                    }

                    continue;
                }

                float dome = Mathf.Sqrt(1f - u * u);
                float light = dy / (rr * squash);
                tone[i] = own + light * 0.18f + (dome - 0.5f) * 0.12f - (u > 0.8f ? (u - 0.8f) * 0.5f : 0f);
                height[i] = Mathf.Max(height[i], 0.22f + top * dome);
            }
        }
    }

    // Short broken crack: a wobbling walk that sometimes forks once. Never encloses cells.
    static void Crack(float[] tone, float[] height, System.Random rng, float x, float y, float dir, float length, int depth)
    {
        for (float s = 0f; s < length; s += 1f)
        {
            dir += ((float)rng.NextDouble() - 0.5f) * 0.5f;
            x += Mathf.Cos(dir);
            y += Mathf.Sin(dir);
            float fade = 1f - s / length;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    float w = (ox == 0 && oy == 0 ? 1f : 0.45f) * fade;
                    int i = Wrap(Mathf.FloorToInt(y) + oy) * Size + Wrap(Mathf.FloorToInt(x) + ox);
                    tone[i] -= 0.22f * w;
                    height[i] -= 0.2f * w;
                }
            }

            if (depth < 1 && s > 6f && rng.NextDouble() < 0.04)
            {
                Crack(tone, height, rng, x, y, dir + (rng.NextDouble() < 0.5 ? 0.7f : -0.7f), length * 0.5f, depth + 1);
            }
        }
    }

    // Grass ground and moss: clumpy patches with dark gaps between them, plus small leaf-litter specks.
    static void PaintGround(int seed, float[] tone, float[] height, int cells, float amount)
    {
        Cells clumps = Worley(cells, seed + 1);
        float[] patch = Fbm(7, 3, seed + 2);
        float[] brush = LatticeXY(16, 6, seed + 3);
        float[] fine = Lattice(96, seed + 4);
        System.Random rng = new System.Random(seed + 5);
        float[] cellTone = new float[cells * cells];
        for (int i = 0; i < cellTone.Length; i++)
        {
            cellTone[i] = (float)rng.NextDouble() - 0.5f;
        }

        for (int i = 0; i < tone.Length; i++)
        {
            float dome = 1f - SmoothStep(0f, 0.75f, clumps.f1[i]);
            float gap = 1f - SmoothStep(0f, 0.12f, clumps.f2[i] - clumps.f1[i]);
            tone[i] = amount * ((patch[i] - 0.5f) * 0.3f + (brush[i] - 0.5f) * 0.16f + (fine[i] - 0.5f) * 0.14f + cellTone[clumps.id[i]] * 0.2f + (dome - 0.5f) * 0.12f - gap * 0.07f);
            height[i] = 0.25f + dome * 0.3f - gap * 0.08f + (patch[i] - 0.5f) * 0.15f + (fine[i] - 0.5f) * 0.12f;
        }

        Scatter(tone, height, rng, 900, 0.8f, 2.6f, 0.34f * amount, 0.45f, 0.35f);
    }

    // Drops round specks and pebbles. A dark rim gives each one a little contact shadow. Wraps at the edges.
    static void Scatter(float[] tone, float[] height, System.Random rng, int count, float rMin, float rMax, float toneAmp, float darkChance, float heightAmp)
    {
        for (int n = 0; n < count; n++)
        {
            float cx = (float)rng.NextDouble() * Size;
            float cy = (float)rng.NextDouble() * Size;
            float r = rMin + (float)rng.NextDouble() * (rMax - rMin);
            bool dark = rng.NextDouble() < darkChance;
            float amp = (0.55f + (float)rng.NextDouble() * 0.45f) * toneAmp * (dark ? -1f : 1f);
            int ir = Mathf.CeilToInt(r) + 1;
            for (int oy = -ir; oy <= ir; oy++)
            {
                for (int ox = -ir; ox <= ir; ox++)
                {
                    int px = Mathf.FloorToInt(cx) + ox;
                    int py = Mathf.FloorToInt(cy) + oy;
                    float dx = px + 0.5f - cx;
                    float dy = py + 0.5f - cy;
                    float u = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (u >= 1f)
                    {
                        continue;
                    }

                    int i = Wrap(py) * Size + Wrap(px);
                    float dome = Mathf.Sqrt(1f - u * u);
                    float rim = u > 0.7f ? (u - 0.7f) / 0.3f * 0.12f : 0f;
                    tone[i] = amp * (0.65f + 0.35f * dome) - rim;
                    height[i] = Mathf.Max(height[i], 0.2f + heightAmp * dome);
                }
            }
        }
    }

    static int Wrap(int v)
    {
        return ((v % Size) + Size) % Size;
    }

    static float SmoothStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // Deterministic per-pixel white noise in 0..1.
    static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // Tileable cellular noise: nearest and second nearest feature point distance (in cell units), the nearest cell id and the offset from its point.
    class Cells
    {
        public float[] f1;
        public float[] f2;
        public float[] dx;
        public float[] dy;
        public int[] id;
    }

    static Cells Worley(int cells, int seed, float warp = 0f)
    {
        System.Random rng = new System.Random(seed);
        float[] px = new float[cells * cells];
        float[] py = new float[cells * cells];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = 0.15f + (float)rng.NextDouble() * 0.7f;
            py[i] = 0.15f + (float)rng.NextDouble() * 0.7f;
        }

        Cells result = new Cells();
        int count = Size * Size;
        result.f1 = new float[count];
        result.f2 = new float[count];
        result.dx = new float[count];
        result.dy = new float[count];
        result.id = new int[count];
        float cellSize = Size / (float)cells;
        float[] warpX = warp > 0f ? Lattice(cells * 2, seed * 13 + 1) : null;
        float[] warpY = warp > 0f ? Lattice(cells * 2, seed * 13 + 2) : null;
        for (int y = 0; y < Size; y++)
        {
            float baseY = (y + 0.5f) / cellSize;
            for (int x = 0; x < Size; x++)
            {
                float fx = (x + 0.5f) / cellSize;
                float fy = baseY;
                if (warp > 0f)
                {
                    fx += (warpX[y * Size + x] - 0.5f) * warp * 2f;
                    fy += (warpY[y * Size + x] - 0.5f) * warp * 2f;
                }

                int cy = Mathf.FloorToInt(fy);
                int cx = Mathf.FloorToInt(fx);
                float best = float.MaxValue;
                float second = float.MaxValue;
                int bestId = 0;
                float bdx = 0f;
                float bdy = 0f;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int wx = ((cx + ox) % cells + cells) % cells;
                        int wy = ((cy + oy) % cells + cells) % cells;
                        int cid = wy * cells + wx;
                        float ddx = fx - (cx + ox + px[cid]);
                        float ddy = fy - (cy + oy + py[cid]);
                        float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                        if (d < best)
                        {
                            second = best;
                            best = d;
                            bestId = cid;
                            bdx = ddx;
                            bdy = ddy;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }
                }

                int i = y * Size + x;
                result.f1[i] = best;
                result.f2[i] = second;
                result.dx[i] = bdx;
                result.dy[i] = bdy;
                result.id[i] = bestId;
            }
        }

        return result;
    }

    // Fractal value noise in about 0..1: each octave doubles the cells and halves the weight.
    static float[] Fbm(int baseCells, int octaves, int seed)
    {
        float[] result = new float[Size * Size];
        float weight = 1f;
        float total = 0f;
        int cells = baseCells;
        for (int o = 0; o < octaves; o++)
        {
            float[] layer = Lattice(cells, seed * 31 + o * 7 + 1);
            for (int i = 0; i < result.Length; i++)
            {
                result[i] += layer[i] * weight;
            }

            total += weight;
            weight *= 0.5f;
            cells *= 2;
        }

        for (int i = 0; i < result.Length; i++)
        {
            result[i] /= total;
        }

        return result;
    }

    // Two octaves of tileable value noise: 4x4 cells (about a quarter of the texture) and 8x8 at lower weight.
    static float[] BuildNoise(int seed)
    {
        float[] a = Lattice(4, seed);
        float[] b = Lattice(8, seed * 7 + 13);
        float[] result = new float[Size * Size];
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < result.Length; i++)
        {
            float v = a[i] * 0.7f + b[i] * 0.3f;
            result[i] = v;
            min = Mathf.Min(min, v);
            max = Mathf.Max(max, v);
        }

        float range = Mathf.Max(0.0001f, max - min);
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (result[i] - min) / range;
        }

        return result;
    }

    static float[] Lattice(int cells, int seed)
    {
        return LatticeXY(cells, cells, seed);
    }

    // Tileable value noise with a different cell count per axis (stretched cells give streaks and brush strokes).
    static float[] LatticeXY(int cellsX, int cellsY, int seed)
    {
        float[] grid = new float[cellsX * cellsY];
        System.Random rng = new System.Random(seed);
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = (float)rng.NextDouble();
        }

        float[] result = new float[Size * Size];
        float cellW = Size / (float)cellsX;
        float cellH = Size / (float)cellsY;
        for (int y = 0; y < Size; y++)
        {
            float fy = y / cellH;
            int y0 = Mathf.FloorToInt(fy);
            float ty = Smooth(fy - y0);
            int y1 = (y0 + 1) % cellsY;
            y0 %= cellsY;
            for (int x = 0; x < Size; x++)
            {
                float fx = x / cellW;
                int x0 = Mathf.FloorToInt(fx);
                float tx = Smooth(fx - x0);
                int x1 = (x0 + 1) % cellsX;
                x0 %= cellsX;
                float top = Mathf.Lerp(grid[y0 * cellsX + x0], grid[y0 * cellsX + x1], tx);
                float bottom = Mathf.Lerp(grid[y1 * cellsX + x0], grid[y1 * cellsX + x1], tx);
                result[y * Size + x] = Mathf.Lerp(top, bottom, ty);
            }
        }

        return result;
    }

    static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

}
}
