using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    const string CladdingPrefabFolder = "Assets/Game/Art/Environment/Nature/Prefabs/Cladding";
    const string RockPrefabFolder = "Assets/Game/Art/Environment/Nature/Prefabs/Rock";
    // Rock_Medium is about 1.5 m across; cladding scale from the planner is the target width in metres.
    const float RockUnitWidth = 1.5f;
    // Single-LOD cull height. QualitySettings.lodBias (1 / 2 / 3 / 4 on Low..Ultra) scales the distance per preset.
    const float CladdingCullHeight = 0.04f;
    const int SpareLayer = 31;

    // Rock meshes along every steep face. Collider-free copies of the island-tinted Rock_Medium prefabs.
    static int PlaceCladding(LevelConfig config, Stage stage, Transform parent)
    {
        if (stage.terrain == null || stage.terrain.terrainData == null)
        {
            return 0;
        }

        GameObject[] variants = CladdingPrefabs(config.levelId);
        if (variants == null)
        {
            // The main-menu preview (levelId "menu") has no Rock_Medium prefabs of its own and its cliffs use the CliffSkin,
            // so nothing is lost there. A real island missing them is worth a warning.
            if (config.levelId != "menu")
            {
                Debug.LogWarning(config.sceneName + ": cladding prefabs missing, skipped");
            }
            return 0;
        }

        TerrainData data = stage.terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 origin = stage.terrain.transform.position;
        float seaGuard = stage.highWaterY + 0.5f;
        List<CladdingRock> rocks = CliffCladdingPlanner.Plan(heights, data.size.x, data.size.y, new Vector2(origin.x, origin.z), p =>
        {
            // Shore: skip faces that never rise above the high-water line. A wall rising out of the sea is kept, since the
            // surface at its xz is a point mid-wall; the highest ground within 2.5 m decides.
            float high = GroundY(stage.terrain, p.x, p.y);
            high = Mathf.Max(high, GroundY(stage.terrain, p.x + 2.5f, p.y));
            high = Mathf.Max(high, GroundY(stage.terrain, p.x - 2.5f, p.y));
            high = Mathf.Max(high, GroundY(stage.terrain, p.x, p.y + 2.5f));
            high = Mathf.Max(high, GroundY(stage.terrain, p.x, p.y - 2.5f));
            return high < seaGuard;
        }, p => WalkwayHeight(stage, p), config.seed + 70);

        if (rocks.Count == 0)
        {
            return 0;
        }

        Transform folder = Folder("CliffCladding");
        folder.SetParent(parent, false);
        for (int i = 0; i < rocks.Count; i++)
        {
            CladdingRock rock = rocks[i];
            GameObject go = PlacePrefab(variants[rock.variant], folder, new Vector3(rock.position.x, origin.y + rock.position.y, rock.position.z), Quaternion.Euler(rock.tilt, rock.yaw, 0f));
            go.transform.localScale = Vector3.one * (rock.scale / RockUnitWidth);
            go.name = "CliffRock";
        }

        return rocks.Count - TrailClearanceCheck(config, stage, folder, origin, data, heights);
    }

    // Props (trees, saplings, rocks, deadwood) placed beside a trail can still reach across it: a 3 m boulder has a 1.5 m radius, more than the placement clearance.
    // Every prop whose collider touches a keeper-sized capsule (0.6 m radius) along a trail centreline is removed. Returns the number removed.
    static int PropTrailClearance(LevelConfig config, Stage stage, Transform props)
    {
        Physics.SyncTransforms();
        HashSet<Transform> doomed = new HashSet<Transform>();
        int samples = 0;
        for (int t = 0; t < stage.trails.Count; t++)
        {
            List<Vector2> path = stage.trails[t];
            for (int i = 1; i < path.Count; i++)
            {
                float length = Vector2.Distance(path[i - 1], path[i]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.3f));
                for (int k = 0; k < steps; k++)
                {
                    Vector2 p = Vector2.Lerp(path[i - 1], path[i], k / (float)steps);
                    float g = GroundY(stage.terrain, p.x, p.y);
                    samples++;
                    Collider[] near = Physics.OverlapCapsule(new Vector3(p.x, g + 0.6f, p.y), new Vector3(p.x, g + 1.16f, p.y), 0.6f, ~0, QueryTriggerInteraction.Ignore);
                    for (int n = 0; n < near.Length; n++)
                    {
                        Transform root = near[n].transform;
                        while (root != null && root.parent != props)
                        {
                            root = root.parent;
                        }

                        if (root != null && root.name != "CliffCladding" && root.name != CliffSkinName)
                        {
                            doomed.Add(root);
                        }
                    }
                }
            }
        }

        // Hidden-path stones and bank steps are walkways too.
        for (int i = 0; i < stage.pathStones.Count; i++)
        {
            Vector3 stone = stage.pathStones[i];
            samples++;
            Collider[] near = Physics.OverlapCapsule(new Vector3(stone.x, stone.y + 0.6f, stone.z), new Vector3(stone.x, stone.y + 1.16f, stone.z), 0.6f, ~0, QueryTriggerInteraction.Ignore);
            for (int n = 0; n < near.Length; n++)
            {
                Transform root = near[n].transform;
                while (root != null && root.parent != props)
                {
                    root = root.parent;
                }

                if (root != null && root.name != "CliffCladding" && root.name != CliffSkinName)
                {
                    doomed.Add(root);
                }
            }
        }

        foreach (Transform prop in doomed)
        {
            Debug.Log(config.sceneName + " prop on a trail removed: " + prop.name + " at " + prop.position.ToString("F1"));
            Object.DestroyImmediate(prop.gameObject);
        }

        Debug.Log(config.sceneName + " prop trail clearance: " + doomed.Count + " props removed (" + samples + " capsule samples)");
        return doomed.Count;
    }

    // Height of the trail, hidden-path stone or beacon ring ground within reach of p (trail and stone 1.3 m, beacon 4 m), NaN when p is free.
    static float WalkwayHeight(Stage stage, Vector2 p)
    {
        float best = float.NaN;
        float bestDistance = 1.3f;
        for (int i = 0; i < stage.trails.Count; i++)
        {
            List<Vector2> path = stage.trails[i];
            for (int s = 1; s < path.Count; s++)
            {
                float t;
                float distance = DistanceToSegment(p, path[s - 1], path[s], out t);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    Vector2 q = Vector2.Lerp(path[s - 1], path[s], t);
                    best = GroundY(stage.terrain, q.x, q.y);
                }
            }
        }

        // Hidden-path stones and bank steps: the stone top is the walkway, kept clear like a trail.
        for (int i = 0; i < stage.pathStones.Count; i++)
        {
            Vector3 stone = stage.pathStones[i];
            float distance = (new Vector2(stone.x, stone.z) - p).magnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = stone.y;
            }
        }

        if (!float.IsNaN(best))
        {
            return best;
        }

        for (int i = 0; i < stage.beaconSpots.Count; i++)
        {
            Vector3 b = stage.beaconSpots[i];
            if ((new Vector2(b.x, b.z) - p).sqrMagnitude < 16f)
            {
                return GroundY(stage.terrain, b.x, b.z);
            }
        }

        return float.NaN;
    }

    static GameObject[] CladdingPrefabs(string levelId)
    {
        if (!AssetDatabase.IsValidFolder(CladdingPrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Game/Art/Environment/Nature/Prefabs", "Cladding");
        }

        GameObject[] result = new GameObject[CliffCladdingPlanner.VariantCount];
        for (int i = 0; i < result.Length; i++)
        {
            string source = RockPrefabFolder + "/Nature_Rock_Medium_" + (i + 1) + "_" + levelId + ".prefab";
            string target = CladdingPrefabFolder + "/Nature_Cliff_Rock_" + (i + 1) + "_" + levelId + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                return null;
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            if (existing == null)
            {
                AssetDatabase.CopyAsset(source, target);
                GameObject contents = PrefabUtility.LoadPrefabContents(target);
                Collider[] colliders = contents.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    Object.DestroyImmediate(colliders[c]);
                }

                LODGroup group = contents.GetComponent<LODGroup>();
                if (group == null)
                {
                    group = contents.AddComponent<LODGroup>();
                }

                group.SetLODs(new LOD[] { new LOD(CladdingCullHeight, contents.GetComponentsInChildren<Renderer>(true)) });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(contents, target);
                PrefabUtility.UnloadPrefabContents(contents);
                existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            }

            result[i] = existing;
        }

        return result;
    }

    // Diagnostics: a 0.6 m radius capsule along every trail centreline must not touch any cladding rock. Temporary mesh colliders
    // on a spare layer are used for the queries and always removed again. Rocks left floating after the removal are removed too.
    static int TrailClearanceCheck(LevelConfig config, Stage stage, Transform folder, Vector3 origin, TerrainData data, float[,] heights)
    {
        List<MeshCollider> temp = new List<MeshCollider>();
        List<int> layers = new List<int>();
        int samples = 0;
        int hits = 0;
        int floating = 0;
        HashSet<GameObject> doomed = new HashSet<GameObject>();
        try
        {
            foreach (Transform rock in folder)
            {
                MeshFilter filter = rock.GetComponentInChildren<MeshFilter>();
                if (filter == null)
                {
                    continue;
                }

                layers.Add(filter.gameObject.layer);
                filter.gameObject.layer = SpareLayer;
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                temp.Add(collider);
            }

            Physics.SyncTransforms();
            for (int t = 0; t < stage.trails.Count; t++)
            {
                List<Vector2> path = stage.trails[t];
                for (int i = 1; i < path.Count; i++)
                {
                    float length = Vector2.Distance(path[i - 1], path[i]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.3f));
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 p = Vector2.Lerp(path[i - 1], path[i], k / (float)steps);
                        float g = GroundY(stage.terrain, p.x, p.y);
                        samples++;
                        Vector3 low = new Vector3(p.x, g + 0.6f, p.y);
                        Vector3 high = new Vector3(p.x, g + 1.16f, p.y);
                        if (Physics.CheckCapsule(low, high, 0.6f, 1 << SpareLayer, QueryTriggerInteraction.Ignore))
                        {
                            hits++;
                            Collider[] near = Physics.OverlapCapsule(low, high, 0.6f, 1 << SpareLayer, QueryTriggerInteraction.Ignore);
                            for (int n = 0; n < near.Length; n++)
                            {
                                doomed.Add(near[n].gameObject);
                            }
                        }
                    }
                }
            }

            // Hidden-path stones and bank steps are walkways too.
            for (int i = 0; i < stage.pathStones.Count; i++)
            {
                Vector3 stone = stage.pathStones[i];
                Vector3 low = new Vector3(stone.x, stone.y + 0.6f, stone.z);
                Vector3 high = new Vector3(stone.x, stone.y + 1.16f, stone.z);
                samples++;
                if (Physics.CheckCapsule(low, high, 0.6f, 1 << SpareLayer, QueryTriggerInteraction.Ignore))
                {
                    hits++;
                    Collider[] near = Physics.OverlapCapsule(low, high, 0.6f, 1 << SpareLayer, QueryTriggerInteraction.Ignore);
                    for (int n = 0; n < near.Length; n++)
                    {
                        doomed.Add(near[n].gameObject);
                    }
                }
            }

            // Removing rocks can leave a neighbour without support, so repeat until nothing floats.
            for (int pass = 0; pass < 4; pass++)
            {
                List<GameObject> loose = FloatingRocks(stage, temp, doomed);
                if (loose.Count == 0)
                {
                    break;
                }

                for (int i = 0; i < loose.Count; i++)
                {
                    doomed.Add(loose[i]);
                }

                floating += loose.Count;
            }
        }
        finally
        {
            for (int i = 0; i < temp.Count; i++)
            {
                if (temp[i] == null)
                {
                    continue;
                }

                GameObject owner = temp[i].gameObject;
                Object.DestroyImmediate(temp[i]);
                owner.layer = layers[i];
            }
        }

        // Every rock touching the capsule was collected in the same pass, so removing them leaves no sample in contact.
        foreach (GameObject rock in doomed)
        {
            Object.DestroyImmediate(rock);
        }

        Debug.Log(config.sceneName + " cladding trail clearance: " + hits + " of " + samples + " samples (0.6 m capsule) touched a cladding rock; removed " + (doomed.Count - floating) + " rocks, 0 remain in contact. Floating rocks removed: " + floating);
        return doomed.Count;
    }

    // A rock floats when its lowest point is clear of the terrain under its footprint and of every other (surviving) rock.
    // Support is probed on a 3 x 3 grid across the footprint: terrain within 0.3 m of the bottom, or another rock within 0.3 m below it.
    static List<GameObject> FloatingRocks(Stage stage, List<MeshCollider> colliders, HashSet<GameObject> removed)
    {
        const float reach = 0.3f;
        List<GameObject> loose = new List<GameObject>();
        for (int i = 0; i < colliders.Count; i++)
        {
            GameObject owner = colliders[i].gameObject;
            if (removed.Contains(owner))
            {
                continue;
            }

            Bounds b = colliders[i].bounds;
            bool supported = false;
            for (int gx = 0; gx < 3 && !supported; gx++)
            {
                for (int gz = 0; gz < 3 && !supported; gz++)
                {
                    float x = Mathf.Lerp(b.min.x, b.max.x, gx / 2f);
                    float z = Mathf.Lerp(b.min.z, b.max.z, gz / 2f);
                    supported = GroundY(stage.terrain, x, z) >= b.min.y - reach;
                }
            }

            if (!supported)
            {
                Collider[] below = Physics.OverlapBox(new Vector3(b.center.x, b.min.y, b.center.z), new Vector3(b.extents.x * 0.6f, reach, b.extents.z * 0.6f), Quaternion.identity, 1 << SpareLayer, QueryTriggerInteraction.Ignore);
                for (int n = 0; n < below.Length && !supported; n++)
                {
                    GameObject other = below[n].gameObject;
                    supported = other != owner && !removed.Contains(other);
                }
            }

            if (!supported)
            {
                loose.Add(owner);
            }
        }

        return loose;
    }
}
}
