using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    const string CliffSkinRoot = "Assets/Game/Art/Environment/Cliffs";
    public const string CliffSkinName = "CliffSkin";
    const float SkinTrailClearance = 0.6f;

    // Cliff skin: smooth rock walls laid just outside the heightmap's one-cell steps (Spec Amendment 3). Meshes are saved as assets under
    // Art/Environment/Cliffs/<levelId>/, there is no collider (the terrain keeps all collision), and the cull setup matches the cladding rocks.
    static int PlaceCliffSkin(LevelConfig config, Stage stage, Transform parent)
    {
        if (stage.terrain == null || stage.terrain.terrainData == null)
        {
            return 0;
        }

        TerrainData data = stage.terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 origin = stage.terrain.transform.position;
        List<SkinStrip> strips = CliffSkinPlanner.Plan(heights, data.size.x, data.size.y, new Vector2(origin.x, origin.z), CliffSkinPlanner.StepThresholdM);
        string folder = CliffSkinRoot + "/" + config.levelId;
        EnsureFolder(folder);
        if (strips.Count == 0)
        {
            RemoveStaleSkinMeshes(folder, 0);
            Debug.Log(config.sceneName + " CLIFFSKIN strips=0 length=0.0 triangles=0");
            return 0;
        }

        List<SkinChunk> chunks = CliffSkinMesher.Build(strips, p => GroundY(stage.terrain, p.x, p.y), origin.y, config.seed + 90);
        Material material = CliffSkinMaterial(config, folder);
        Transform root = Folder(CliffSkinName);
        root.SetParent(parent, false);

        int triangles = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            SkinChunk chunk = chunks[i];
            Mesh mesh = SaveSkinMesh(folder + "/CliffSkin_" + config.levelId + "_" + i + ".asset", chunk);
            triangles += chunk.triangles.Length / 3;

            GameObject go = new GameObject("CliffSkinChunk" + i);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            LODGroup group = go.AddComponent<LODGroup>();
            group.SetLODs(new LOD[] { new LOD(CladdingCullHeight, new Renderer[] { renderer }) });
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        RemoveStaleSkinMeshes(folder, chunks.Count);
        float length = 0f;
        for (int i = 0; i < strips.Count; i++)
        {
            length += strips[i].length;
        }

        Debug.Log(config.sceneName + " CLIFFSKIN strips=" + strips.Count + " length=" + length.ToString("F1") + " chunks=" + chunks.Count + " triangles=" + triangles);
        ReportSkinTrailClearance(config, stage, chunks);
        return triangles;
    }

    static Mesh SaveSkinMesh(string path, SkinChunk chunk)
    {
        Mesh fresh = new Mesh();
        fresh.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (chunk.vertices.Length > 65000)
        {
            fresh.indexFormat = IndexFormat.UInt32;
        }

        fresh.vertices = chunk.vertices;
        fresh.normals = chunk.normals;
        fresh.uv = chunk.uvs;
        fresh.triangles = chunk.triangles;
        fresh.RecalculateBounds();
        fresh.RecalculateTangents();

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        // Overwrite in place so the asset keeps its GUID and scenes keep their reference.
        EditorUtility.CopySerialized(fresh, existing);
        Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void RemoveStaleSkinMeshes(string folder, int keep)
    {
        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int underscore = name.LastIndexOf('_');
            int index;
            if (underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out index) && index >= keep)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    // One URP Lit material per island: kit rock albedo (the same greyscale texture the terrain faces and the cladding rocks use) times the island's rockTint.
    static Material CliffSkinMaterial(LevelConfig config, string folder)
    {
        string path = folder + "/CliffSkin_" + config.levelId + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null)
        {
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = lit;
        LookProfile look = LoadLookProfile();
        if (look == null)
        {
            look = config.lookProfile;
        }

        if (look == null)
        {
            look = AssetDatabase.LoadAssetAtPath<LookProfile>("Assets/Game/Art/Look/LookProfile_island1.asset");
        }

        Texture2D kitRock = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Environment/Nature/Textures/Rocks_Diffuse_Grey.png");
        Color tint = look != null ? look.rockTint : Color.white;
        mat.SetTexture("_BaseMap", kitRock);
        mat.SetTextureScale("_BaseMap", new Vector2(1f / CliffTileMetres, 1f / CliffTileMetres));
        mat.SetTextureOffset("_BaseMap", Vector2.zero);
        mat.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Metallic", 0f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Diagnostics: plan distance from every skin vertex to every JitteredTrail centreline. A small clip is accepted by the spec; the worst spots are logged.
    static void ReportSkinTrailClearance(LevelConfig config, Stage stage, List<SkinChunk> chunks)
    {
        if (stage.trails == null || stage.trails.Count == 0)
        {
            Debug.Log(config.sceneName + " CLIFFSKIN trail clearance: no trails");
            return;
        }

        float minDistance = float.MaxValue;
        int within = 0;
        int total = 0;
        List<KeyValuePair<float, Vector3>> worst = new List<KeyValuePair<float, Vector3>>();
        for (int c = 0; c < chunks.Count; c++)
        {
            Vector3[] vertices = chunks[c].vertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector2 p = new Vector2(vertices[v].x, vertices[v].z);
                float best = float.MaxValue;
                for (int t = 0; t < stage.trails.Count; t++)
                {
                    List<Vector2> path = stage.trails[t];
                    for (int s = 1; s < path.Count; s++)
                    {
                        float unused;
                        best = Mathf.Min(best, DistanceToSegment(p, path[s - 1], path[s], out unused));
                    }
                }

                total++;
                minDistance = Mathf.Min(minDistance, best);
                if (best < SkinTrailClearance)
                {
                    within++;
                    worst.Add(new KeyValuePair<float, Vector3>(best, vertices[v]));
                }
            }
        }

        worst.Sort((a, b) => a.Key.CompareTo(b.Key));
        string spots = "";
        List<Vector3> listed = new List<Vector3>();
        for (int i = 0; i < worst.Count && listed.Count < 5; i++)
        {
            bool near = false;
            for (int k = 0; k < listed.Count; k++)
            {
                near |= (new Vector2(listed[k].x, listed[k].z) - new Vector2(worst[i].Value.x, worst[i].Value.z)).magnitude < 2f;
            }

            if (near)
            {
                continue;
            }

            listed.Add(worst[i].Value);
            spots += " [d=" + worst[i].Key.ToString("F2") + " at " + worst[i].Value.x.ToString("F1") + "," + worst[i].Value.y.ToString("F1") + "," + worst[i].Value.z.ToString("F1") + "]";
        }

        Debug.Log(config.sceneName + " CLIFFSKIN trail clearance: min plan distance " + (minDistance == float.MaxValue ? "n/a" : minDistance.ToString("F2")) + " m; vertices within "
            + SkinTrailClearance.ToString("F1") + " m: " + within + " of " + total + "; worst spots:" + spots);
    }
}
}
