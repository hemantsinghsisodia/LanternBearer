using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Scene checks for the D2 environment props: leftover photo props, missing colliders and bare cliff faces.
public static class PropValidation
{
    public const string PhotoFolder = "Prefabs/PolyHaven/";
    public const float HeroClearance = 6f;
    public const float MinRunLength = 4f;
    public const float MinRunHeight = 2f;
    public const float CladdingReach = 3f;
    public const float SkinReach = 1.5f;
    const float SteepGradient = 1f;
    const float FaceGradient = 0.7f;

    public static int Report(bool quiet)
    {
        int problems = 0;
        GameObject props = GameObject.Find("Props");
        if (props != null)
        {
            problems += CheckPhotoProps(props.transform, quiet);
            problems += CheckColliders(props.transform, quiet);
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++)
        {
            problems += CheckCliffFaces(terrains[i], props != null ? props.transform : null, quiet);
        }

        return problems;
    }

    // Prefab instance roots under the parent, each with its source asset path.
    static List<KeyValuePair<GameObject, string>> InstancesUnder(Transform parent)
    {
        List<KeyValuePair<GameObject, string>> list = new List<KeyValuePair<GameObject, string>>();
        HashSet<GameObject> seen = new HashSet<GameObject>();
        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(renderers[i].gameObject);
            if (root == null || !seen.Add(root))
            {
                continue;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
            list.Add(new KeyValuePair<GameObject, string>(root, path));
        }

        return list;
    }

    static List<Vector3> HeroSpots()
    {
        List<Vector3> spots = new List<Vector3>();
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            spots.Add(player.transform.position);
        }

        GameObject beacons = GameObject.Find("Beacons");
        if (beacons != null)
        {
            foreach (Transform beacon in beacons.transform)
            {
                spots.Add(beacon.position);
            }
        }

