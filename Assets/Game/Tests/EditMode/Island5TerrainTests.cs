using System;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Reads the built Island 5 terrain asset (Levels/Island5_Terrain.asset): the spire's shape, walkway and gaps.
public class Island5TerrainTests
{
    const string TerrainPath = "Assets/Game/Levels/Island5_Terrain.asset";
    const float WaterFraction = 0.2f;

    // Heightmap MD5 (GetHeights, float bytes) of Islands 1-4 recorded before the spire work. SculptHeights must not change for spire == false.
    static readonly string[] ExpectedHashes =
    {
        "09c602efd94a7c332f2d872eb593fac3",
        "12e43ef11f3b5c8ab7161a9ee8d4514c",
        "e94c410ec6d5e88c997896f87abdeab4",
        "cef3f7b0968b6deefdf30dde4d605de9"
    };

    static TerrainData Load()
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        Assert.IsNotNull(data, "build Island 5 first: " + TerrainPath);
        return data;
    }

    static float Sea(TerrainData data)
    {
        return WaterFraction * data.size.y;
    }

    // Metres above the sea at a world XZ (island centre at the origin).
    static float HeightAt(TerrainData data, Vector2 xz)
    {
        float u = Mathf.Clamp01(xz.x / data.size.x + 0.5f);
        float v = Mathf.Clamp01(xz.y / data.size.z + 0.5f);
        return data.GetInterpolatedHeight(u, v) - Sea(data);
    }

    static float SteepnessAt(TerrainData data, Vector2 xz)
    {
        float u = Mathf.Clamp01(xz.x / data.size.x + 0.5f);
        float v = Mathf.Clamp01(xz.y / data.size.z + 0.5f);
        return data.GetSteepness(u, v);
    }

    static bool InsideAnyGap(float u, float pad)
    {
        for (int g = 0; g < 4; g++)
        {
            Vector2 span = SpireLayout.GapSpanU(g);
            if (u >= span.x - pad && u <= span.y + pad)
            {
                return true;
            }
        }

        return false;
    }

    [Test]
    public void SummitAboutForty()
    {
        TerrainData data = Load();
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        float max = 0f;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                max = Mathf.Max(max, heights[z, x]);
            }
        }

        float summit = max * data.size.y - Sea(data);
        Assert.GreaterOrEqual(summit, 38f);
        Assert.LessOrEqual(summit, 42f);
        Assert.AreEqual(SpireLayout.SummitHeight, HeightAt(data, SpireLayout.LampXZ()), 0.6f, "the great lamp sits on the top");
    }

    [Test]
    public void WalkwayFollowsSpiral()
    {
        TerrainData data = Load();
        float endU = SpireLayout.GapSpanU(3).x;
        int checkedPoints = 0;
        for (int i = 0; i < 400 && checkedPoints < 200; i++)
        {
            float u = 0.005f + (endU - 0.01f) * i / 399f;
            if (InsideAnyGap(u, 0.02f))
            {
                continue;
            }

            Vector2 p = SpireLayout.PathPoint(u);
            float expected = SpireLayout.PathHeight(u);
            Assert.AreEqual(expected, HeightAt(data, p), 1.0f, "height at u=" + u);
            Assert.LessOrEqual(SteepnessAt(data, p), 25f, "slope at u=" + u);
            checkedPoints++;
        }

        Assert.GreaterOrEqual(checkedPoints, 150, "enough walkway samples outside the gaps");
    }

    [Test]
    public void WalkwayIsAWalkwayWide()
    {
        TerrainData data = Load();
        float endU = SpireLayout.GapSpanU(3).x;
        for (int i = 0; i < 100; i++)
        {
            float u = 0.01f + (endU - 0.02f) * i / 99f;
            if (InsideAnyGap(u, 0.02f))
            {
                continue;
            }

            Vector2 p = SpireLayout.PathPoint(u);
            Vector2 t = SpireLayout.PathTangent(u);
            Vector2 side = new Vector2(-t.y, t.x) * (SpireLayout.PathWidth * 0.5f - 0.2f);
            float centre = HeightAt(data, p);
            Assert.AreEqual(centre, HeightAt(data, p + side), 0.5f, "left edge at u=" + u);
            Assert.AreEqual(centre, HeightAt(data, p - side), 0.5f, "right edge at u=" + u);
        }
    }

    [Test]
    public void GapsAreNotWalkable()
    {
        TerrainData data = Load();
        SpireGap[] gaps = SpireLayout.Gaps();
        for (int g = 0; g < gaps.Length; g++)
        {
            float nearH = HeightAt(data, gaps[g].nearXZ);
            float farH = HeightAt(data, gaps[g].farXZ);
            float lowest = float.MaxValue;
            for (int s = 0; s <= 40; s++)
            {
                lowest = Mathf.Min(lowest, HeightAt(data, Vector2.Lerp(gaps[g].nearXZ, gaps[g].farXZ, s / 40f)));
            }

            bool water = lowest < -0.5f;
            bool deep = lowest <= Mathf.Min(nearH, farH) - 4f;
            Assert.IsTrue(water || deep, "gap " + g + " lowest " + lowest + " near " + nearH + " far " + farH);
            if (g > 0)
            {
                Assert.LessOrEqual(lowest, Mathf.Min(nearH, farH) - 6f, "gap " + g + " is at least 6 m deep");
            }

            if (g < 3)
            {
                Assert.Less(lowest, -0.5f, "gap " + g + " is flooded (a keeper who falls in is put back by the water hazard)");
            }
        }
    }

    // Walking rules of the keeper: climb surfaces up to the 50 degree slope limit (and steps up to 0.7 m), never wade into water, fall freely.
    // From the beach, only the first stretch of walkway (before gap 1) may be reachable: the cliffs and the gaps block everything else.
    [Test]
    public void GapsBlockTheWalkway()
    {
        TerrainData data = Load();
        const float cell = 0.5f;
        int n = (int)(data.size.x / cell);
        float half = data.size.x * 0.5f;
        float sea = Sea(data);
        float[,] h = new float[n, n];
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                h[z, x] = data.GetInterpolatedHeight((x + 0.5f) * cell / data.size.x, (z + 0.5f) * cell / data.size.z) - sea;
            }
        }

        float climbTan = Mathf.Tan(50f * Mathf.Deg2Rad);
        float[,] grade = new float[n, n];
        for (int z = 1; z < n - 1; z++)
        {
            for (int x = 1; x < n - 1; x++)
            {
                float gx = (h[z, x + 1] - h[z, x - 1]) / (2f * cell);
                float gz = (h[z + 1, x] - h[z - 1, x]) / (2f * cell);
                grade[z, x] = Mathf.Sqrt(gx * gx + gz * gz);
            }
        }

        Func<Vector2, int> index = p => Mathf.Clamp((int)((p.y + half) / cell), 0, n - 1) * n + Mathf.Clamp((int)((p.x + half) / cell), 0, n - 1);
        bool[] seen = new bool[n * n];
        System.Collections.Generic.Queue<int> queue = new System.Collections.Generic.Queue<int>();
        int start = index(SpireLayout.PathPoint(0.001f));
        seen[start] = true;
        queue.Enqueue(start);
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int cz = cur / n;
            int cx = cur % n;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + dx[k];
                int nz = cz + dz[k];
                if (nx < 1 || nz < 1 || nx >= n - 1 || nz >= n - 1 || seen[nz * n + nx] || h[nz, nx] < -0.3f)
                {
                    continue;
                }

                float up = h[nz, nx] - h[cz, cx];
                if ((up > 0.2f && grade[nz, nx] > climbTan) || up > 0.7f)
                {
                    continue;
                }

                seen[nz * n + nx] = true;
                queue.Enqueue(nz * n + nx);
            }
        }

        Vector2[] beacons = SpireLayout.BeaconXZ();
        Assert.IsTrue(seen[index(beacons[0])], "beacon 0 is on the beach side");
        Assert.IsTrue(seen[index(beacons[1])], "beacon 1 comes before gap 1");
        for (int i = 2; i < beacons.Length; i++)
        {
            Assert.IsFalse(seen[index(beacons[i])], "beacon " + i + " must be behind a gap");
        }

        Assert.IsFalse(seen[index(SpireLayout.LampXZ())], "the great lamp is behind the summit span");
    }
    [Test]
    public void BeaconTerrainMatchesPathHeight()
    {
        TerrainData data = Load();
        Vector2[] xz = SpireLayout.BeaconXZ();
        float[] u = SpireLayout.BeaconParameters();
        for (int i = 0; i < xz.Length; i++)
        {
            float expected = SpireLayout.PathHeight(u[i]);
            float actual = HeightAt(data, xz[i]);
            Assert.AreEqual(expected, actual, 0.3f, "beacon " + i);
            Assert.AreEqual(RingLogic.Classify(expected), RingLogic.Classify(actual), "beacon " + i + " keeps its ring");
            // The keeper stands within the landing, not just at the centre.
            for (int k = 0; k < 8; k++)
            {
                float angle = k * Mathf.PI * 0.25f;
                Vector2 around = xz[i] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.5f;
                Assert.AreEqual(expected, HeightAt(data, around), 0.7f, "beacon " + i + " landing at " + k);
            }
        }
    }

    [Test]
    public void BeaconPrefabDoesNotRaiseStandingHeight()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Gameplay/Beacon.prefab");
        Assert.IsNotNull(prefab);
        Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            CapsuleCollider capsule = colliders[i] as CapsuleCollider;
            Assert.IsNotNull(capsule, "only the thin post collides, found " + colliders[i].GetType().Name + " on " + colliders[i].name);
            Assert.LessOrEqual(capsule.radius, 0.3f, "the post is too thin to stand on");
        }
    }

    [Test]
    public void IslandsCoverTheRings()
    {
        TerrainData data = Load();
        Vector2 start = SpireLayout.PathPoint(0f);
        Assert.Greater(start.magnitude, 40f);
        Assert.Greater(HeightAt(data, start), 0.2f, "the spawn is dry land");
        // Sea all round beyond the beach.
        for (int k = 0; k < 16; k++)
        {
            float angle = k * Mathf.PI * 0.125f;
            Vector2 offshore = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 52f;
            Assert.Less(HeightAt(data, offshore), 0f, "open water at angle " + k);
        }
    }

    [Test]
    public void Islands1To4TerrainUnchanged()
    {
        for (int i = 0; i < 4; i++)
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Game/Levels/Island" + (i + 1) + "_Terrain.asset");
            Assert.IsNotNull(data, "Island" + (i + 1) + "_Terrain");
            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);
            byte[] bytes = new byte[res * res * 4];
            Buffer.BlockCopy(heights, 0, bytes, 0, bytes.Length);
            string hash;
            using (MD5 md5 = MD5.Create())
            {
                hash = BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }

            Assert.AreEqual(ExpectedHashes[i], hash, "Island" + (i + 1) + " heightmap changed");
        }
    }

    [Test]
    public void Island5IsConfiguredAndUnlockedFromIsland4()
    {
        ScriptableObject island4 = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Levels/Island4.asset");
        ScriptableObject island5 = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Levels/Island5.asset");
        ScriptableObject island1 = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Levels/Island1.asset");
        Assert.IsNotNull(island5);
        SerializedObject four = new SerializedObject(island4);
        SerializedObject five = new SerializedObject(island5);
        Assert.AreEqual("Island5", four.FindProperty("nextLevelScene").stringValue);
        Assert.IsTrue(five.FindProperty("spire").boolValue);
        Assert.AreEqual(8, five.FindProperty("beaconCount").intValue);
        Assert.AreEqual(26, five.FindProperty("fireflyCount").intValue);
        Assert.AreEqual(4, five.FindProperty("mothCount").intValue);
        Assert.AreEqual(0, five.FindProperty("hiddenPathCount").intValue);
        Assert.AreEqual(5505, five.FindProperty("seed").intValue);
        Assert.AreEqual("island5", five.FindProperty("levelId").stringValue);
        Assert.IsNotNull(five.FindProperty("biome").objectReferenceValue);
        LookProfile look = five.FindProperty("lookProfile").objectReferenceValue as LookProfile;
        Assert.IsNotNull(look);
        Assert.AreEqual("island5", look.levelId);
        Assert.IsFalse(new SerializedObject(island1).FindProperty("spire").boolValue);
        Assert.IsFalse(four.FindProperty("spire").boolValue);
    }
}
}
