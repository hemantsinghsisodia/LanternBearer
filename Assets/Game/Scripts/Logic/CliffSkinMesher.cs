using System;
using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
// Raw mesh data for one culling chunk of the cliff skin (no UnityEngine.Mesh, so it stays pure and testable).
public class SkinChunk
{
    public Vector3[] vertices;
    public Vector3[] normals;
    public Vector2[] uvs;
    public int[] triangles;
}

// Pure mesh builder for the cliff skin. Each strip becomes a wall: bottom a little under the foot, top just over the rim, with a small
// inward lip that rolls over the rim and hides the jagged top edge. Gentle low-frequency bulges (seeded value noise along arc length and
// height) keep it from reading as a flat sheet. UVs are world metres: u = arc length, v = height (lip: height of the top plus the distance
// travelled over the lip), so the material tiling matches the terrain's rock shading.
public static class CliffSkinMesher
{
    public const float BottomSink = 0.3f;        // wall bottom below the lower of foot / ground
    public const float RowStep = 0.5f;           // vertical resolution
    public const float ColumnStep = 0.45f;       // horizontal resolution (columns also split at corners)
    public const float CornerAngle = 12f;        // degrees of turning that force a column
    public const float LipRise = 0.08f;          // wall top above the rim (hides the rim zig-zag; keep within 0.05-0.15)
    public const float TuckCurl = 0.13f;         // row 1: the top edge curls this far inward from the wall top, a hair lower
    public const float TuckDepth = 0.30f;        // row 2: this far behind the skin line (0.05 m inside the step edge), buried below the rim
    public const float TuckSink = 0.3f;          // row 2 sits this far below the rim ground, so no rock lies flat on walkable ground
    public const float BulgeMin = -0.07f;        // bulges never move the wall inward past the face (it sits 0.25 m out)
    public const float BulgeMax = 0.15f;
    public const float NoiseWavelengthArc = 3f;
    public const float NoiseWavelengthHeight = 2.2f;
    public const float SectionLength = 24f;      // a strip is cut into sections of at most this length
    public const float ChunkSize = 32f;          // sections are grouped by this grid for culling

    class Grid
    {
        public Vector3[][] positions;   // [column][row]
        public Vector3[][] normals;
        public Vector2[][] uvs;
        public float[] arc;
        public bool closed;
    }

