using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Island 5's walkway clearance band. Props (rocks, logs, trees) and the gap set pieces were placed with no regard for the spiral path
// above the last trail point, so boulders sat on the summit walkway and the keeper could wedge between them. The band is the path
// width plus 1 m around every SpireLayout path sample (the summit span included) and the lamp plateau disc. This step (a post step of
// BuildIsland5, also in the menu) removes every prop whose collider reaches into the band, and strips the colliders of set pieces and
// cladding that do, in the saved scene, through EditorSceneManager. No terrain rebuild. The builder's placement keeps the same band
// clear in the first place (IslandBuilder.Blocked / PlaceSpireSetPieces use BandSamples).
public static class Island5ClearanceInstaller
{
    public const string ScenePath = "Assets/Game/Scenes/Island5.unity";
    // Half of (PathWidth + 1 m).
    public const float BandHalfWidth = (SpireLayout.PathWidth + 1f) * 0.5f;
    const float SampleSpacing = 0.5f;
    const float PlateauSpacing = 1.5f;
    const float PlateauProbeRadius = 1f;

    [MenuItem("Lantern Keeper/Clear Island 5 Walkway Band")]
    public static void InstallMenu()
    {
        Install();
    }

    // Path samples (XZ) every SampleSpacing metres from the beach to the lamp plateau edge, summit span included.
    public static List<Vector2> BandSamples()
    {
        List<Vector2> result = new List<Vector2>();
        Vector2 previous = SpireLayout.PathPoint(0f);
        float travelled = SampleSpacing;
        const int steps = 8000;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = SpireLayout.PathPoint(i / (float)steps);
            travelled += Vector2.Distance(previous, point);
            previous = point;
            if (travelled >= SampleSpacing)
            {
                travelled = 0f;
                result.Add(point);
            }
        }

        return result;
    }

    // Plateau grid (XZ) inside the lamp plateau disc.
    public static List<Vector2> PlateauSamples()
    {
        List<Vector2> result = new List<Vector2>();
        float limit = SpireLayout.SummitPlateauRadius;
        for (float x = -limit; x <= limit; x += PlateauSpacing)
        {
            for (float z = -limit; z <= limit; z += PlateauSpacing)
            {
                if (x * x + z * z <= limit * limit)
                {
                    result.Add(new Vector2(x, z));
                }
            }
        }

        return result;
    }

    public static bool NearAny(List<Vector2> points, Vector2 xz, float radius)
    {
        float sqr = radius * radius;
        for (int i = 0; i < points.Count; i++)
        {
            if ((points[i] - xz).sqrMagnitude < sqr)
            {
                return true;
            }
        }

        return false;
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        Terrain terrain = null;
        Transform props = null;
        Transform pieces = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Island")
            {
                terrain = root.GetComponent<Terrain>();
            }
            else if (root.name == "Props")
            {
                props = root.transform;
            }
            else if (root.name == "GapSetPieces")
            {
                pieces = root.transform;
            }
        }

        if (terrain == null)
        {
            Debug.LogError("Island5ClearanceInstaller: Island 5 needs its terrain. Build Island 5 first.");
            return;
        }

        Physics.SyncTransforms();
        HashSet<Transform> doomedProps = new HashSet<Transform>();
        HashSet<Collider> stripped = new HashSet<Collider>();
        List<Vector2> path = BandSamples();
        for (int i = 0; i < path.Count; i++)
        {
            Collect(terrain, path[i], BandHalfWidth, props, pieces, doomedProps, stripped);
        }

        List<Vector2> plateau = PlateauSamples();
        for (int i = 0; i < plateau.Count; i++)
        {
            Collect(terrain, plateau[i], PlateauProbeRadius, props, pieces, doomedProps, stripped);
        }

        foreach (Transform prop in doomedProps)
        {
            if (prop != null)
            {
                Debug.Log("Island5ClearanceInstaller: removed " + prop.name + " at " + prop.position.ToString("F1"));
                Object.DestroyImmediate(prop.gameObject);
            }
        }

        foreach (Collider collider in stripped)
        {
            if (collider != null)
            {
                Debug.Log("Island5ClearanceInstaller: stripped the collider of " + collider.name + " at " + collider.transform.position.ToString("F1"));
                Object.DestroyImmediate(collider);
            }
        }

        if (doomedProps.Count + stripped.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("Island5ClearanceInstaller: " + doomedProps.Count + " props removed, " + stripped.Count + " set-piece or cladding colliders stripped from the walkway band (" + path.Count + " path samples, " + plateau.Count + " plateau samples).");
    }

    static void Collect(Terrain terrain, Vector2 xz, float radius, Transform props, Transform pieces, HashSet<Transform> doomedProps, HashSet<Collider> stripped)
    {
        float ground = terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;
        Collider[] near = Physics.OverlapSphere(new Vector3(xz.x, ground + 1.1f, xz.y), radius, ~0, QueryTriggerInteraction.Ignore);
        for (int n = 0; n < near.Length; n++)
        {
            Collider collider = near[n];
            if (collider is TerrainCollider)
            {
                continue;
            }

            if (props != null && collider.transform.IsChildOf(props))
            {
                Transform top = collider.transform;
                while (top.parent != props)
                {
                    top = top.parent;
                }

                // Cladding and the cliff skin are one big folder of rocks: strip the colliders, never the folder.
                if (top.name == "CliffCladding" || top.name == "CliffSkin" || top.childCount > 100)
                {
                    stripped.Add(collider);
                }
                else
                {
                    doomedProps.Add(top);
                }
            }
            else if (pieces != null && collider.transform.IsChildOf(pieces))
            {
                stripped.Add(collider);
            }
        }
    }

    static Scene OpenScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == ScenePath)
            {
                SceneManager.SetActiveScene(loaded);
                return loaded;
            }
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
}
