using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class FinaleIslandMesherTests
{
    // A 33x33 height grid: a cone-shaped island (peak 1 in the middle, 0 at the edge, sea at 0.2 of the 10 m terrain), and a two layer splat:
    // layer 0 (red) on the left half, layer 1 (blue) on the right half.
    static void Synthetic(out float[,] heights, out float[,,] alpha, out Color[] layers)
    {
        int res = 33;
        heights = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x - 16f) / 16f, dz = (z - 16f) / 16f;
                heights[z, x] = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz));
            }
        }

        alpha = new float[64, 64, 2];
        for (int z = 0; z < 64; z++)
        {
            for (int x = 0; x < 64; x++)
            {
                bool left = x < 32;
                alpha[z, x, 0] = left ? 1f : 0f;
                alpha[z, x, 1] = left ? 0f : 1f;
            }
        }

        layers = new[] { new Color(0.8f, 0.1f, 0.1f, 1f), new Color(0.1f, 0.1f, 0.8f, 1f) };
    }

    [Test]
    public void TerrainIsCutAtTheSeaAndFollowsTheGrid()
    {
        Synthetic(out float[,] heights, out float[,,] alpha, out Color[] layers);
        Mesh mesh = FinaleIslandMesher.BuildTerrain("t", heights, alpha, layers, new Vector3(60f, 10f, 60f), 2f, 32, -1, -2f);
        Assert.Greater(mesh.vertexCount, 100);
        Assert.Less(mesh.vertexCount, 33 * 33, "the sea skirt is cut away");
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Assert.GreaterOrEqual(Mathf.Max(v[t[i]].y, Mathf.Max(v[t[i + 1]].y, v[t[i + 2]].y)), 0f, "no triangle lies wholly below the sea");
        }

        Assert.AreEqual(8f, mesh.bounds.max.y, 0.05f, "the peak is 10 m above the base, 2 m of it under the sea");
        Assert.GreaterOrEqual(mesh.bounds.min.y, -2f - 0.001f, "the skirt floor");
    }

    [Test]
    public void TerrainColoursComeFromTheAlphamapLayers()
    {
        Synthetic(out float[,] heights, out float[,,] alpha, out Color[] layers);
        Mesh mesh = FinaleIslandMesher.BuildTerrain("t", heights, alpha, layers, new Vector3(60f, 10f, 60f), 2f, 32, -1, -2f);
        Vector3[] v = mesh.vertices;
        Color32[] c = mesh.colors32;
        Assert.AreEqual(v.Length, c.Length);
        int checkedLeft = 0, checkedRight = 0;
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i].y < 1.5f)
            {
                continue;
            }

            if (v[i].x < -4f)
            {
                Assert.Greater(c[i].r, c[i].b + 40, "left of the island is the red layer");
                checkedLeft++;
            }
            else if (v[i].x > 4f)
            {
                Assert.Greater(c[i].b, c[i].r + 40, "right of the island is the blue layer");
                checkedRight++;
            }
        }

        Assert.Greater(checkedLeft, 5);
        Assert.Greater(checkedRight, 5);
    }

    [Test]
    public void LowTerrainHasFewerTrianglesThanFull()
    {
        Synthetic(out float[,] heights, out float[,,] alpha, out Color[] layers);
        Mesh full = FinaleIslandMesher.BuildTerrain("f", heights, alpha, layers, new Vector3(60f, 10f, 60f), 2f, 32, -1, -2f);
        Mesh low = FinaleIslandMesher.BuildTerrain("l", heights, alpha, layers, new Vector3(60f, 10f, 60f), 2f, 16, -1, -2f);
        Assert.Less(low.triangles.Length, full.triangles.Length / 2);
    }

    [Test]
    public void BlendLayersMixesBySplatWeight()
    {
        float[,,] alpha = new float[1, 1, 2];
        alpha[0, 0, 0] = 0.5f;
        alpha[0, 0, 1] = 0.5f;
        Color mix = FinaleIslandMesher.BlendLayers(alpha, 0, 0, new[] { Color.black, Color.white });
        Assert.AreEqual(0.5f, mix.linear.r, 0.01f, "half black, half white in linear light");
    }

    [Test]
    public void ProxyTreesAreSmallAndFaceOutwards()
    {
        FinaleMeshBuilder b = new FinaleMeshBuilder();
        FinaleIslandMesher.AddPine(b, Vector3.zero, 8f, 2.5f, new Color32(40, 90, 40, 255), new Color32(80, 60, 40, 255));
        int pine = b.TriangleCount;
        FinaleIslandMesher.AddBroadleaf(b, new Vector3(20f, 0f, 0f), 6f, 2f, new Color32(40, 90, 40, 255), new Color32(80, 60, 40, 255));
        int broadleaf = b.TriangleCount - pine;
        Assert.LessOrEqual(pine, 60);
        Assert.LessOrEqual(broadleaf, 60);
        Assert.AreEqual(b.Vertices.Count, b.Colors.Count);

        // Every triangle's normal points away from the tree's axis (or up/down for the ends), never into the tree.
        for (int i = 0; i < b.Triangles.Count; i += 3)
        {
            Vector3 a = b.Vertices[b.Triangles[i]], p = b.Vertices[b.Triangles[i + 1]], c = b.Vertices[b.Triangles[i + 2]];
            Vector3 n = Vector3.Cross(p - a, c - a);
            Vector3 centroid = (a + p + c) / 3f;
            Vector3 axisPoint = new Vector3(centroid.x < 10f ? 0f : 20f, centroid.y, 0f);
            Vector3 outward = centroid - axisPoint;
            if (i / 3 >= pine)
            {
                // The crown's ellipsoid: outward from its centre.
                axisPoint = new Vector3(20f, 6f * 0.66f, 0f);
                outward = centroid - axisPoint;
                if (centroid.y < 6f * 0.3f)
                {
                    outward = new Vector3(centroid.x - 20f, 0f, centroid.z);
                }
            }

            if (n.sqrMagnitude < 1e-8f)
            {
                continue;
            }

            Assert.GreaterOrEqual(Vector3.Dot(n.normalized, outward.normalized), -0.001f, "triangle " + (i / 3) + " faces outwards");
        }
    }

    [Test]
    public void EllipsoidRocksAreClosedAndSmall()
    {
        FinaleMeshBuilder b = new FinaleMeshBuilder();
        FinaleIslandMesher.AddEllipsoid(b, Vector3.up, new Vector3(1.5f, 1f, 1.5f), 7, 3, new Color32(120, 120, 120, 255));
        Assert.LessOrEqual(b.TriangleCount, 40);
        Mesh mesh = b.ToMesh("rock", true);
        Assert.AreEqual(b.Vertices.Count, mesh.vertexCount);
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            Assert.GreaterOrEqual(Vector3.Dot(normals[i], (b.Vertices[i] - Vector3.up).normalized), 0f, "vertex " + i + " normal points out");
        }
    }
}
}
