using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
// Accumulates vertex coloured geometry (sRGB Color32 vertex colours) and turns it into one mesh. Used by the finale's miniature islands.
public class FinaleMeshBuilder
{
    public readonly List<Vector3> Vertices = new List<Vector3>();
    public readonly List<Color32> Colors = new List<Color32>();
    public readonly List<int> Triangles = new List<int>();

    public int TriangleCount { get { return Triangles.Count / 3; } }

    public int AddVertex(Vector3 position, Color32 color)
    {
        Vertices.Add(position);
        Colors.Add(color);
        return Vertices.Count - 1;
    }

    public void AddTriangle(int a, int b, int c)
    {
        Triangles.Add(a);
        Triangles.Add(b);
        Triangles.Add(c);
    }

    // Appends a mesh given by its arrays, transformed by `matrix`, in one colour.
    public void AddMesh(IList<Vector3> vertices, IList<int> triangles, Matrix4x4 matrix, Color32 color)
    {
        int first = Vertices.Count;
        for (int i = 0; i < vertices.Count; i++)
        {
            AddVertex(matrix.MultiplyPoint3x4(vertices[i]), color);
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            Triangles.Add(first + triangles[i]);
        }
    }

    public Mesh ToMesh(string name, bool normals)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(Vertices);
        mesh.SetColors(Colors);
        mesh.SetTriangles(Triangles, 0);
        if (normals)
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
        return mesh;
    }
}

// Pure geometry and colour helpers for the finale's detailed distant islands (Islands 1 to 4 baked from their TerrainData, never from a loaded scene).
public static class FinaleIslandMesher
{
    public const int LandCells = 128;
    public const int LowCells = 64;

    // ---- colour ----

    public static Color32 ToVertexColor(Color srgb)
    {
        return new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(srgb.r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(srgb.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(srgb.b) * 255f), 255);
    }

    // The painted ground colour at alphamap texel (x, y): the layer colours (sRGB) mixed by the splat weights, in linear light.
    // The weights are sharpened (the dominant layer wins, so a grass island reads green rather than a muddy average), and the cliff layer's weight
    // is scaled by `cliffWeight` (0..1: the rock layer is painted over most of the land, but should show on steep ground).
    public static Color BlendLayers(float[,,] alpha, int x, int y, Color[] layerSrgb, int cliffLayer = -1, float cliffWeight = 1f)
    {
        int layers = Mathf.Min(alpha.GetLength(2), layerSrgb.Length);
        float sum = 0f;
        Vector3 linear = Vector3.zero;
        for (int k = 0; k < layers; k++)
        {
            float w = alpha[y, x, k];
            if (k == cliffLayer)
            {
                w *= cliffWeight;
            }

            w *= w;
            if (w <= 0f)
            {
                continue;
            }

            Color c = layerSrgb[k].linear;
            linear += new Vector3(c.r, c.g, c.b) * w;
            sum += w;
        }

        if (sum <= 1e-5f)
        {
            return layerSrgb.Length > 0 ? layerSrgb[0] : Color.gray;
        }

        linear /= sum;
        return new Color(linear.x, linear.y, linear.z, 1f).gamma;
    }

    // ---- terrain ----

