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
            if (NearTrail(stage, p.x, p.y, 1.7f))
            {
                return true;
            }

            if (NearSpot(stage.beaconSpots, p.x, p.y, 4f))
            {
                return true;
            }

            // Shore: skip faces that never rise above the high-water line. A wall rising out of the sea is kept, since the
            // surface at its xz is a point mid-wall; the highest ground within 2.5 m decides.
            float high = GroundY(stage.terrain, p.x, p.y);
            high = Mathf.Max(high, GroundY(stage.terrain, p.x + 2.5f, p.y));
            high = Mathf.Max(high, GroundY(stage.terrain, p.x - 2.5f, p.y));
            high = Mathf.Max(high, GroundY(stage.terrain, p.x, p.y + 2.5f));
            high = Mathf.Max(high, GroundY(stage.terrain, p.x, p.y - 2.5f));
            return high < seaGuard;
        }, config.seed + 70);

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

        return rocks.Count;
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
}
}
