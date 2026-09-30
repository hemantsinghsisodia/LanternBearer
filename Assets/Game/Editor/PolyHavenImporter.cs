using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
public static class PolyHavenImporter
{
    const string ModelRoot = "Assets/Game/Models/PolyHaven";
    const string PrefabRoot = "Assets/Game/Prefabs/PolyHaven";
    const string MaterialRoot = "Assets/Game/Materials/PolyHaven";

    struct Rule
    {
        public string id;
        public BiomeCategory category;
        public int total;
        public Vector2 scale;
        public bool align;
        public bool limitSlope;
        public float minSlope;
        public float maxSlope;
        public bool limitHeight;
        public float minHeight;
        public float maxHeight;
    }

    static HashSet<string> onlyIds;

    [MenuItem("Lantern Keeper/Import Poly Haven Assets")]
    public static void ImportFromMenu()
    {
        ImportAll();
    }

    public static void ImportOnly(string csv)
    {
        onlyIds = new HashSet<string>(csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
        try
        {
            ImportAll();
        }
        finally
        {
            onlyIds = null;
        }
    }

    static bool Wanted(string id)
    {
        return onlyIds == null || onlyIds.Contains(id);
    }

    public static void ImportAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before importing Poly Haven assets.");
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

        string[] folders = Directory.GetDirectories(ToFull(ModelRoot));
        List<string> imported = new List<string>();
        List<string> skipped = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < folders.Length; i++)
            {
                string id = Path.GetFileName(folders[i]);
                if (!Wanted(id))
                {
                    continue;
                }

                string fbx = ModelRoot + "/" + id + "/" + id + ".fbx";
                if (!File.Exists(ToFull(fbx)))
                {
                    skipped.Add(id + " (no fbx)");
                    continue;
                }

                PrepareTextures(ModelRoot + "/" + id);
                ModelImporter model = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (model != null)
                {
                    model.useFileScale = true;
                    model.globalScale = model.fileScale > 0f && model.fileScale < 0.5f ? 1f / model.fileScale : 1f;
                    model.isReadable = true;
                    model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    model.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    model.materialSearch = ModelImporterMaterialSearch.Everywhere;
                    model.importAnimation = false;
                    model.SaveAndReimport();
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        for (int i = 0; i < folders.Length; i++)
        {
            string id = Path.GetFileName(folders[i]);
            if (!Wanted(id))
            {
                continue;
            }

            string folder = ModelRoot + "/" + id;
            if (!File.Exists(ToFull(folder + "/" + id + ".fbx")))
            {
                continue;
            }

            BuildMaterials(folder, lit, materials);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        for (int i = 0; i < folders.Length; i++)
        {
            string id = Path.GetFileName(folders[i]);
            if (!Wanted(id))
            {
                continue;
            }

            string fbx = ModelRoot + "/" + id + "/" + id + ".fbx";
            if (!File.Exists(ToFull(fbx)))
            {
                continue;
            }

            int made = BuildPrefabs(id, fbx, materials);
            if (made > 0)
            {
                imported.Add(id + " (" + made + " prefabs)");
            }
            else
            {
                skipped.Add(id + " (no meshes)");
            }
        }

        AttachHeroes();
        if (onlyIds == null)
        {
            CreateBiomes();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Poly Haven import done. Imported " + imported.Count + " assets. Skipped " + skipped.Count + ".\n" + string.Join("\n", imported) + (skipped.Count > 0 ? "\nSkipped:\n" + string.Join("\n", skipped) : ""));
    }

    static void PrepareTextures(string folder)
    {
        string full = ToFull(folder);
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
            }

            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    static void BuildMaterials(string folder, Shader lit, Dictionary<string, Material> materials)
    {
        string full = ToFull(folder);
        string[] files = Directory.GetFiles(full, "*_albedo.png");
        EnsureFolder(MaterialRoot);
        for (int i = 0; i < files.Length; i++)
        {
            string albedoAsset = ToAsset(files[i]);
            string baseName = Path.GetFileNameWithoutExtension(albedoAsset);
            if (baseName.EndsWith("_albedo"))
            {
                baseName = baseName.Substring(0, baseName.Length - "_albedo".Length);
            }

            string normalAsset = folder + "/" + baseName + "_normal.png";
            string roughAsset = folder + "/" + baseName + "_roughness.png";
            string maskAsset = folder + "/" + baseName + "_smoothness.png";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoAsset);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAsset);
            Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughAsset);
            Texture2D mask = BuildSmoothnessMask(roughness, roughAsset, maskAsset);
            string matPath = MaterialRoot + "/" + baseName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.shader = lit;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetFloat("_Smoothness", 0.35f);
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

            bool foliage = IsFoliage(baseName);
            if (foliage)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.35f);
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
            materials[baseName] = mat;
        }
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