    // A land mesh (cells x cells) of a terrain: island local space, x/z centred on the terrain, y relative to the sea (seaY above the terrain base).
    // Triangles wholly below the sea are cut away (the skirt), the rest is clamped to skirtFloor. Vertex colours (sRGB) come from the alphamap
    // layers, with steep slopes blended to layer `cliffLayer`, a little deterministic variation, and darker wet ground at the shore.
    // heights are [z, x]; alpha is [z, x, layer].
    public static Mesh BuildTerrain(string name, float[,] heights, float[,,] alpha, Color[] layerSrgb, Vector3 size, float seaY, int cells, int cliffLayer, float skirtFloor)
    {
        int res = heights.GetLength(0);
        int aRes = alpha.GetLength(0);
        int n = cells + 1;
        Vector3[] grid = new Vector3[n * n];
        Color32[] colors = new Color32[n * n];
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / cells;
                float v = (float)j / cells;
                float y = Bilinear(heights, res, u, v) * size.y - seaY;
                grid[j * n + i] = new Vector3((u - 0.5f) * size.x, y, (v - 0.5f) * size.z);

                float e = 1.5f / cells;
                float dx = (Bilinear(heights, res, Mathf.Min(1f, u + e), v) - Bilinear(heights, res, Mathf.Max(0f, u - e), v)) * size.y / (2f * e * size.x);
                float dz = (Bilinear(heights, res, u, Mathf.Min(1f, v + e)) - Bilinear(heights, res, u, Mathf.Max(0f, v - e))) * size.y / (2f * e * size.z);
                float slope = Mathf.Sqrt(dx * dx + dz * dz);
                float cliffWeight = 0.12f + 0.88f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.8f, slope));
                int ax = Mathf.Clamp(Mathf.RoundToInt(u * (aRes - 1)), 0, aRes - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(v * (aRes - 1)), 0, aRes - 1);
                Color color = BlendLayers(alpha, ax, az, layerSrgb, cliffLayer, cliffWeight);
                if (cliffLayer >= 0 && cliffLayer < layerSrgb.Length)
                {
                    float cliff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.2f, slope));
                    color = Color.Lerp(color, layerSrgb[cliffLayer], cliff);
                }

                float variation = 1f + (Hash(i, j) - 0.5f) * 0.12f;
                float wet = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(-y / 1.5f));
                // Relief: a baked hill-shade (light from the north-west, low) plus a little darkening on steep ground, so the land reads as hills from afar.
                Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
                float lit = Mathf.Clamp01(Vector3.Dot(normal, new Vector3(-0.5f, 0.6f, 0.55f).normalized));
                float relief = Mathf.Lerp(0.62f, 1.18f, lit) * Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(slope - 0.4f));
                float k = variation * wet * relief;
                colors[j * n + i] = ToVertexColor(new Color(color.r * k, color.g * k, color.b * k, 1f));
            }
        }

        List<int> kept = new List<int>();
        for (int j = 0; j < cells; j++)
        {
            for (int i = 0; i < cells; i++)
            {
                int v00 = j * n + i, v10 = v00 + 1, v01 = v00 + n, v11 = v01 + 1;
                AddKept(kept, grid, v00, v01, v10);
                AddKept(kept, grid, v10, v01, v11);
            }
        }

        FinaleMeshBuilder builder = new FinaleMeshBuilder();
        int[] remap = new int[grid.Length];
        for (int i = 0; i < remap.Length; i++) remap[i] = -1;
        for (int t = 0; t < kept.Count; t++)
        {
            int old = kept[t];
            if (remap[old] < 0)
            {
                Vector3 p = grid[old];
                p.y = Mathf.Max(p.y, skirtFloor);
                remap[old] = builder.AddVertex(p, colors[old]);
            }

            builder.Triangles.Add(remap[old]);
        }

        return builder.ToMesh(name, true);
    }

    static void AddKept(List<int> kept, Vector3[] grid, int a, int b, int c)
    {
        if (Mathf.Max(grid[a].y, Mathf.Max(grid[b].y, grid[c].y)) >= 0f)
        {
            kept.Add(a);
            kept.Add(b);
            kept.Add(c);
        }
    }

    // Heights are [z, x]; u and v run 0..1 across the terrain.
    public static float Bilinear(float[,] heights, int res, float u, float v)
    {
        float fx = u * (res - 1), fz = v * (res - 1);
        int x0 = Mathf.Min((int)fx, res - 2), z0 = Mathf.Min((int)fz, res - 2);
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(heights[z0, x0], heights[z0, x0 + 1], tx);
        float b = Mathf.Lerp(heights[z0 + 1, x0], heights[z0 + 1, x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    // Vertex clustering: vertices falling in the same `cell` metre grid cell are merged to their average, and triangles that collapse are dropped.
    // Cheap, deterministic LOD for the beacon model, which is far too detailed at 300 m.
    public static void Simplify(IList<Vector3> vertices, IList<int> triangles, float cell, out Vector3[] outVertices, out int[] outTriangles)
    {
        Dictionary<long, int> cellIndex = new Dictionary<long, int>();
        List<Vector3> sums = new List<Vector3>();
        List<int> counts = new List<int>();
        int[] map = new int[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p = vertices[i];
            long key = ((long)Mathf.FloorToInt(p.x / cell) & 0x1FFFFF) | (((long)Mathf.FloorToInt(p.y / cell) & 0x1FFFFF) << 21) | (((long)Mathf.FloorToInt(p.z / cell) & 0x1FFFFF) << 42);
            if (!cellIndex.TryGetValue(key, out int index))
            {
                index = sums.Count;
                cellIndex[key] = index;
                sums.Add(Vector3.zero);
                counts.Add(0);
            }

            sums[index] += p;
            counts[index]++;
            map[i] = index;
        }

        outVertices = new Vector3[sums.Count];
        for (int i = 0; i < outVertices.Length; i++)
        {
            outVertices[i] = sums[i] / counts[i];
        }

        List<int> kept = new List<int>();
        HashSet<long> seen = new HashSet<long>();
        for (int t = 0; t < triangles.Count; t += 3)
        {
            int a = map[triangles[t]], b = map[triangles[t + 1]], c = map[triangles[t + 2]];
            if (a == b || b == c || a == c)
            {
                continue;
            }

            // The same triangle twice (in either rotation) is kept once.
            int lo = Mathf.Min(a, Mathf.Min(b, c)), hi = Mathf.Max(a, Mathf.Max(b, c)), mid = a + b + c - lo - hi;
            long id = ((long)lo << 42) | ((long)mid << 21) | (long)hi;
            if (!seen.Add(id))
            {
                continue;
            }

            kept.Add(a);
            kept.Add(b);
            kept.Add(c);
        }

        outTriangles = kept.ToArray();
    }

    // ---- low-poly trees (the scene trees are 3k to 10k triangles of leaf cards; these stand-ins are 36 to 48) ----

    public static void AddPine(FinaleMeshBuilder b, Vector3 foot, float height, float radius, Color32 leaf, Color32 bark)
    {
        AddPrism(b, foot, height * 0.30f, height * 0.05f, 6, bark);
        for (int i = 0; i < 3; i++)
        {
            float baseY = height * (0.16f + 0.22f * i);
            AddCone(b, foot + Vector3.up * baseY, radius * (1f - 0.27f * i), height * 0.42f, 8, leaf);
        }
    }

    public static void AddBroadleaf(FinaleMeshBuilder b, Vector3 foot, float height, float radius, Color32 leaf, Color32 bark)
    {
        AddPrism(b, foot, height * 0.5f, height * 0.06f, 6, bark);
        AddEllipsoid(b, foot + Vector3.up * height * 0.66f, new Vector3(radius, height * 0.34f, radius), 8, 4, leaf);
    }

    // A prism from foot upwards (open top and bottom).
    public static void AddPrism(FinaleMeshBuilder b, Vector3 foot, float height, float radius, int sides, Color32 color)
    {
        int[] low = new int[sides], high = new int[sides];
        for (int s = 0; s < sides; s++)
        {
            float a = s * 2f * Mathf.PI / sides;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            low[s] = b.AddVertex(foot + d, color);
            high[s] = b.AddVertex(foot + d + Vector3.up * height, color);
        }

        for (int s = 0; s < sides; s++)
        {
            int t = (s + 1) % sides;
            b.AddTriangle(low[s], high[s], low[t]);
            b.AddTriangle(low[t], high[s], high[t]);
        }
    }

    // A cone with its base ring at `baseCentre` (open base).
    public static void AddCone(FinaleMeshBuilder b, Vector3 baseCentre, float radius, float height, int sides, Color32 color)
    {
        int apex = b.AddVertex(baseCentre + Vector3.up * height, color);
        int[] ring = new int[sides];
        for (int s = 0; s < sides; s++)
        {
            float a = s * 2f * Mathf.PI / sides;
            ring[s] = b.AddVertex(baseCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, color);
        }

        for (int s = 0; s < sides; s++)
        {
            b.AddTriangle(ring[s], apex, ring[(s + 1) % sides]);
        }
    }

    // A closed ellipsoid: `segments` round, `rings` latitude bands (poles included).
    public static void AddEllipsoid(FinaleMeshBuilder b, Vector3 centre, Vector3 radii, int segments, int rings, Color32 color)
    {
        int top = b.AddVertex(centre + new Vector3(0f, radii.y, 0f), color);
        int bottom = b.AddVertex(centre - new Vector3(0f, radii.y, 0f), color);
        int[][] ring = new int[rings - 1][];
        for (int r = 1; r < rings; r++)
        {
            float phi = Mathf.PI * r / rings;
            float y = Mathf.Cos(phi) * radii.y;
            float rr = Mathf.Sin(phi);
            ring[r - 1] = new int[segments];
            for (int s = 0; s < segments; s++)
            {
                float a = s * 2f * Mathf.PI / segments;
                ring[r - 1][s] = b.AddVertex(centre + new Vector3(Mathf.Cos(a) * rr * radii.x, y, Mathf.Sin(a) * rr * radii.z), color);
            }
        }

        for (int s = 0; s < segments; s++)
        {
            int t = (s + 1) % segments;
            b.AddTriangle(ring[0][s], top, ring[0][t]);
            b.AddTriangle(ring[rings - 2][t], bottom, ring[rings - 2][s]);
            for (int r = 0; r < rings - 2; r++)
            {
                b.AddTriangle(ring[r + 1][s], ring[r][s], ring[r + 1][t]);
                b.AddTriangle(ring[r + 1][t], ring[r][s], ring[r][t]);
            }
        }
    }

    // Two crossed vertical unit-ish quads (width x height, base at `foot`), uv across; the flame card of a beacon.
    public static void AddCrossedCards(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 foot, float width, float height)
    {
        for (int card = 0; card < 2; card++)
        {
            Vector3 side = card == 0 ? Vector3.right : Vector3.forward;
            int first = vertices.Count;
            vertices.Add(foot - side * width * 0.5f);
            vertices.Add(foot + side * width * 0.5f);
            vertices.Add(foot - side * width * 0.5f + Vector3.up * height);
            vertices.Add(foot + side * width * 0.5f + Vector3.up * height);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            if (card == 0)
            {
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
            }
            else
            {
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first + 3);
            }
        }
    }
}
}
