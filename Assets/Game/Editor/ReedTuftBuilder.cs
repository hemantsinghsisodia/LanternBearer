using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Own-authored marsh reed clump for the terrain detail layer: 12 slightly bent blades 1.0-1.6 m tall, four with a seed head.
// Uses LanternKeeper/Foliage. The tone ramp (island 3 olive at the root to straw, then a brown seed head) is a generated texture sampled along the blade height.
public static class ReedTuftBuilder
{
    public const string MeshPath = "Assets/Game/Meshes/Reeds/ReedTuft.asset";
    public const string MaterialPath = "Assets/Game/Materials/Reeds/ReedTuft.mat";
    public const string PrefabPath = "Assets/Game/Prefabs/Reeds/ReedTuft.prefab";
    public const string RampPath = "Assets/Game/Textures/Reeds/ReedRamp.png";

    // Island 3 tones: olive #5E7238 at the root to straw #B49A5A at the tip; seed head brown.
    static readonly Color Olive = new Color32(0x5E, 0x72, 0x38, 255);
    static readonly Color Straw = new Color32(0xB4, 0x9A, 0x5A, 255);
    static readonly Color Seed = new Color32(0x7A, 0x5A, 0x32, 255);
    const float SeedStart = 0.88f;

    [MenuItem("Lantern Keeper/Nature/Build Reed Tuft")]
    public static void Build()
    {
        EnsurePrefab();
    }

    public static GameObject EnsurePrefab()
    {
        EnsureFolder("Assets/Game/Meshes/Reeds");
        EnsureFolder("Assets/Game/Materials/Reeds");
        EnsureFolder("Assets/Game/Prefabs/Reeds");
        EnsureFolder("Assets/Game/Textures/Reeds");

        Texture2D ramp = EnsureRamp();
        Material material = EnsureMaterial(ramp);
        Mesh mesh = EnsureMesh();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            GameObject instance = new GameObject("ReedTuft");
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer created = instance.AddComponent<MeshRenderer>();
            created.sharedMaterial = material;
            created.shadowCastingMode = ShadowCastingMode.Off;
            created.receiveShadows = false;
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }
        else
        {
            prefab.GetComponentInChildren<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        return prefab;
    }

    static Texture2D EnsureRamp()
    {
        if (!File.Exists(Path.GetFullPath(RampPath)))
        {
            const int height = 64;
            Texture2D texture = new Texture2D(4, height, TextureFormat.RGBA32, false, false);
            for (int y = 0; y < height; y++)
            {
                float t = (y + 0.5f) / height;
                Color c = t < SeedStart ? Color.Lerp(Olive, Straw, Mathf.SmoothStep(0f, 1f, t / SeedStart)) : Seed;
                for (int x = 0; x < 4; x++)
                {
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(RampPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RampPath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(RampPath) as TextureImporter;
        if (importer != null)
        {
            bool differs = !importer.sRGBTexture
                || importer.mipmapEnabled
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.filterMode != FilterMode.Bilinear
                || importer.alphaSource != TextureImporterAlphaSource.None;
            if (differs)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(RampPath);
    }

    static Material EnsureMaterial(Texture2D ramp)
    {
        Shader shader = Shader.Find("LanternKeeper/Foliage");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        mat.shader = shader;
        mat.SetTexture("_BaseMap", ramp);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Cutoff", 0.1f);
        mat.SetFloat("_Desaturate", 0f);
        mat.SetFloat("_SwayHeight", 1.6f);
        mat.SetFloat("_SwayAmount", 0.14f);
        mat.SetFloat("_Wrap", 0.5f);
        mat.enableInstancing = true;
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh EnsureMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "ReedTuft";
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        Fill(mesh);
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static void Fill(Mesh mesh)
    {
        const int blades = 12;
        // Rows along the blade; the seed head widens just after SeedStart.
        float[] rows = { 0f, 0.3f, 0.6f, SeedStart, SeedStart + 0.01f, 0.95f, 1f };
        int rowCount = rows.Length;
        Vector3[] vertices = new Vector3[blades * rowCount * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[blades * (rowCount - 1) * 6];

        for (int b = 0; b < blades; b++)
        {
            float yaw = b * 137.5f;
            float radius = 0.03f + (b % 4) * 0.03f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            // Blades fan outwards a little and bend further towards the tip.
            Vector3 origin = Quaternion.Euler(0f, b * 61f, 0f) * Vector3.forward * radius;
            Vector3 outward = origin.sqrMagnitude > 0.0001f ? origin.normalized : forward;
            float height = 1.0f + 0.6f * ((b * 5) % 12) / 11f;
            float bend = (0.10f + 0.08f * (b % 3)) * height;
            bool head = b % 3 == 0;

            int baseIndex = b * rowCount * 2;
            for (int r = 0; r < rowCount; r++)
            {
                float t = rows[r];
                float half = Mathf.Lerp(0.018f, 0.006f, t / SeedStart);
                if (t > SeedStart)
                {
                    half = head ? Mathf.Lerp(0.03f, 0.002f, (t - SeedStart - 0.01f) / (1f - SeedStart - 0.01f)) : Mathf.Lerp(0.006f, 0.001f, (t - SeedStart) / (1f - SeedStart));
                }

                Vector3 center = origin + outward * (0.12f * t * height) + Vector3.up * (t * height) + forward * (bend * t * t);
                int i0 = baseIndex + r * 2;
                vertices[i0] = center - right * half;
                vertices[i0 + 1] = center + right * half;
                Vector3 n = (Vector3.up * 0.8f + outward * 0.3f).normalized;
                normals[i0] = n;
                normals[i0 + 1] = n;
                uvs[i0] = new Vector2(0.5f, t);
                uvs[i0 + 1] = new Vector2(0.5f, t);
            }

            int indexBase = b * (rowCount - 1) * 6;
            for (int r = 0; r < rowCount - 1; r++)
            {
                int v0 = baseIndex + r * 2;
                int t0 = indexBase + r * 6;
                triangles[t0] = v0;
                triangles[t0 + 1] = v0 + 2;
                triangles[t0 + 2] = v0 + 1;
                triangles[t0 + 3] = v0 + 1;
                triangles[t0 + 4] = v0 + 2;
                triangles[t0 + 5] = v0 + 3;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
}
