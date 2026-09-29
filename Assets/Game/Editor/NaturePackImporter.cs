using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
public static class NaturePackImporter
{
    const string ModelRoot = "Assets/Game/Models/NaturePack";
    const string TextureRoot = "Assets/Game/Textures/NaturePack";
    const string PrefabRoot = "Assets/Game/Prefabs/NaturePack";
    const string MaterialRoot = "Assets/Game/Materials/NaturePack";

    struct TexSet
    {
        public string material;
        public string color;
        public string normal;
        public string rough;
        public bool foliage;
    }

    [MenuItem("Lantern Keeper/Import Nature Pack")]
    public static void ImportFromMenu()
    {
        ImportAll();
    }

    public static void ImportAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before importing the Nature pack.");
            return;
        }

        AssetDatabase.Refresh();
        EnsureFolder(MaterialRoot);
        EnsureFolder(PrefabRoot);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("URP Lit shader was not found.");
            return;
        }

        TexSet[] sets = TextureSets();
        PrepareTextures();
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        for (int i = 0; i < sets.Length; i++)
        {
            materials[sets[i].material] = BuildMaterial(sets[i], lit);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string[] folders = Directory.GetDirectories(ToFull(ModelRoot));
        Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
        List<string> imported = new List<string>();
        for (int i = 0; i < folders.Length; i++)
        {
            string id = Path.GetFileName(folders[i]);
            string fbx = ModelRoot + "/" + id + "/" + id + ".fbx";
            if (!File.Exists(ToFull(fbx)))
            {
                continue;
            }

            ModelImporter model = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (model != null)
            {
                model.globalScale = 1f;
                model.useFileScale = true;
                model.bakeAxisConversion = false;
                model.isReadable = true;
                model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                model.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                model.materialSearch = ModelImporterMaterialSearch.Everywhere;
                model.importAnimation = false;
                model.SaveAndReimport();
            }

            string made = BuildPrefab(id, fbx, materials);
            if (made != null)
            {
                imported.Add(made);
            }
        }

        PolyHavenImporter.RebuildBiomes();
        AssetDatabase.SaveAssets();
        Debug.Log("Nature pack import done. " + imported.Count + " prefabs.\n" + string.Join("\n", imported));
    }

    static TexSet[] TextureSets()
    {
        return new[]
        {
            T("np_leaf_2", "leaf_2", true),
            T("np_leaf_3", "leaf_3", true),
            T("np_leaf_4", "leaf_4", true),
            T("np_bark_1", "bark_1", false),
            T("np_bark_willow", "bark_willow", false),
            T("np_grass", "grass_atlas", true),
            T("np_plants_1", "plants_1", true),
            T("np_plants_2", "plants_2", true),
            T("np_plants_6", "plants_6", true),
            T("np_rock", "rocks_01", false)
        };
    }

    static TexSet T(string material, string prefix, bool foliage)
    {
        TexSet set = new TexSet();
        set.material = material;
        set.color = prefix + "_color.png";
        set.normal = prefix + "_normal.png";
        set.rough = prefix + "_roughness.png";
        set.foliage = foliage;
        return set;
    }

    static void PrepareTextures()
    {
        string full = ToFull(TextureRoot);
        if (!Directory.Exists(full))
        {
            return;
        }

        string[] files = Directory.GetFiles(full, "*.png");
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = ToAsset(files[i]);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (name.EndsWith("_normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (name.EndsWith("_roughness") || name.EndsWith("_smoothness"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.isReadable = name.EndsWith("_roughness");
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
            }

            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    static Material BuildMaterial(TexSet set, Shader lit)
    {
        string colorAsset = TextureRoot + "/" + set.color;
        string normalAsset = TextureRoot + "/" + set.normal;
        string roughAsset = TextureRoot + "/" + set.rough;
        string maskAsset = TextureRoot + "/" + Path.GetFileNameWithoutExtension(set.rough).Replace("_roughness", "_smoothness") + ".png";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(colorAsset);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAsset);
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughAsset);
        Texture2D mask = BuildSmoothnessMask(roughness, roughAsset, maskAsset);
        string matPath = MaterialRoot + "/" + set.material + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = lit;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BaseMap", albedo);
        mat.SetFloat("_Smoothness", 0.25f);
        mat.SetFloat("_Metallic", 0f);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (mask != null)
        {
            mat.SetTexture("_MetallicGlossMap", mask);
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (set.foliage)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.doubleSidedGI = true;
            mat.enableInstancing = true;
        }
        else
        {
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Back);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = (int)RenderQueue.Geometry;
            mat.enableInstancing = true;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D BuildSmoothnessMask(Texture2D roughness, string roughAsset, string maskAsset)
    {
        if (roughness == null)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(maskAsset);
        }

        if (!roughness.isReadable)
        {
            TextureImporter importer = AssetImporter.GetAtPath(roughAsset) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.isReadable = true;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughAsset);
        }

        if (roughness == null || !roughness.isReadable)
        {
            return null;
        }

        Color[] pixels = roughness.GetPixels();
        Texture2D mask = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
        Color[] written = new Color[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            float smooth = 1f - pixels[i].r;
            written[i] = new Color(0f, 0f, 0f, smooth);
        }

        mask.SetPixels(written);
        mask.Apply();
        File.WriteAllBytes(ToFull(maskAsset), mask.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(mask);
        AssetDatabase.ImportAsset(maskAsset);
        TextureImporter maskImporter = AssetImporter.GetAtPath(maskAsset) as TextureImporter;
        if (maskImporter != null)
        {
            maskImporter.sRGBTexture = false;
            maskImporter.textureType = TextureImporterType.Default;
            maskImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            maskImporter.SaveAndReimport();
        }

        TextureImporter roughImporter = AssetImporter.GetAtPath(roughAsset) as TextureImporter;
        if (roughImporter != null && roughImporter.isReadable)
        {
            roughImporter.isReadable = false;
            roughImporter.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(maskAsset);
    }

    static string BuildPrefab(string id, string fbx, Dictionary<string, Material> materials)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null)
        {
            Debug.LogWarning("Nature pack model missing: " + fbx);
            return null;
        }

        BiomeCategory category = CategoryFor(id);
        string folder = PrefabRoot + "/" + category;
        EnsureFolder(folder);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter source = null;
        int best = -1;
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
            {
                continue;
            }

            int tris = filters[i].sharedMesh.triangles.Length / 3;
            if (tris > best)
            {
                best = tris;
                source = filters[i];
            }
        }

        if (source == null)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return null;
        }

        GameObject root = new GameObject(id);
        CopyMesh(source, root, materials);
        AddCollider(root, category);
        string path = folder + "/" + id + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        MeshFilter saved = root.GetComponent<MeshFilter>();
        if (saved != null && saved.sharedMesh != null && !AssetDatabase.Contains(saved.sharedMesh))
        {
            AssetDatabase.AddObjectToAsset(saved.sharedMesh, path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        Bounds bounds = saved != null && saved.sharedMesh != null ? saved.sharedMesh.bounds : new Bounds();
        int triangles = saved != null && saved.sharedMesh != null ? saved.sharedMesh.triangles.Length / 3 : 0;
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(instance);
        return id + " tris=" + triangles + " size=" + bounds.size.x.ToString("0.00") + "x" + bounds.size.y.ToString("0.00") + "x" + bounds.size.z.ToString("0.00");
    }

    static void CopyMesh(MeshFilter source, GameObject destination, Dictionary<string, Material> materials)
    {
        MeshFilter filter = destination.AddComponent<MeshFilter>();
        Mesh mesh = source.sharedMesh;
        Quaternion rotation = source.transform.localRotation;
        if (Quaternion.Angle(rotation, Quaternion.identity) > 0.5f)
        {
            mesh = BakeUpright(source.sharedMesh, rotation);
        }

        filter.sharedMesh = mesh;
        MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
        MeshRenderer renderer = destination.AddComponent<MeshRenderer>();
        Material[] assigned = sourceRenderer != null ? sourceRenderer.sharedMaterials : new Material[0];
        Material[] replaced = new Material[assigned.Length];
        for (int i = 0; i < assigned.Length; i++)
        {
            string name = assigned[i] != null ? assigned[i].name : "";
            int space = name.IndexOf(' ');
            if (space > 0)
            {
                name = name.Substring(0, space);
            }

            Material found;
            if (!materials.TryGetValue(name, out found))
            {
                found = assigned[i];
            }

            replaced[i] = found;
        }

        renderer.sharedMaterials = replaced;
        string id = destination.name.ToLowerInvariant();
        bool cover = id.Contains("grass") || id.Contains("flower") || id.Contains("plant");
        renderer.shadowCastingMode = cover ? ShadowCastingMode.Off : ShadowCastingMode.On;
        renderer.receiveShadows = !cover;
    }

    static Mesh BakeUpright(Mesh source, Quaternion rotation)
    {
        Mesh mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = source.name + "_Upright";
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = rotation * verts[i];
        }

        mesh.vertices = verts;
        if (normals != null && normals.Length == verts.Length)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = rotation * normals[i];
            }

            mesh.normals = normals;
        }

        if (tangents != null && tangents.Length == verts.Length)
        {
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tangent = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }

            mesh.tangents = tangents;
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddCollider(GameObject root, BiomeCategory category)
    {
        if (category == BiomeCategory.GroundCover || category == BiomeCategory.Flower || category == BiomeCategory.Undergrowth)
        {
            return;
        }

        MeshFilter filter = root.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return;
        }

        Bounds bounds = filter.sharedMesh.bounds;
        if (category == BiomeCategory.Tree || category == BiomeCategory.Sapling)
        {
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = Mathf.Max(0.4f, bounds.size.y * 0.42f);
            capsule.radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.22f, 0.16f, 0.55f);
            capsule.center = new Vector3(bounds.center.x, bounds.min.y + capsule.height * 0.5f, bounds.center.z);
            return;
        }

        if (category == BiomeCategory.Rock || category == BiomeCategory.SetPiece || category == BiomeCategory.Deadwood)
        {
            MeshCollider collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;
        }
    }

    static BiomeCategory CategoryFor(string id)
    {
        if (id.StartsWith("np_tree"))
        {
            return BiomeCategory.Tree;
        }

        if (id.StartsWith("np_bush"))
        {
            return BiomeCategory.Sapling;
        }

        if (id.StartsWith("np_grass") || id.StartsWith("np_plant"))
        {
            return BiomeCategory.GroundCover;
        }

        if (id.StartsWith("np_flower"))
        {
            return BiomeCategory.Flower;
        }

        if (id.StartsWith("np_rock"))
        {
            return BiomeCategory.Rock;
        }

        return BiomeCategory.Undergrowth;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }

    static string ToFull(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }

    static string ToAsset(string fullPath)
    {
        string full = Path.GetFullPath(fullPath).Replace('\\', '/');
        string root = Path.GetFullPath(Directory.GetCurrentDirectory()).Replace('\\', '/');
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return full.Substring(root.Length).TrimStart('/');
        }

        return fullPath.Replace('\\', '/');
    }
}
}
