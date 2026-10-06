using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CliffSkinMesherTests
{
    const int N = 64;
    const float Cell = 0.11f;
    const float WorldSize = (N - 1) * Cell;
    const float HeightScale = 10f;
    const float Top = 6f;
    static readonly Vector2 Origin = new Vector2(-3f, -3f);
    static readonly Vector2 Dir = new Vector2(Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad));
    static readonly Vector2 Perp = new Vector2(-Dir.y, Dir.x);

    static float[,] Plateau()
    {
        float[,] h = new float[N, N];
        for (int z = 0; z < N; z++)
        {
            for (int x = 0; x < N; x++)
            {
                Vector2 d = new Vector2(x - 32, z - 32);
                if (Mathf.Abs(Vector2.Dot(d, Dir)) <= 18f && Mathf.Abs(Vector2.Dot(d, Perp)) <= 10f)
                {
                    h[z, x] = Top / HeightScale;
                }
            }
        }

        return h;
    }

    static List<SkinChunk> Mesh(int seed = 5)
    {
        List<SkinStrip> strips = CliffSkinPlanner.Plan(Plateau(), WorldSize, HeightScale, Origin, CliffSkinPlanner.StepThresholdM);
        float[,] h = Plateau();
        System.Func<Vector2, float> ground = p => h[Mathf.Clamp(Mathf.RoundToInt((p.y - Origin.y) / Cell), 0, N - 1), Mathf.Clamp(Mathf.RoundToInt((p.x - Origin.x) / Cell), 0, N - 1)] * HeightScale;
        return CliffSkinMesher.Build(strips, ground, 0f, seed);
    }

    [Test]
    public void BuildsValidDeterministicMesh()
    {
        List<SkinChunk> a = Mesh();
        List<SkinChunk> b = Mesh();
        Assert.Greater(a.Count, 0);
        Assert.AreEqual(a.Count, b.Count);
        for (int c = 0; c < a.Count; c++)
        {
            SkinChunk chunk = a[c];
            Assert.Greater(chunk.triangles.Length, 0);
            Assert.AreEqual(0, chunk.triangles.Length % 3);
            Assert.AreEqual(chunk.vertices.Length, chunk.normals.Length);
            Assert.AreEqual(chunk.vertices.Length, chunk.uvs.Length);
            for (int i = 0; i < chunk.triangles.Length; i++)
            {
                Assert.That(chunk.triangles[i], Is.InRange(0, chunk.vertices.Length - 1));
            }

            for (int i = 0; i < chunk.vertices.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(chunk.vertices[i].x + chunk.vertices[i].y + chunk.vertices[i].z));
                Assert.AreEqual(1f, chunk.normals[i].magnitude, 0.01f);
                Assert.AreEqual(chunk.vertices[i], b[c].vertices[i]);
            }
        }
    }

    [Test]
    public void WallStaysOutsideFaceAndSpansTheStep()
    {
        float[,] h = Plateau();
        float half = Cell * 0.5f;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        List<SkinChunk> chunks = Mesh();
        for (int c = 0; c < chunks.Count; c++)
        {
            for (int i = 0; i < chunks[c].vertices.Length; i++)
            {
                Vector3 v = chunks[c].vertices[i];
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
                if (v.y > Top - 0.35f)
                {
                    continue; // the lip is meant to reach over the rim
                }

                float nearest = float.MaxValue;
                for (int z = 0; z < N; z++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        if (h[z, x] < 0.5f)
                        {
                            continue;
                        }

                        float dx = Mathf.Max(Mathf.Abs(v.x - (Origin.x + x * Cell)) - half, 0f);
                        float dz = Mathf.Max(Mathf.Abs(v.z - (Origin.y + z * Cell)) - half, 0f);
                        nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
                    }
                }

                Assert.Greater(nearest, 0f, "wall vertex " + v + " is inside the plateau");
            }
        }

        Assert.LessOrEqual(minY, -CliffSkinMesher.BottomSink + 0.01f);
        Assert.GreaterOrEqual(maxY, Top + CliffSkinMesher.LipRise - 0.01f);
    }

    [Test]
    public void WallFacesOutward()
    {
        List<SkinChunk> chunks = Mesh();
        Vector3 centre = new Vector3(Origin.x + 32 * Cell, 0f, Origin.y + 32 * Cell);
        int checkedCount = 0;
        for (int c = 0; c < chunks.Count; c++)
        {
            SkinChunk chunk = chunks[c];
            for (int t = 0; t < chunk.triangles.Length; t += 3)
            {
                Vector3 a = chunk.vertices[chunk.triangles[t]];
                Vector3 b = chunk.vertices[chunk.triangles[t + 1]];
                Vector3 d = chunk.vertices[chunk.triangles[t + 2]];
                if (a.y > Top - 0.5f || b.y > Top - 0.5f || d.y > Top - 0.5f)
                {
                    continue;
                }

                Vector3 normal = Vector3.Cross(b - a, d - a);
                Vector3 outward = (a + b + d) / 3f - centre;
                outward.y = 0f;
                if (normal.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                checkedCount++;
                Assert.Greater(Vector3.Dot(normal.normalized, outward.normalized), 0f, "triangle " + t + " faces into the plateau");
            }
        }

        Assert.Greater(checkedCount, 100);
    }

    [Test]
    public void ClosedLoopSeamHasNoCrack()
    {
        // The loop's closing column duplicates the first one; positions and normals must match.
        List<SkinChunk> chunks = Mesh();
        SkinChunk chunk = chunks[0];
        int rows = 0;
        while (rows < chunk.uvs.Length && chunk.uvs[rows].x == chunk.uvs[0].x)
        {
            rows++;
        }

        Assert.Greater(rows, 3);
        int last = chunk.vertices.Length - rows;
        for (int r = 0; r < rows; r++)
        {
            Assert.AreEqual(0f, Vector3.Distance(chunk.vertices[r], chunk.vertices[last + r]), 1e-3f, "seam position row " + r);
            Assert.AreEqual(0f, Vector3.Distance(chunk.normals[r], chunk.normals[last + r]), 1e-3f, "seam normal row " + r);
        }

        float a = CliffSkinMesher.Bulge(0f, 2f, 10f, 3, 4);
        float b = CliffSkinMesher.Bulge(10f, 2f, 10f, 3, 4);
        Assert.AreEqual(a, b, 1e-4f);
        Assert.That(a, Is.InRange(CliffSkinMesher.BulgeMin, CliffSkinMesher.BulgeMax));
    }
}
}