    class ChunkBuilder
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<int> triangles = new List<int>();
    }

    // ground: world height of the terrain at a plan point. originY: world y of the terrain origin (strip heights are relative to it).
    public static List<SkinChunk> Build(List<SkinStrip> strips, Func<Vector2, float> ground, float originY, int seed)
    {
        SortedDictionary<long, ChunkBuilder> chunks = new SortedDictionary<long, ChunkBuilder>();
        for (int s = 0; s < strips.Count; s++)
        {
            Grid grid = BuildGrid(strips[s], ground, originY, seed + s * 7919);
            if (grid == null)
            {
                continue;
            }

            AddSections(grid, chunks);
        }

        List<SkinChunk> result = new List<SkinChunk>();
        foreach (KeyValuePair<long, ChunkBuilder> pair in chunks)
        {
            ChunkBuilder b = pair.Value;
            if (b.triangles.Count == 0)
            {
                continue;
            }

            SkinChunk chunk = new SkinChunk();
            chunk.vertices = b.vertices.ToArray();
            chunk.normals = b.normals.ToArray();
            chunk.uvs = b.uvs.ToArray();
            chunk.triangles = b.triangles.ToArray();
            result.Add(chunk);
        }

        return result;
    }

    static Grid BuildGrid(SkinStrip strip, Func<Vector2, float> ground, float originY, int seed)
    {
        List<SkinPoint> pts = strip.points;
        int n = pts.Count;
        if (n < 3)
        {
            return null;
        }

        // Cumulative arc length of the strip's own polyline.
        float[] arcAll = new float[n + 1];
        for (int i = 1; i < n; i++)
        {
            arcAll[i] = arcAll[i - 1] + Vector2.Distance(pts[i - 1].position, pts[i].position);
        }

        arcAll[n] = arcAll[n - 1] + (strip.closed ? Vector2.Distance(pts[n - 1].position, pts[0].position) : 0f);
        float totalArc = arcAll[n];

        // Pick the columns: every ColumnStep metres, plus wherever the polyline turns.
        List<int> picked = new List<int>();
        picked.Add(0);
        float lastArc = 0f;
        Vector2 lastDir = Direction(pts, 0, strip.closed);
        for (int i = 1; i < n; i++)
        {
            Vector2 dir = Direction(pts, i, strip.closed);
            bool last = !strip.closed && i == n - 1;
            if (last || arcAll[i] - lastArc >= ColumnStep || Vector2.Angle(lastDir, dir) >= CornerAngle)
            {
                picked.Add(i);
                lastArc = arcAll[i];
                lastDir = dir;
            }
        }

        int columns = picked.Count + (strip.closed ? 1 : 0);
        if (columns < 2)
        {
            return null;
        }

        float[] bottom = new float[columns];
        float[] top = new float[columns];
        float maxHeight = 0f;
        for (int c = 0; c < columns; c++)
        {
            SkinPoint p = pts[picked[c % picked.Count]];
            float footWorld = originY + p.foot;
            float low = Mathf.Min(footWorld, Mathf.Min(ground(p.position), ground(p.position + p.normal * 0.3f)));
            bottom[c] = low - BottomSink;
            top[c] = originY + p.rim + LipRise;
            maxHeight = Mathf.Max(maxHeight, top[c] - bottom[c]);
        }

        int wallRows = Mathf.Max(2, Mathf.CeilToInt(maxHeight / RowStep));
        int rowCount = wallRows + 1 + 2; // wall rows, then two lip rows
        Grid grid = new Grid();
        grid.closed = strip.closed;
        grid.positions = new Vector3[columns][];
        grid.normals = new Vector3[columns][];
        grid.uvs = new Vector2[columns][];
        grid.arc = new float[columns];
        int noiseCells = strip.closed ? Mathf.Max(1, Mathf.RoundToInt(totalArc / NoiseWavelengthArc)) : 0;

        for (int c = 0; c < columns; c++)
        {
            int src = picked[c % picked.Count];
            SkinPoint p = pts[src];
            float arc = c < picked.Count ? arcAll[src] : totalArc;
            grid.arc[c] = arc;
            grid.positions[c] = new Vector3[rowCount];
            grid.uvs[c] = new Vector2[rowCount];
            for (int r = 0; r <= wallRows; r++)
            {
                float y = Mathf.Lerp(bottom[c], top[c], r / (float)wallRows);
                float b = Bulge(arc, y, totalArc, noiseCells, seed);
                Vector2 q = p.position + p.normal * b;
                grid.positions[c][r] = new Vector3(q.x, y, q.y);
                grid.uvs[c][r] = new Vector2(arc, y);
            }

            // Tuck: the top edge curls a little inward and then drops steeply into the terrain, so nothing lies flat on the rim.
            float topBulge = Bulge(arc, top[c], totalArc, noiseCells, seed);
            Vector2 outer = p.position + p.normal * topBulge;
            Vector2 lip1 = outer - p.normal * TuckCurl;
            Vector2 lip2 = p.position - p.normal * TuckDepth;
            float rimY = top[c] - LipRise;
            float y1 = top[c] - 0.03f;
            float y2 = Mathf.Min(rimY, ground(lip2)) - TuckSink;
            grid.positions[c][wallRows + 1] = new Vector3(lip1.x, y1, lip1.y);
            grid.positions[c][wallRows + 2] = new Vector3(lip2.x, y2, lip2.y);
            float travel1 = Vector2.Distance(outer, lip1);
            float travel2 = travel1 + Vector2.Distance(lip1, lip2);
            grid.uvs[c][wallRows + 1] = new Vector2(arc, top[c] + travel1);
            grid.uvs[c][wallRows + 2] = new Vector2(arc, top[c] + travel2);
        }

        ComputeNormals(grid, columns, rowCount);
        return grid;
    }

    static Vector2 Direction(List<SkinPoint> pts, int i, bool closed)
    {
        int n = pts.Count;
        Vector2 prev = pts[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)].position;
        Vector2 next = pts[closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1)].position;
        Vector2 d = next - prev;
        return d.sqrMagnitude > 1e-12f ? d.normalized : Vector2.right;
    }

    // Area-weighted smooth normals over the whole strip grid; the closing column of a loop shares the first column's normals.
    static void ComputeNormals(Grid grid, int columns, int rowCount)
    {
        grid.normals = new Vector3[columns][];
        for (int c = 0; c < columns; c++)
        {
            grid.normals[c] = new Vector3[rowCount];
        }

        for (int c = 0; c < columns - 1; c++)
        {
            for (int r = 0; r < rowCount - 1; r++)
            {
                Vector3 a = grid.positions[c][r];
                Vector3 b = grid.positions[c][r + 1];
                Vector3 d = grid.positions[c + 1][r];
                Vector3 e = grid.positions[c + 1][r + 1];
                Vector3 n1 = Vector3.Cross(b - a, d - a);
                Vector3 n2 = Vector3.Cross(b - d, e - d);
                grid.normals[c][r] += n1 + n2;
                grid.normals[c][r + 1] += n1 + n2;
                grid.normals[c + 1][r] += n1 + n2;
                grid.normals[c + 1][r + 1] += n1 + n2;
            }
        }

        if (grid.closed)
        {
            for (int r = 0; r < rowCount; r++)
            {
                Vector3 sum = grid.normals[0][r] + grid.normals[columns - 1][r];
                grid.normals[0][r] = sum;
                grid.normals[columns - 1][r] = sum;
            }
        }

        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rowCount; r++)
            {
                Vector3 v = grid.normals[c][r];
                grid.normals[c][r] = v.sqrMagnitude > 1e-12f ? v.normalized : Vector3.up;
            }
        }
    }

    static void AddSections(Grid grid, SortedDictionary<long, ChunkBuilder> chunks)
    {
        int columns = grid.positions.Length;
        int start = 0;
        while (start < columns - 1)
        {
            int end = start + 1;
            while (end < columns - 1 && grid.arc[end] - grid.arc[start] < SectionLength)
            {
                end++;
            }

            // Avoid a tiny trailing section: fold it into this one.
            if (columns - 1 - end > 0 && grid.arc[columns - 1] - grid.arc[end] < SectionLength * 0.25f)
            {
                end = columns - 1;
            }

            int mid = (start + end) / 2;
            Vector3 centre = grid.positions[mid][grid.positions[mid].Length / 2];
            long key = ((long)Mathf.FloorToInt(centre.x / ChunkSize) << 32) ^ (long)(uint)Mathf.FloorToInt(centre.z / ChunkSize);
            ChunkBuilder b;
            if (!chunks.TryGetValue(key, out b))
            {
                b = new ChunkBuilder();
                chunks[key] = b;
            }

            AppendSection(grid, start, end, b);
            start = end;
        }
    }

    static void AppendSection(Grid grid, int c0, int c1, ChunkBuilder b)
    {
        int rowCount = grid.positions[0].Length;
        int baseIndex = b.vertices.Count;
        for (int c = c0; c <= c1; c++)
        {
            for (int r = 0; r < rowCount; r++)
            {
                b.vertices.Add(grid.positions[c][r]);
                b.normals.Add(grid.normals[c][r]);
                b.uvs.Add(grid.uvs[c][r]);
            }
        }

        for (int c = 0; c < c1 - c0; c++)
        {
            for (int r = 0; r < rowCount - 1; r++)
            {
                int a = baseIndex + c * rowCount + r;
                int up = a + 1;
                int next = a + rowCount;
                int nextUp = next + 1;
                b.triangles.Add(a);
                b.triangles.Add(up);
                b.triangles.Add(next);
                b.triangles.Add(next);
                b.triangles.Add(up);
                b.triangles.Add(nextUp);
            }
        }
    }

    // Outward bulge in metres, in [BulgeMin, BulgeMax]. noiseCells > 0 makes the noise wrap along a closed loop.
    public static float Bulge(float arc, float height, float totalArc, int noiseCells, int seed)
    {
        float u;
        int wrap = 0;
        if (noiseCells > 0)
        {
            u = totalArc > 1e-4f ? arc / totalArc * noiseCells : 0f;
            wrap = noiseCells;
        }
        else
        {
            u = arc / NoiseWavelengthArc;
        }

        float v = height / NoiseWavelengthHeight;
        float n = ValueNoise(u, v, wrap, seed) * 0.7f + ValueNoise(u * 2f + 17f, v * 2.1f + 5.1f, wrap > 0 ? wrap * 2 : 0, seed + 101) * 0.3f;
        return Mathf.Lerp(BulgeMin, BulgeMax, Mathf.Clamp01(n));
    }

    // wrap > 0: lattice x indices repeat every `wrap` cells (the noise is periodic in u).
    static float ValueNoise(float u, float v, int wrap, int seed)
    {
        int iu = Mathf.FloorToInt(u);
        int iv = Mathf.FloorToInt(v);
        float fu = u - iu;
        float fv = v - iv;
        fu = fu * fu * (3f - 2f * fu);
        fv = fv * fv * (3f - 2f * fv);
        float a = Lattice(iu, iv, wrap, seed);
        float b = Lattice(iu + 1, iv, wrap, seed);
        float c = Lattice(iu, iv + 1, wrap, seed);
        float d = Lattice(iu + 1, iv + 1, wrap, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fu), Mathf.Lerp(c, d, fu), fv);
    }

    static float Lattice(int ix, int iy, int wrap, int seed)
    {
        if (wrap > 0)
        {
            ix = ((ix % wrap) + wrap) % wrap;
        }

        unchecked
        {
            uint h = (uint)(ix * 374761393 + iy * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
}
