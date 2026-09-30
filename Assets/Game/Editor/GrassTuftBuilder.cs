using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Own-authored grass clump for the terrain carpet. Crossed blade ribbons, under 300 triangles.
// The blade texture is generated here (no third-party art). Wind uses LanternKeeper/GrassBend.
public static class GrassTuftBuilder
{
    public const string MeshPath = "Assets/Game/Meshes/Grass/GrassTuft.asset";
    public const string MaterialPath = "Assets/Game/Materials/Grass/GrassTuft.mat";
    public const string PrefabPath = "Assets/Game/Prefabs/Grass/GrassTuft.prefab";
    public const string AlbedoPath = "Assets/Game/Textures/Grass/GrassBlade.png";

    public static GameObject EnsurePrefab()
    {
        EnsureFolder("Assets/Game/Meshes");
        EnsureFolder("Assets/Game/Meshes/Grass");
        EnsureFolder("Assets/Game/Textures");
        EnsureFolder("Assets/Game/Textures/Grass");
        EnsureFolder("Assets/Game/Materials");
        EnsureFolder("Assets/Game/Materials/Grass");
        EnsureFolder("Assets/Game/Prefabs");
        EnsureFolder("Assets/Game/Prefabs/Grass");

        Mesh mesh = EnsureMesh();
        Material material = EnsureMaterial();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            GameObject instance = new GameObject("GrassTuft");
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
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        return prefab;
    }

    static Mesh EnsureMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "GrassTuft";
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        Fill(mesh);
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static void Fill(Mesh mesh)
    {
        const int blades = 12;
        const int segments = 4;
        const float targetHeight = 0.42f;
        const float targetRadius = 0.20f;

        int rows = segments + 1;
        Vector3[] vertices = new Vector3[blades * rows * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[blades * segments * 6];

        for (int b = 0; b < blades; b++)
        {
            float yaw = (b * 30f) + ((b % 3) * 7f);
            float height = b % 3 == 0 ? 0.30f : (b % 3 == 1 ? 0.42f : 0.36f);
            float width = 0.012f + (b % 4) * 0.003f;
            float lean = ((b % 2 == 0) ? 0.09f : -0.06f) * height;
            float side = ((b % 5) - 2) * 0.01f;
            float u0 = 0.08f + (b % 3) * 0.02f;
            float u1 = 0.92f - (b % 2) * 0.02f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 origin = forward * (0.015f + (b % 3) * 0.012f) + right * side;

            int vertexBase = b * rows * 2;
            for (int s = 0; s < rows; s++)
            {
                float t = s / (float)segments;
                float curve = t * t;
                float half = width * Mathf.Lerp(1f, 0.22f, t) * 0.5f;
                Vector3 center = origin + Vector3.up * (t * height) + forward * (lean * curve);
                int i0 = vertexBase + s * 2;
                vertices[i0] = center - right * half;
                vertices[i0 + 1] = center + right * half;
                uvs[i0] = new Vector2(u0, t);
                uvs[i0 + 1] = new Vector2(u1, t);
            }

            int indexBase = b * segments * 6;
            for (int s = 0; s < segments; s++)
            {
                int v0 = vertexBase + s * 2;
                int t0 = indexBase + s * 6;
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
        mesh.RecalculateBounds();
        Vector3 size = mesh.bounds.size;
        Vector3 boundsCenter = mesh.bounds.center;
        float yScale = size.y > 0.001f ? targetHeight / size.y : 1f;
        float xz = Mathf.Max(size.x, size.z);
        float xzScale = xz > 0.001f ? (targetRadius * 2f) / xz : 1f;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 local = vertices[i] - boundsCenter;
            local.x *= xzScale;
            local.z *= xzScale;
            local.y *= yScale;
            local.y += targetHeight * 0.5f;
            vertices[i] = local;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        BiasNormals(mesh, blades, rows);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    static void BiasNormals(Mesh mesh, int blades, int rows)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = new Vector3[vertices.Length];
        for (int b = 0; b < blades; b++)
        {
            int vertexBase = b * rows * 2;
            Vector3 bottom = (vertices[vertexBase] + vertices[vertexBase + 1]) * 0.5f;
            Vector3 top = (vertices[vertexBase + (rows - 1) * 2] + vertices[vertexBase + (rows - 1) * 2 + 1]) * 0.5f;
            Vector3 right = (vertices[vertexBase + 1] - vertices[vertexBase]).normalized;
            Vector3 along = (top - bottom).normalized;
            Vector3 face = Vector3.Cross(right, along).normalized;
            if (face.sqrMagnitude < 0.001f)
            {
                face = Vector3.forward;
            }

            Vector3 biased = face.sqrMagnitude > 0.001f ? face.normalized : Vector3.up;
            for (int s = 0; s < rows; s++)
            {
                int i0 = vertexBase + s * 2;
                normals[i0] = biased;
                normals[i0 + 1] = biased;
            }
        }

        mesh.normals = normals;
    }

    static Material EnsureMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/GrassBend");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        Texture albedo = EnsureBladeTexture();
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_BumpMap", null);

        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.3f);
        material.SetFloat("_BumpScale", 0f);
        material.SetFloat("_TipHeight", 0.42f);
        material.enableInstancing = true;
        material.doubleSidedGI = true;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture EnsureBladeTexture()
    {
        const int width = 64;
        const int height = 256;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        float half = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float taper = Mathf.Lerp(1f, 0.08f, Mathf.SmoothStep(0f, 1f, v));
            Color low = new Color(0.40f, 0.52f, 0.16f, 1f);
            Color mid = new Color(0.55f, 0.64f, 0.22f, 1f);
            Color tip = new Color(0.72f, 0.74f, 0.34f, 1f);
            Color body = v < 0.55f ? Color.Lerp(low, mid, v / 0.55f) : Color.Lerp(mid, tip, (v - 0.55f) / 0.45f);
            float vein = 0.88f + 0.12f * Mathf.Sin(v * 36f);
            for (int x = 0; x < width; x++)
            {
                float dx = (x - half) / half;
                float shade = Mathf.Lerp(0.9f, 1.08f, 1f - Mathf.Abs(dx)) * vein;
                Color color = body * shade;
                float alpha = Mathf.Abs(dx) < taper * 0.72f ? 1f : 0f;
                pixels[y * width + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(AlbedoPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(AlbedoPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AlbedoPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture>(AlbedoPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string leaf = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
}
