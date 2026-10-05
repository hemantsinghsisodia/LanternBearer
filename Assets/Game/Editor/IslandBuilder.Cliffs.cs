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
            Debug.LogWarning(config.sceneName + ": cladding prefabs missing, skipped");
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

    // Height of the trail or beacon ring ground within reach of p (trail 1.3 m, beacon 4 m), NaN when p is free.
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
    // on a spare layer are used for the query and removed again. Also logs where a trail passes within 4 m of a steep cell.
    static int TrailClearanceCheck(LevelConfig config, Stage stage, Transform folder, Vector3 origin, TerrainData data, float[,] heights)
    {
        const int spareLayer = 31;
        List<MeshCollider> temp = new List<MeshCollider>();
        List<int> layers = new List<int>();
        foreach (Transform rock in folder)
        {
            MeshFilter filter = rock.GetComponentInChildren<MeshFilter>();
            if (filter == null)
            {
                continue;
            }

            layers.Add(filter.gameObject.layer);
            filter.gameObject.layer = spareLayer;
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            temp.Add(collider);
        }

        Physics.SyncTransforms();
        int samples = 0;
        int hits = 0;
        HashSet<GameObject> doomed = new HashSet<GameObject>();
        int cliffLogged = 0;
        int cols = heights.GetLength(1);
        float cell = data.size.x / (cols - 1);
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
                    if (Physics.CheckCapsule(new Vector3(p.x, g + 0.6f, p.y), new Vector3(p.x, g + 1.16f, p.y), 0.6f, 1 << spareLayer, QueryTriggerInteraction.Ignore))
                    {
                        hits++;
                        Collider[] near = Physics.OverlapCapsule(new Vector3(p.x, g + 0.6f, p.y), new Vector3(p.x, g + 1.16f, p.y), 0.6f, 1 << spareLayer, QueryTriggerInteraction.Ignore);
                        for (int n = 0; n < near.Length; n++)
                        {
                            doomed.Add(near[n].gameObject);
                        }
                    }

                    if (cliffLogged < 3 && k == 0 && i % 6 == 0)
                    {
                        // Nearest steep cell within 5 m (sparse scan).
                        for (float dx = -5f; dx <= 5f && cliffLogged < 3; dx += 1f)
                        {
                            for (float dz = -5f; dz <= 5f; dz += 1f)
                            {
                                int cx = Mathf.RoundToInt((p.x + dx - origin.x) / cell);
                                int cz = Mathf.RoundToInt((p.y + dz - origin.z) / cell);
                                if (cx < 1 || cz < 1 || cx >= cols - 1 || cz >= cols - 1)
                                {
                                    continue;
                                }

                                float gx = (heights[cz, cx + 1] - heights[cz, cx - 1]) * data.size.y / (2f * cell);
                                float gz = (heights[cz + 1, cx] - heights[cz - 1, cx]) * data.size.y / (2f * cell);
                                if (Mathf.Sqrt(gx * gx + gz * gz) > 1f)
                                {
                                    Debug.Log(config.sceneName + " TRAILCLIFF trail=" + p.x.ToString("F1") + "," + g.ToString("F1") + "," + p.y.ToString("F1")
                                        + " steep=" + (p.x + dx).ToString("F1") + "," + (p.y + dz).ToString("F1"));
                                    cliffLogged++;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        for (int i = 0; i < temp.Count; i++)
        {
            GameObject owner = temp[i].gameObject;
            Object.DestroyImmediate(temp[i]);
            owner.layer = layers[i];
        }

        // Every rock touching the capsule was collected in the same pass, so removing them leaves no sample in contact.
        foreach (GameObject rock in doomed)
        {
            Object.DestroyImmediate(rock);
        }

        Debug.Log(config.sceneName + " cladding trail clearance: " + hits + " of " + samples + " samples (0.6 m capsule) touched a cladding rock; removed " + doomed.Count + " rocks, 0 remain in contact");
        return doomed.Count;
    }
}
}