    static int BuildPrefabs(string id, string fbx, Dictionary<string, Material> materials)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null)
        {
            return 0;
        }

        BiomeCategory category = CategoryFor(id);
        string folder = PrefabRoot + "/" + category;
        EnsureFolder(folder);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
        Dictionary<string, List<MeshFilter>> groups = new Dictionary<string, List<MeshFilter>>();
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter.sharedMesh == null || SkipName(filter.gameObject.name))
            {
                continue;
            }

            string key = LodKey(filter.gameObject.name);
            List<MeshFilter> list;
            if (!groups.TryGetValue(key, out list))
            {
                list = new List<MeshFilter>();
                groups[key] = list;
            }

            list.Add(filter);
        }

        int made = 0;
        HashSet<string> used = new HashSet<string>();
        foreach (KeyValuePair<string, List<MeshFilter>> pair in groups)
        {
            List<MeshFilter> lods = pair.Value;
            lods.Sort((a, b) => LodIndex(a.gameObject.name).CompareTo(LodIndex(b.gameObject.name)));
            string prefabName = UniqueName(pair.Key, used);
            GameObject root = new GameObject(prefabName);
            bool multi = HasMultipleLods(lods);
            if (!multi)
            {
                CopyMesh(lods[0], root, materials);
                AddCollider(root, category);
            }
            else
            {
                LODGroup group = root.AddComponent<LODGroup>();
                LOD[] levels = new LOD[lods.Count];
                for (int i = 0; i < lods.Count; i++)
                {
                    GameObject child = new GameObject(lods[i].gameObject.name);
                    child.transform.SetParent(root.transform, false);
                    CopyMesh(lods[i], child, materials);
                    float height = LodScreenHeight(i, lods.Count);
                    levels[i] = new LOD(height, child.GetComponentsInChildren<Renderer>());
                }

                group.SetLODs(levels);
                group.RecalculateBounds();
                AddCollider(root, category);
            }

            string path = folder + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            MeshFilter[] savedFilters = root.GetComponentsInChildren<MeshFilter>();
            bool addedMesh = false;
            for (int m = 0; m < savedFilters.Length; m++)
            {
                Mesh savedMesh = savedFilters[m].sharedMesh;
                if (savedMesh != null && !AssetDatabase.Contains(savedMesh))
                {
                    AssetDatabase.AddObjectToAsset(savedMesh, path);
                    addedMesh = true;
                }
            }

            if (addedMesh)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            UnityEngine.Object.DestroyImmediate(root);
            made++;
        }

        UnityEngine.Object.DestroyImmediate(instance);
        return made;
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

    static void CopyMesh(MeshFilter source, GameObject destination, Dictionary<string, Material> materials)
    {
        MeshFilter filter = destination.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = destination.AddComponent<MeshFilter>();
        }

        Mesh mesh = source.sharedMesh;
        Quaternion rotation = source.transform.localRotation;
        if (Quaternion.Angle(rotation, Quaternion.identity) > 0.5f)
        {
            mesh = BakeUpright(source.sharedMesh, rotation);
        }

        filter.sharedMesh = mesh;
        MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
        MeshRenderer renderer = destination.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = destination.AddComponent<MeshRenderer>();
        }

        Material[] assigned = sourceRenderer != null ? sourceRenderer.sharedMaterials : new Material[0];
        Material[] replaced = new Material[assigned.Length];
        for (int i = 0; i < assigned.Length; i++)
        {
            string name = assigned[i] != null ? Clean(assigned[i].name) : "";
            Material found;
            if (!materials.TryGetValue(name, out found))
            {
                found = FindMaterial(name, materials);
            }

            replaced[i] = found != null ? found : assigned[i];
        }

        renderer.sharedMaterials = replaced;
        bool cover = IsCoverName(destination.name) || IsCoverRenderer(replaced);
        renderer.shadowCastingMode = cover ? ShadowCastingMode.Off : ShadowCastingMode.On;
        renderer.receiveShadows = !cover;
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

    public static void RebuildBiomes()
    {
        AttachHeroes();
        CreateBiomes();
        AssetDatabase.SaveAssets();
    }

    static void AttachHeroes()
    {
        string[] ids = { "jacaranda_tree", "tree_small_02", "island_tree_02" };
        for (int i = 0; i < ids.Length; i++)
        {
            string lodPath = PrefabRoot + "/Tree/" + ids[i] + ".prefab";
            string heroPath = PrefabRoot + "/Tree/" + ids[i] + "_hero.prefab";
            GameObject heroSource = AssetDatabase.LoadAssetAtPath<GameObject>(heroPath);
            if (heroSource == null || AssetDatabase.LoadAssetAtPath<GameObject>(lodPath) == null)
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(lodPath);
            try
            {
                Transform existing = root.transform.Find("Hero");
                GameObject hero = existing != null ? existing.gameObject : null;
                if (hero == null)
                {
                    hero = (GameObject)PrefabUtility.InstantiatePrefab(heroSource, root.transform);
                    hero.name = "Hero";
                }

                Collider[] colliders = hero.GetComponents<Collider>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[c]);
                }

                hero.SetActive(false);
                UltraHeroSwitch swap = root.GetComponent<UltraHeroSwitch>();
                if (swap == null)
                {
                    swap = root.AddComponent<UltraHeroSwitch>();
                }

                SerializedObject serialized = new SerializedObject(swap);
                serialized.FindProperty("lodGroup").objectReferenceValue = root.GetComponent<LODGroup>();
                serialized.FindProperty("hero").objectReferenceValue = hero;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, lodPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    static void CreateBiomes()
    {
        EnsureFolder("Assets/Game/Levels/Biomes");
        Biome pine = WriteBiome("Assets/Game/Levels/Biomes/PineForest.asset", PineRules());
        Biome nama = WriteBiome("Assets/Game/Levels/Biomes/Namaqualand.asset", NamaRules());
        Assign("Assets/Game/Levels/Island1.asset", pine);
        Assign("Assets/Game/Levels/Island2.asset", nama);
    }

    static void Assign(string path, Biome biome)
    {
        LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
        if (config == null)
        {
            return;
        }

        config.biome = biome;
        EditorUtility.SetDirty(config);
    }

    static Biome WriteBiome(string path, Rule[] rules)
    {
        Biome biome = AssetDatabase.LoadAssetAtPath<Biome>(path);
        if (biome == null)
        {
            biome = ScriptableObject.CreateInstance<Biome>();
            AssetDatabase.CreateAsset(biome, path);
        }

        List<BiomeEntry> entries = new List<BiomeEntry>();
        for (int i = 0; i < rules.Length; i++)
        {
            List<GameObject> prefabs = FindPrefabs(rules[i]);
            if (prefabs.Count == 0)
            {
                Debug.LogWarning("No prefabs for " + rules[i].id);
                continue;
            }

            int each = Mathf.Max(1, Mathf.RoundToInt(rules[i].total / (float)prefabs.Count));
            if (rules[i].category == BiomeCategory.GroundCover || rules[i].category == BiomeCategory.Flower)
            {
                each = Mathf.Clamp(rules[i].total, 1, 8);
            }

            if (rules[i].category == BiomeCategory.SetPiece)
            {
                each = rules[i].total;
            }

            for (int p = 0; p < prefabs.Count; p++)
            {
                int count = each;
                if (rules[i].category != BiomeCategory.GroundCover && rules[i].category != BiomeCategory.Flower && rules[i].category != BiomeCategory.SetPiece && p == prefabs.Count - 1)
                {
                    int used = each * (prefabs.Count - 1);
                    count = Mathf.Max(0, rules[i].total - used);
                }

                if (count <= 0)
                {
                    continue;
                }

                BiomeEntry entry = new BiomeEntry();
                entry.prefab = prefabs[p];
                entry.category = rules[i].category;
                entry.count = count;
                entry.scaleRange = rules[i].scale;
                entry.alignToSlope = rules[i].align;
                entry.limitSlope = rules[i].limitSlope;
                entry.minSlope = rules[i].minSlope;
                entry.maxSlope = rules[i].maxSlope;
                entry.limitHeight = rules[i].limitHeight;
                entry.minHeight = rules[i].minHeight;
                entry.maxHeight = rules[i].maxHeight;
                entries.Add(entry);
            }
        }

        biome.entries = entries.ToArray();
        EditorUtility.SetDirty(biome);
        return biome;
    }

    static List<GameObject> FindPrefabs(Rule rule)
    {
        List<string> folders = new List<string>();
        AddFolder(folders, PrefabRoot + "/" + rule.category);
        AddFolder(folders, "Assets/Game/Prefabs/NaturePack/" + rule.category);
        BiomeCategory extra;
        if (TryExtraCategory(rule.id, out extra))
        {
            AddFolder(folders, PrefabRoot + "/" + extra);
        }

        List<GameObject> found = FindInFolders(rule, folders);
        if (found.Count == 0)
        {
            folders.Clear();
            AddFolder(folders, PrefabRoot);
            string[] categoryNames = System.Enum.GetNames(typeof(BiomeCategory));
            for (int i = 0; i < categoryNames.Length; i++)
            {
                AddFolder(folders, PrefabRoot + "/" + categoryNames[i]);
            }

            found = FindInFolders(rule, folders);
        }

        if ((rule.category == BiomeCategory.GroundCover || rule.category == BiomeCategory.Flower) && found.Count > 3)
        {
            found.RemoveRange(3, found.Count - 3);
        }

        return found;
    }

    static void AddFolder(List<string> folders, string folder)
    {
        if (!folders.Contains(folder))
        {
            folders.Add(folder);
        }
    }

    static List<GameObject> FindInFolders(Rule rule, List<string> folders)
    {
        List<GameObject> found = new List<GameObject>();
        List<string> files = new List<string>();
        for (int r = 0; r < folders.Count; r++)
        {
            if (!Directory.Exists(ToFull(folders[r])))
            {
                continue;
            }

            files.AddRange(Directory.GetFiles(ToFull(folders[r]), "*.prefab"));
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Count; i++)
        {
            string name = Path.GetFileNameWithoutExtension(files[i]);
            if (name.EndsWith("_hero", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!name.StartsWith(rule.id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.Length > rule.id.Length)
            {
                char next = name[rule.id.Length];
                if (next != '_' && next != ' ')
                {
                    continue;
                }
            }

            if (!MatchesAsset(name, rule.id))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToAsset(files[i]));
            if (prefab != null)
            {
                found.Add(prefab);
            }
        }

        if ((rule.category == BiomeCategory.GroundCover || rule.category == BiomeCategory.Flower) && found.Count > 3)
        {
            found.RemoveRange(3, found.Count - 3);
        }

        return found;
    }

    static Rule R(string id, BiomeCategory category, int total, float minScale, float maxScale, bool align, bool slope, float minSlope, float maxSlope, bool height, float minHeight, float maxHeight)
    {
        Rule rule = new Rule();
        rule.id = id;
        rule.category = category;
        rule.total = total;
        rule.scale = new Vector2(minScale, maxScale);
        rule.align = align;
        rule.limitSlope = slope;
        rule.minSlope = minSlope;
        rule.maxSlope = maxSlope;
        rule.limitHeight = height;
        rule.minHeight = minHeight;
        rule.maxHeight = maxHeight;
        return rule;
    }

    static Rule[] PineRules()
    {
        return new[]
        {
            R("jacaranda_tree", BiomeCategory.Tree, 4, 0.92f, 1.08f, false, true, 0f, 0.48f, true, 0.28f, 0.92f),
            R("tree_small_02", BiomeCategory.Tree, 3, 0.92f, 1.08f, false, true, 0f, 0.48f, true, 0.28f, 0.92f),
            R("fir_sapling_medium_b", BiomeCategory.Tree, 3, 0.95f, 1.12f, false, true, 0f, 0.48f, true, 0.28f, 0.92f),
            R("shrub_01", BiomeCategory.Sapling, 10, 1.5f, 1.75f, false, true, 0f, 0.55f, true, 0.26f, 0.9f),
            R("shrub_03", BiomeCategory.Sapling, 10, 1.55f, 1.8f, false, true, 0f, 0.55f, true, 0.26f, 0.9f),
            R("tree_stump_01", BiomeCategory.Deadwood, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("tree_stump_02", BiomeCategory.Deadwood, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("dead_tree_trunk", BiomeCategory.Deadwood, 3, 0.9f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("dead_tree_trunk_02", BiomeCategory.Deadwood, 3, 0.9f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("pine_roots", BiomeCategory.Deadwood, 6, 0.85f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("dry_branches_medium_01", BiomeCategory.Deadwood, 6, 0.85f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("rock_moss_set_02", BiomeCategory.Rock, 10, 0.85f, 1.25f, true, true, 0.12f, 1f, false, 0f, 1f),
            R("fern_02", BiomeCategory.Undergrowth, 22, 0.85f, 1.2f, true, true, 0f, 0.7f, true, 0.26f, 0.95f),
            R("shrub_02", BiomeCategory.Undergrowth, 10, 0.85f, 1.15f, true, true, 0f, 0.6f, true, 0.26f, 0.9f),
            R("shrub_04", BiomeCategory.Undergrowth, 10, 0.85f, 1.15f, true, true, 0f, 0.6f, true, 0.26f, 0.9f),
            R("nettle_plant", BiomeCategory.Undergrowth, 6, 1.15f, 1.45f, true, true, 0f, 0.65f, true, 0.26f, 0.95f),
            R("grass_medium_01", BiomeCategory.GroundCover, 6, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("grass_medium_02", BiomeCategory.GroundCover, 5, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("grass_bermuda_01", BiomeCategory.GroundCover, 4, 0.8f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("moss_01", BiomeCategory.GroundCover, 4, 0.8f, 1.3f, true, true, 0.28f, 1f, false, 0f, 1f),
            R("dandelion_01", BiomeCategory.Flower, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("celandine_01", BiomeCategory.Flower, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("shrub_sorrel_01", BiomeCategory.GroundCover, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("weed_plant_02", BiomeCategory.GroundCover, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("anthurium_botany_01_a", BiomeCategory.Flower, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("anthurium_botany_01_c", BiomeCategory.Flower, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("anthurium_botany_01_e", BiomeCategory.Flower, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("periwinkle_plant_02", BiomeCategory.GroundCover, 3, 0.9f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("boulder_01", BiomeCategory.Rock, 4, 0.85f, 1.2f, true, true, 0.08f, 0.9f, false, 0f, 1f),
            R("rock_07", BiomeCategory.Rock, 3, 2.1f, 2.7f, true, true, 0.08f, 0.9f, false, 0f, 1f),
            R("rock_09", BiomeCategory.Rock, 2, 4.6f, 5.6f, true, true, 0.08f, 0.9f, false, 0f, 1f)
        };
    }

    static Rule[] NamaRules()
    {
        return new[]
        {
            R("quiver_tree_01", BiomeCategory.Tree, 16, 0.9f, 1.12f, false, true, 0f, 0.55f, true, 0.3f, 0.95f),
            R("quiver_tree_02", BiomeCategory.Tree, 12, 0.9f, 1.12f, false, true, 0f, 0.55f, true, 0.3f, 0.95f),
            R("dead_quiver_trunk", BiomeCategory.Deadwood, 8, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("namaqualand_boulder_02", BiomeCategory.Rock, 4, 0.9f, 1.15f, true, true, 0.22f, 1.2f, true, 0.34f, 1f),
            R("namaqualand_boulder_03", BiomeCategory.Rock, 4, 0.9f, 1.15f, true, true, 0.22f, 1.2f, true, 0.34f, 1f),
            R("namaqualand_boulder_04", BiomeCategory.Rock, 3, 0.9f, 1.15f, true, true, 0.22f, 1.2f, true, 0.34f, 1f),
            R("namaqualand_boulder_05", BiomeCategory.Rock, 4, 0.9f, 1.2f, true, true, 0.18f, 1.2f, true, 0.32f, 1f),
            R("namaqualand_boulder_06", BiomeCategory.Rock, 4, 0.9f, 1.2f, true, true, 0.18f, 1.2f, true, 0.32f, 1f),
            R("namaqualand_boulders_01", BiomeCategory.Rock, 6, 0.85f, 1.25f, true, true, 0.16f, 1.2f, true, 0.3f, 1f),
            R("namaqualand_rocks_01", BiomeCategory.Rock, 10, 0.85f, 1.3f, true, true, 0.12f, 1.2f, false, 0f, 1f),
            R("namaqualand_stones_01", BiomeCategory.Rock, 8, 0.8f, 1.35f, true, false, 0f, 1f, false, 0f, 1f),
            R("namaqualand_cliff_01", BiomeCategory.SetPiece, 1, 1f, 1f, false, false, 0f, 1f, false, 0f, 1f),
            R("namaqualand_cliff_02", BiomeCategory.SetPiece, 0, 1f, 1f, false, false, 0f, 1f, false, 0f, 1f),
            R("wild_rooibos_bush", BiomeCategory.Undergrowth, 14, 0.85f, 1.15f, true, true, 0f, 0.65f, true, 0.28f, 0.95f),
            R("didelta_spinosa", BiomeCategory.Undergrowth, 8, 0.85f, 1.1f, true, true, 0f, 0.6f, true, 0.28f, 0.9f),
            R("cheiridopsis_succulent", BiomeCategory.GroundCover, 3, 0.85f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("crystalline_iceplant", BiomeCategory.GroundCover, 3, 0.85f, 1.25f, true, false, 0f, 1f, false, 0f, 1f),
            R("leipoldtia_schultzei", BiomeCategory.GroundCover, 3, 0.85f, 1.25f, true, false, 0f, 1f, false, 0f, 1f),
            R("flower_gazania", BiomeCategory.Flower, 5, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("flower_ursinia", BiomeCategory.Flower, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("flower_empodium", BiomeCategory.Flower, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("flower_heliophila", BiomeCategory.Flower, 4, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("flower_stinkkruid", BiomeCategory.Flower, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("bark_debris_01", BiomeCategory.GroundCover, 2, 0.85f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("dry_quiver_leaf", BiomeCategory.GroundCover, 2, 0.8f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("periwinkle_plant_01", BiomeCategory.GroundCover, 4, 0.9f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("periwinkle_plant_03", BiomeCategory.GroundCover, 4, 0.9f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("periwinkle_plant_05", BiomeCategory.GroundCover, 3, 0.9f, 1.2f, true, false, 0f, 1f, false, 0f, 1f),
            R("weed_plant_02_a", BiomeCategory.GroundCover, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("weed_plant_02_c", BiomeCategory.GroundCover, 3, 0.85f, 1.15f, true, false, 0f, 1f, false, 0f, 1f),
            R("boulder_01", BiomeCategory.Rock, 4, 0.85f, 1.2f, true, true, 0.1f, 1.1f, false, 0f, 1f),
            R("rock_07", BiomeCategory.Rock, 3, 2.1f, 2.7f, true, true, 0.1f, 1.1f, false, 0f, 1f),
            R("rock_09", BiomeCategory.Rock, 3, 4.6f, 5.6f, true, true, 0.1f, 1.1f, false, 0f, 1f)
        };
    }

    static bool MatchesAsset(string name, string id)
    {
        if (!name.StartsWith(id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Length == id.Length)
        {
            return true;
        }

        if (name[id.Length] != '_')
        {
            return false;
        }

        string rest = name.Substring(id.Length + 1);
        int digits = 0;
        while (digits < rest.Length && char.IsDigit(rest[digits]))
        {
            digits++;
        }

        if (digits > 0 && (digits == rest.Length || rest[digits] == '_'))
        {
            return false;
        }

        return true;
    }

    static float LodScreenHeight(int index, int count)
    {
        if (count == 3)
        {
            if (index <= 0)
            {
                return 0.5f;
            }

            if (index == 1)
            {
                return 0.2f;
            }

            return 0.01f;
        }

        return index == count - 1 ? 0.02f : Mathf.Lerp(0.18f, 0.05f, index / (float)Mathf.Max(1, count - 1));
    }

    static bool TryExtraCategory(string id, out BiomeCategory category)
    {
        switch (id)
        {
            case "jacaranda_tree":
            case "tree_small_02":
            case "island_tree_01":
            case "island_tree_02":
            case "island_tree_03":
            case "pine_tree_01":
            case "fir_tree_01":
                category = BiomeCategory.Tree;
                return true;
            case "pine_sapling_small":
            case "pine_sapling_medium":
            case "fir_sapling":
            case "fir_sapling_medium":
                category = BiomeCategory.Sapling;
                return true;
            case "shrub_01":
            case "shrub_03":
            case "nettle_plant":
                category = BiomeCategory.Undergrowth;
                return true;
            case "periwinkle_plant":
            case "weed_plant_02":
                category = BiomeCategory.GroundCover;
                return true;
            case "anthurium_botany_01":
                category = BiomeCategory.Flower;
                return true;
            case "rock_07":
            case "rock_09":
            case "boulder_01":
                category = BiomeCategory.Rock;
                return true;
        }

        category = BiomeCategory.Rock;
        return false;
    }

    static BiomeCategory CategoryFor(string id)
    {
        BiomeCategory extra;
        if (TryExtraCategory(id, out extra))
        {
            return extra;
        }

        Rule[] rules = PineRules();
        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i].id == id)
            {
                return rules[i].category;
            }
        }

        rules = NamaRules();
        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i].id == id)
            {
                return rules[i].category;
            }
        }

        return BiomeCategory.Rock;
    }

    static bool IsFoliage(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.IndexOf("leav") >= 0 || n.IndexOf("twig") >= 0 || n.IndexOf("needle") >= 0 || n.IndexOf("petal") >= 0 || n.IndexOf("frond") >= 0)
        {
            return true;
        }

        bool solid = n.Contains("trunk") || n.Contains("bark") || n.Contains("branches") || n.Contains("rock") || n.Contains("stone");
        if (solid && !n.Contains("leaf") && !n.Contains("needle") && !n.Contains("twig"))
        {
            return false;
        }

        string[] keys = { "twig", "leaf", "needle", "grass", "fern", "moss", "flower", "petal", "frond", "blade", "foliage", "sorrel", "dandelion", "celandine", "gazania", "ursinia", "empodium", "heliophila", "stink", "rooibos", "didelta", "leipoldtia", "iceplant", "cheiridopsis", "shrub", "jacaranda", "nettle", "anthurium", "periwinkle", "weed" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (n.Contains(keys[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsCoverName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("grass") || n.Contains("flower") || n.Contains("moss") || n.Contains("dandelion") || n.Contains("celandine") || n.Contains("sorrel") || n.Contains("gazania") || n.Contains("ursinia") || n.Contains("empodium") || n.Contains("heliophila") || n.Contains("stink") || n.Contains("iceplant") || n.Contains("leipoldtia") || n.Contains("cheiridopsis") || n.Contains("debris") || n.Contains("quiver_leaf") || n.Contains("dry_quiver") || n.Contains("anthurium") || n.Contains("periwinkle") || n.Contains("weed");
    }

    static bool IsCoverRenderer(Material[] materials)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && IsCoverName(materials[i].name))
            {
                return true;
            }
        }

        return false;
    }

    static bool SkipName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("geonodes") || n.Contains("geometry") || n.EndsWith("_geo") || n.Contains("_geo_");
    }

    static string LodKey(string name)
    {
        string clean = Clean(name);
        int lod = clean.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
        if (lod > 0 && lod + 4 < clean.Length)
        {
            bool digits = true;
            for (int i = lod + 4; i < clean.Length; i++)
            {
                if (!char.IsDigit(clean[i]))
                {
                    digits = false;
                    break;
                }
            }

            if (digits)
            {
                return clean.Substring(0, lod);
            }
        }

        return clean;
    }

    static int LodIndex(string name)
    {
        string clean = Clean(name);
        int lod = clean.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
        if (lod < 0)
        {
            return 0;
        }

        int value;
        if (int.TryParse(clean.Substring(lod + 4), out value))
        {
            return value;
        }

        return 0;
    }

    static bool HasMultipleLods(List<MeshFilter> lods)
    {
        if (lods.Count < 2)
        {
            return false;
        }

        int first = LodIndex(lods[0].gameObject.name);
        for (int i = 1; i < lods.Count; i++)
        {
            if (LodIndex(lods[i].gameObject.name) != first)
            {
                return true;
            }
        }

        return false;
    }

    static string UniqueName(string name, HashSet<string> used)
    {
        string candidate = name;
        int suffix = 2;
        while (!used.Add(candidate))
        {
            candidate = name + "_" + suffix;
            suffix++;
        }

        return candidate;
    }

    static string Clean(string name)
    {
        int dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            bool digits = true;
            for (int i = dot + 1; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i]))
                {
                    digits = false;
                    break;
                }
            }

            if (digits)
            {
                return name.Substring(0, dot);
            }
        }

        return name;
    }

    static Material FindMaterial(string name, Dictionary<string, Material> materials)
    {
        Material mat;
        if (materials.TryGetValue(name, out mat))
        {
            return mat;
        }

        foreach (KeyValuePair<string, Material> pair in materials)
        {
            if (name.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase) || pair.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
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