        return spots;
    }

    static int CheckPhotoProps(Transform props, bool quiet)
    {
        int problems = 0;
        List<Vector3> spots = HeroSpots();
        List<KeyValuePair<GameObject, string>> instances = InstancesUnder(props);
        for (int i = 0; i < instances.Count; i++)
        {
            string path = instances[i].Value;
            if (!path.Contains(PhotoFolder))
            {
                continue;
            }

            GameObject go = instances[i].Key;
            string prefabName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!NatureKitImporter.AllowList.ContainsKey(prefabName))
            {
                problems++;
                if (!quiet)
                {
                    Debug.LogWarning("Photo prop " + go.name + " outside allow-list", go);
                }

                continue;
            }

            for (int s = 0; s < spots.Count; s++)
            {
                Vector2 d = new Vector2(go.transform.position.x - spots[s].x, go.transform.position.z - spots[s].z);
                if (d.magnitude < HeroClearance)
                {
                    problems++;
                    if (!quiet)
                    {
                        Debug.LogWarning("Photo prop " + go.name + " within 6 m of spawn/beacon", go);
                    }

                    break;
                }
            }
        }

        return problems;
    }

    // Tree, Sapling, Rock and Deadwood prefabs (by source folder) all need a collider; the cladding folder does not.
    static int CheckColliders(Transform props, bool quiet)
    {
        int problems = 0;
        List<KeyValuePair<GameObject, string>> instances = InstancesUnder(props);
        for (int i = 0; i < instances.Count; i++)
        {
            string path = instances[i].Value.Replace('\\', '/');
            bool needs = path.Contains("/Tree/") || path.Contains("/Sapling/") || path.Contains("/Rock/") || path.Contains("/Deadwood/");
            if (!needs)
            {
                continue;
            }

            GameObject go = instances[i].Key;
            if (go.GetComponentInChildren<Collider>(true) == null)
            {
                problems++;
                if (!quiet)
                {
                    Debug.LogWarning("Tree/rock " + go.name + " missing collider", go);
                }
            }
        }

        return problems;
    }

    // A steep run (connected steep cells with no cladding rock within 3 m and no CliffSkin strip within 1.5 m) longer than 4 m and taller than 2 m is a bare face.
    static int CheckCliffFaces(Terrain terrain, Transform props, bool quiet)
    {
        TerrainData data = terrain.terrainData;
        if (data == null)
        {
            return 0;
        }

        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 origin = terrain.transform.position;
        float cell = data.size.x / (res - 1);
        float scale = data.size.y;

        List<Vector2> rocks = new List<Vector2>();
        if (props != null)
        {
            Transform folder = props.Find("CliffCladding");
            if (folder != null)
            {
                foreach (Transform rock in folder)
                {
                    rocks.Add(new Vector2(rock.position.x, rock.position.z));
                }
            }
        }

        List<SkinStrip> strips = CliffSkinPlanner.Plan(heights, data.size.x, scale, new Vector2(origin.x, origin.z), CliffSkinPlanner.StepThresholdM);
        List<Vector2> skin = new List<Vector2>();
        for (int s = 0; s < strips.Count; s++)
        {
            for (int p = 0; p < strips[s].points.Count; p++)
            {
                skin.Add(strips[s].points[p].position);
            }
        }

        // Uncovered steep cells.
        bool[,] bare = new bool[res, res];
        Grid rockGrid = new Grid(rocks, CladdingReach);
        Grid skinGrid = new Grid(skin, SkinReach);
        for (int z = 1; z < res - 1; z++)
        {
            for (int x = 1; x < res - 1; x++)
            {
                float gx = (heights[z, x + 1] - heights[z, x - 1]) * scale / (2f * cell);
                float gz = (heights[z + 1, x] - heights[z - 1, x]) * scale / (2f * cell);
                if (Mathf.Sqrt(gx * gx + gz * gz) <= SteepGradient)
                {
                    continue;
                }

                Vector2 world = new Vector2(origin.x + x * cell, origin.z + z * cell);
                if (!rockGrid.Near(world) && !skinGrid.Near(world) && FaceHeight(heights, scale, cell, x, z) > MinRunHeight)
                {
                    bare[z, x] = true;
                }
            }
        }

        int problems = 0;
        bool[,] seen = new bool[res, res];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        for (int z = 1; z < res - 1; z++)
        {
            for (int x = 1; x < res - 1; x++)
            {
                if (!bare[z, x] || seen[z, x])
                {
                    continue;
                }

                int minX = x, maxX = x, minZ = z, maxZ = z;
                float lo = float.MaxValue, hi = float.MinValue, face = 0f;
                int count = 0;
                stack.Push(new Vector2Int(x, z));
                seen[z, x] = true;
                while (stack.Count > 0)
                {
                    Vector2Int c = stack.Pop();
                    count++;
                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minZ = Mathf.Min(minZ, c.y);
                    maxZ = Mathf.Max(maxZ, c.y);
                    float h = heights[c.y, c.x] * scale;
                    lo = Mathf.Min(lo, h);
                    hi = Mathf.Max(hi, h);
                    face = Mathf.Max(face, FaceHeight(heights, scale, cell, c.x, c.y));
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = c.x + dx;
                            int nz = c.y + dz;
                            if (nx < 1 || nz < 1 || nx >= res - 1 || nz >= res - 1 || !bare[nz, nx] || seen[nz, nx])
                            {
                                continue;
                            }

                            seen[nz, nx] = true;
                            stack.Push(new Vector2Int(nx, nz));
                        }
                    }
                }

                float runLength = Mathf.Sqrt((maxX - minX) * (maxX - minX) + (maxZ - minZ) * (maxZ - minZ)) * cell;
                if (runLength > MinRunLength)
                {
                    problems++;
                    if (!quiet)
                    {
                        Vector3 pos = new Vector3(origin.x + (minX + maxX) * 0.5f * cell, origin.y + (lo + hi) * 0.5f, origin.z + (minZ + maxZ) * 0.5f * cell);
                        Debug.LogWarning("Cliff face at " + pos.ToString("F1") + " has no cladding (" + runLength.ToString("F1") + " m long, " + face.ToString("F1") + " m tall, " + count + " cells)");
                    }
                }
            }
        }

        return problems;
    }

    // Height of the face through a steep cell: walk up and down the slope while it stays steeper than about 35 degrees (the planner's rule).
    static float FaceHeight(float[,] heights, float scale, float cell, int x, int z)
    {
        int res = heights.GetLength(0);
        float h = heights[z, x] * scale;
        float lo = h;
        float hi = h;
        for (int dir = -1; dir <= 1; dir += 2)
        {
            float px = x;
            float pz = z;
            for (int step = 0; step < 120; step++)
            {
                int cx = Mathf.RoundToInt(px);
                int cz = Mathf.RoundToInt(pz);
                if (cx < 1 || cz < 1 || cx >= res - 1 || cz >= res - 1)
                {
                    break;
                }

                Vector2 g = new Vector2((heights[cz, cx + 1] - heights[cz, cx - 1]) * scale / (2f * cell), (heights[cz + 1, cx] - heights[cz - 1, cx]) * scale / (2f * cell));
                if (g.magnitude <= FaceGradient)
                {
                    break;
                }

                g.Normalize();
                px += dir * g.x;
                pz += dir * g.y;
                int nx = Mathf.RoundToInt(px);
                int nz = Mathf.RoundToInt(pz);
                if (nx < 0 || nz < 0 || nx >= res || nz >= res)
                {
                    break;
                }

                float v = heights[nz, nx] * scale;
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
        }

        return hi - lo;
    }

    // Bucket grid for "is any point within reach" queries.
    class Grid
    {
        readonly float reach;
        readonly Dictionary<long, List<Vector2>> buckets = new Dictionary<long, List<Vector2>>();

        public Grid(List<Vector2> points, float reach)
        {
            this.reach = reach;
            for (int i = 0; i < points.Count; i++)
            {
                long key = Key(Mathf.FloorToInt(points[i].x / reach), Mathf.FloorToInt(points[i].y / reach));
                List<Vector2> list;
                if (!buckets.TryGetValue(key, out list))
                {
                    list = new List<Vector2>();
                    buckets[key] = list;
                }

                list.Add(points[i]);
            }
        }

        static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        public bool Near(Vector2 p)
        {
            int cx = Mathf.FloorToInt(p.x / reach);
            int cz = Mathf.FloorToInt(p.y / reach);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Vector2> list;
                    if (!buckets.TryGetValue(Key(cx + dx, cz + dz), out list))
                    {
                        continue;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        if ((list[i] - p).sqrMagnitude <= reach * reach)
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
}
