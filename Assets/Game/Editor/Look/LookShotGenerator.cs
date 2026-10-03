using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LanternKeeper
{
public static class LookShotGenerator
{
    public const string ShotFolder = "Assets/Game/Art/Look/Shots";
    public static readonly string[] LevelIds = { "island1", "island2", "island3", "island4" };

    const float DefaultFov = 50f;
    const float GrassFov = 60f;
    const float CliffGrid = 8f;
    const float CliffSpan = 2f;

    public static string AssetPath(string levelId)
    {
        return ShotFolder + "/LookShots_" + levelId + ".asset";
    }

    public static string ScenePath(string levelId)
    {
        return "Assets/Game/Scenes/Island" + levelId.Substring("island".Length) + ".unity";
    }

    [MenuItem("Lantern Keeper/Look/Generate Shot Lists")]
    public static void Generate()
    {
        bool overwrite = false;
        for (int i = 0; i < LevelIds.Length; i++)
        {
            if (File.Exists(AssetPath(LevelIds[i])))
            {
                overwrite = true;
            }
        }

        if (overwrite && !EditorUtility.DisplayDialog("Regenerate shot lists", "Shot lists already exist. Regenerating replaces the poses used for the baseline.", "Regenerate", "Cancel"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string previous = EditorSceneManager.GetActiveScene().path;
        Directory.CreateDirectory(ShotFolder);
        for (int i = 0; i < LevelIds.Length; i++)
        {
            EditorSceneManager.OpenScene(ScenePath(LevelIds[i]), OpenSceneMode.Single);
            LookShotList list = ScriptableObject.CreateInstance<LookShotList>();
            list.levelId = LevelIds[i];
            list.shots = Build(LevelIds[i]);
            string path = AssetPath(LevelIds[i]);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(list, path);
            Debug.Log("Look shots: " + path + " (" + list.shots.Count + " shots)");
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous))
        {
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }

    static List<LookShot> Build(string levelId)
    {
        List<LookShot> shots = new List<LookShot>();
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        Terrain terrain = Terrain.activeTerrain;
        WaterHazard hazard = Object.FindAnyObjectByType<WaterHazard>();
        if (player == null || terrain == null || hazard == null)
        {
            Debug.LogError("Look shots: " + levelId + " is missing the player, terrain or water hazard.");
            return shots;
        }

        float water = new SerializedObject(hazard).FindProperty("surfaceY").floatValue;
        Vector3 spawn = player.transform.position;

        // Gameplay camera start pose: CameraFollow.SnapBehind with its pitch of 22 and configured distance.
        CameraFollow follow = Object.FindAnyObjectByType<CameraFollow>();
        float distance = 6f;
        float pivotHeight = 1.6f;
        if (follow != null)
        {
            SerializedObject so = new SerializedObject(follow);
            distance = so.FindProperty("distance").floatValue;
            pivotHeight = so.FindProperty("pivotHeight").floatValue;
        }

        float yaw = player.transform.eulerAngles.y;
        Vector3 pivot = spawn + Vector3.up * pivotHeight;
        Quaternion orbit = Quaternion.Euler(22f, yaw, 0f);
        Vector3 spawnCam = pivot + orbit * Vector3.back * distance;
        spawnCam.y = Mathf.Max(spawnCam.y, Height(terrain, spawnCam) + 0.4f);
        Quaternion spawnRot = Quaternion.LookRotation((pivot - spawnCam).normalized, Vector3.up);
        shots.Add(Shot("spawn", spawnCam, spawnRot, DefaultFov));

        Beacon beacon = NearestBeacon(spawn);
        if (beacon != null)
        {
            Vector3 top = BeaconTop(beacon);
            Vector3 flat = beacon.transform.position - spawn;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
            {
                flat = Vector3.forward;
            }

            // Camera on the player's side of the beacon, 7 m out.
            Vector3 from = beacon.transform.position - flat.normalized * 7f;
            from.y = beacon.transform.position.y + 2.5f;
            from.y = Mathf.Max(from.y, Height(terrain, from) + 1.2f);
            shots.Add(Shot("beacon", from, Quaternion.LookRotation((top - from).normalized, Vector3.up), DefaultFov));
        }
        else
        {
            Debug.LogError("Look shots: " + levelId + " has no beacon.");
        }

        Vector3 center = terrain.transform.position + new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
        Vector3 shore = center;
        bool found = false;
        for (float x = 0f; x < terrain.terrainData.size.x; x += 0.5f)
        {
            Vector3 p = new Vector3(center.x + x, 0f, center.z);
            if (Height(terrain, p) < water)
            {
                shore = p;
                shore.y = water;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError("Look shots: " + levelId + " has no shoreline along +X.");
        }

        Vector3 inland = shore - Vector3.right * 6f;
        inland.y = Height(terrain, inland) + 2f;
        Vector3 seaPoint = shore + Vector3.right * 30f;
        seaPoint.y = water;
        shots.Add(Shot("shoreline", inland, Quaternion.LookRotation((seaPoint - inland).normalized, Vector3.up), DefaultFov));

        Vector3 cliff;
        Vector3 downhill;
        FindCliff(terrain, water, out cliff, out downhill);
        Vector3 cliffCam = cliff + downhill * 10f;
        cliffCam.y = Mathf.Max(cliff.y, Height(terrain, cliffCam) + 2f);
        shots.Add(Shot("cliff", cliffCam, Quaternion.LookRotation((cliff - cliffCam).normalized, Vector3.up), DefaultFov));

        Vector3 waterCam = shore + Vector3.right * 4f;
        waterCam.y = water + 1.2f;
        shots.Add(Shot("water", waterCam, Quaternion.Euler(25f, 90f, 0f), DefaultFov));

        Vector3 behind = spawn - player.transform.forward * 1.5f;
        behind.y = Height(terrain, behind) + 0.6f;
        shots.Add(Shot("grass", behind, Quaternion.LookRotation((spawn - behind).normalized, Vector3.up), GrassFov));

        Vector3 mothCam = spawnCam + spawnRot * Vector3.forward * 1.5f;
        shots.Add(Shot("moth", mothCam, spawnRot, DefaultFov));
        if (levelId == "island4")
        {
            shots.Add(Shot("shade", mothCam, spawnRot, DefaultFov));
        }

        return shots;
    }

    static LookShot Shot(string label, Vector3 position, Quaternion rotation, float fov)
    {
        LookShot shot = new LookShot();
        shot.label = label;
        shot.position = position;
        shot.euler = rotation.eulerAngles;
        shot.fov = fov;
        return shot;
    }

    static float Height(Terrain terrain, Vector3 world)
    {
        return terrain.SampleHeight(world) + terrain.transform.position.y;
    }

    static Beacon NearestBeacon(Vector3 from)
    {
        Beacon[] all = Object.FindObjectsByType<Beacon>();
        Beacon best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < all.Length; i++)
        {
            float sqr = (all[i].transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = all[i];
            }
        }

        return best;
    }

    static Vector3 BeaconTop(Beacon beacon)
    {
        Renderer[] renderers = beacon.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(beacon.transform.position, Vector3.zero);
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer)
            {
                continue;
            }

            if (!any)
            {
                bounds = renderers[i].bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return new Vector3(beacon.transform.position.x, any ? bounds.max.y : beacon.transform.position.y + 2f, beacon.transform.position.z);
    }

    // Steepest sample on an 8 m grid: largest height change over 2 m. The camera backs off downhill so it faces the slope.
    static void FindCliff(Terrain terrain, float water, out Vector3 point, out Vector3 downhill)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float best = -1f;
        point = origin + size * 0.5f;
        downhill = Vector3.right;
        for (float x = CliffGrid; x < size.x - CliffGrid; x += CliffGrid)
        {
            for (float z = CliffGrid; z < size.z - CliffGrid; z += CliffGrid)
            {
                Vector3 p = origin + new Vector3(x, 0f, z);
                float h = Height(terrain, p);
                if (h < water + 1f)
                {
                    continue;
                }

                float dx = Height(terrain, p + Vector3.right * CliffSpan) - h;
                float dz = Height(terrain, p + Vector3.forward * CliffSpan) - h;
                float delta = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
                if (delta > best)
                {
                    best = delta;
                    p.y = h;
                    point = p;
                    Vector3 away = new Vector3(dx, 0f, dz);
                    if (away.sqrMagnitude < 0.0001f)
                    {
                        away = Vector3.left;
                    }

                    // The gradient points uphill, so the camera goes the other way.
                    downhill = -away.normalized;
                }
            }
        }
    }
}
}
