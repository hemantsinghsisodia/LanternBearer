using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Imports the Quaternius Stylized Nature MegaKit (CC0) models we use, builds per-island tinted materials
// and per-category prefabs with colliders. Source files live in the git-ignored ArtSource/Nature/MegaKit folder.
// Mesh scale and orientation are baked into shared mesh assets, because island placement overwrites the root scale.
public static class NatureKitImporter
{
    const string SourceRoot = "ArtSource/Nature/MegaKit";
    const string Root = "Assets/Game/Art/Environment/Nature";
    const string ModelRoot = Root + "/Models";
    const string TextureRoot = Root + "/Textures";
    const string MeshRoot = Root + "/Meshes";
    const string MaterialRoot = Root + "/Materials";
    const string PrefabRoot = Root + "/Prefabs";
    const string ProfilePath = "Assets/Game/Art/Look/LookProfile_island";
    const int IslandCount = 4;

    struct Spec
    {
        public string model;
        public BiomeCategory category;
        public string prefix;
        // Target height in metres, or the largest horizontal extent when byExtent is set.
        public float target;
        public bool byExtent;
        public bool layDown;
        // Only change size when the model is outside [min, max] (used for trees).
        public float clampMin;
        public float clampMax;
    }

    static readonly string[] Textures =
    {
        "Bark_DeadTree", "Bark_DeadTree_Normal", "Bark_NormalTree", "Bark_NormalTree_Normal", "Bark_TwistedTree", "Bark_TwistedTree_Normal",
        "Flowers", "Grass", "Leaf_Pine", "Leaves", "Leaves_NormalTree", "Leaves_TwistedTree", "Mushrooms", "PathRocks_Diffuse", "Rocks_Diffuse"
    };

    const string BushLeafKey = "Leaves_TwistedTree_Bush";

    static Spec Tree(string model)
    {
        Spec s = new Spec();
        s.model = model;
        s.category = BiomeCategory.Tree;
        s.prefix = "";
        s.clampMin = 4f;
        s.clampMax = 8f;
        return s;
    }

    static Spec Fixed(string model, BiomeCategory category, string prefix, float height)
    {
        Spec s = new Spec();
        s.model = model;
        s.category = category;
        s.prefix = prefix;
        s.target = height;
        return s;
    }

    static Spec Extent(string model, BiomeCategory category, string prefix, float extent, bool layDown)
    {
        Spec s = Fixed(model, category, prefix, extent);
        s.byExtent = true;
        s.layDown = layDown;
        return s;
    }

    static List<Spec> Specs()
    {
        List<Spec> list = new List<Spec>();
        string[] trees = { "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "Pine_1", "Pine_2", "Pine_3", "Pine_4", "TwistedTree_1", "TwistedTree_2", "TwistedTree_3", "DeadTree_1", "DeadTree_2", "DeadTree_3" };
        for (int i = 0; i < trees.Length; i++)
        {
            list.Add(Tree(trees[i]));
        }

        string[] saplings = { "CommonTree_1", "CommonTree_2", "Pine_1", "Pine_2", "TwistedTree_1" };
        for (int i = 0; i < saplings.Length; i++)
        {
            list.Add(Fixed(saplings[i], BiomeCategory.Sapling, "Sapling_", 2.5f));
        }

        // Deadwood is generated (DeadwoodBuilder): the kit's DeadTree_4/5 read as spidery branch heaps even at 3.5 m.

        for (int i = 1; i <= 3; i++)
        {
            list.Add(Fixed("Rock_Medium_" + i, BiomeCategory.Rock, "", 1.2f));
            list.Add(Extent("Pebble_Round_" + i, BiomeCategory.Rock, "", 1.4f, false));
            list.Add(Extent("Pebble_Square_" + i, BiomeCategory.Rock, "", 1.2f, false));
        }

        list.Add(Fixed("Bush_Common", BiomeCategory.Undergrowth, "", 1.0f));
        list.Add(Extent("Fern_1", BiomeCategory.Undergrowth, "", 1.8f, false));
        list.Add(Fixed("Plant_1", BiomeCategory.Undergrowth, "", 1.0f));
        list.Add(Fixed("Plant_1_Big", BiomeCategory.Undergrowth, "", 1.3f));
        list.Add(Extent("Plant_7", BiomeCategory.Undergrowth, "", 1.0f, false));
        list.Add(Extent("Plant_7_Big", BiomeCategory.Undergrowth, "", 1.3f, false));

        list.Add(Fixed("Bush_Common_Flowers", BiomeCategory.Flower, "", 1.0f));
        list.Add(Fixed("Flower_3_Single", BiomeCategory.Flower, "", 0.35f));
        list.Add(Fixed("Flower_3_Group", BiomeCategory.Flower, "", 0.4f));
        list.Add(Fixed("Flower_4_Single", BiomeCategory.Flower, "", 0.35f));
        list.Add(Fixed("Flower_4_Group", BiomeCategory.Flower, "", 0.4f));

        list.Add(Fixed("Clover_1", BiomeCategory.GroundCover, "", 0.25f));
        list.Add(Fixed("Clover_2", BiomeCategory.GroundCover, "", 0.25f));
        list.Add(Fixed("Grass_Common_Short", BiomeCategory.GroundCover, "", 0.4f));
        list.Add(Fixed("Grass_Common_Tall", BiomeCategory.GroundCover, "", 0.7f));
        list.Add(Fixed("Grass_Wispy_Short", BiomeCategory.GroundCover, "", 0.4f));
        list.Add(Fixed("Grass_Wispy_Tall", BiomeCategory.GroundCover, "", 0.7f));
        list.Add(Fixed("Mushroom_Common", BiomeCategory.GroundCover, "", 0.2f));
        list.Add(Fixed("Mushroom_Laetiporus", BiomeCategory.GroundCover, "", 0.3f));
        list.Add(Extent("Petal_1", BiomeCategory.GroundCover, "", 0.25f, false));
        list.Add(Extent("Petal_2", BiomeCategory.GroundCover, "", 0.3f, false));
        list.Add(Extent("Petal_3", BiomeCategory.GroundCover, "", 0.3f, false));
        return list;
    }

    [MenuItem("Lantern Keeper/Nature/Import Kit")]
    public static void ImportKit()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before importing the nature kit.");
            return;
        }

        List<Spec> specs = Specs();
        CopySources(specs);
        AssetDatabase.Refresh();
        PrepareTextures();
        PrepareModels(specs);
        EnsureFolder(MaterialRoot);
        EnsureFolder(MeshRoot);

        Shader foliage = Shader.Find("LanternKeeper/Foliage");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (foliage == null || lit == null)
        {
            Debug.LogError("NatureKitImporter: missing shader. Foliage=" + (foliage != null) + " Lit=" + (lit != null));
            return;
        }

        StringBuilder log = new StringBuilder();
        int prefabCount = 0;
        for (int island = 1; island <= IslandCount; island++)
        {
            LookProfile profile = AssetDatabase.LoadAssetAtPath<LookProfile>(ProfilePath + island + ".asset");
            if (profile == null)
            {
                Debug.LogError("NatureKitImporter: missing look profile for island" + island);
                return;
            }

            string id = string.IsNullOrEmpty(profile.levelId) ? "island" + island : profile.levelId;
            Dictionary<string, Material> materials = BuildMaterials(id, profile, foliage, lit);
            for (int i = 0; i < specs.Count; i++)
            {
                string line = BuildPrefab(specs[i], id, materials);
                if (line != null)
                {
                    prefabCount++;
                    if (island == 1)
                    {
                        log.AppendLine(line);
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        string[] ids = new string[IslandCount];
        for (int island = 1; island <= IslandCount; island++)
        {
            LookProfile profile = AssetDatabase.LoadAssetAtPath<LookProfile>(ProfilePath + island + ".asset");
            ids[island - 1] = string.IsNullOrEmpty(profile.levelId) ? "island" + island : profile.levelId;
        }

        prefabCount += DeadwoodBuilder.BuildAll(ids);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Nature kit import done: " + prefabCount + " prefabs (" + specs.Count + " kit models + " + DeadwoodBuilder.Models.Length + " generated deadwood per island).\n" + log);
    }

    // Poly Haven prefabs deliberately kept after a biome rebuild (prefab name -> reason).
    public static readonly Dictionary<string, string> AllowList = new Dictionary<string, string>
    {
        // Island 2 set piece: no kit equivalent. Task 5 replaces it with rock cladding; drop this entry then.
        { "namaqualand_cliff_01", "SetPiece cliff, replaced by cladding in Task 5" }
    };

    [MenuItem("Lantern Keeper/Nature/Rebuild Island 1 Biome")]
    public static void RebuildIsland1()
    {
        RebuildBiome("PineForest", "island1");
    }

    [MenuItem("Lantern Keeper/Nature/Rebuild Islands 2-4 Biomes")]
    public static void RebuildIslands2To4()
    {
        RebuildBiome("Namaqualand", "island2");
        RebuildBiome("Marsh", "island3");
        RebuildBiome("Heath", "island4");
    }

    [MenuItem("Lantern Keeper/Nature/Rebuild All Biomes")]
    public static void RebuildAll()
    {
        RebuildIsland1();
        RebuildIslands2To4();
    }

    // Per-biome model choices. Entries are matched by category and order (and by the old prefab name for ground cover and undergrowth).
    class Table
    {
        public string[] trees;
        public string[] saplings;
        public string[] bushes;
        public string[] deadwood;
        public string[] flowers;
        public string[] grasses = { "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall" };
        public string[] mushrooms = { "Mushroom_Common", "Mushroom_Laetiporus" };
        public string[] lowPlants = { "Clover_1", "Clover_2" };
        public string[] litter = { "Petal_1", "Petal_2", "Petal_3" };
    }

    static Table TableFor(string biomeName)
    {
        Table t = new Table();
        t.deadwood = DeadwoodModels;
        switch (biomeName)
        {
            case "Namaqualand":
                t.trees = new[] { "TwistedTree_1", "DeadTree_2" };
                t.bushes = new[] { "Bush_Common", "Plant_1_Big", "Plant_7_Big", "Plant_1", "Plant_7" };
                t.flowers = new[] { "Flower_3_Single", "Flower_4_Group", "Flower_3_Group", "Flower_4_Single" };
                break;
            case "Marsh":
                t.trees = new[] { "TwistedTree_3", "TwistedTree_2" };
                t.bushes = new[] { "Bush_Common", "Plant_1_Big", "Plant_7_Big", "Plant_1", "Plant_7" };
                t.flowers = new[] { "Flower_3_Group", "Flower_4_Group" };
                break;
            case "Heath":
                t.trees = new[] { "DeadTree_3", "TwistedTree_1" };
                t.saplings = new[] { "Sapling_TwistedTree_1" };
                t.bushes = new[] { "Bush_Common", "Plant_1_Big", "Plant_7_Big" };
                t.flowers = new[] { "Flower_3_Single" };
                break;
            default:
                t.trees = new[] { "", "Pine_1", "Pine_3" };
                t.saplings = new[] { "Sapling_Pine_1", "Sapling_CommonTree_1", "Sapling_Pine_2", "Sapling_CommonTree_2", "Sapling_TwistedTree_1" };
                t.bushes = new[] { "Bush_Common", "Plant_1_Big", "Plant_7_Big", "Plant_1", "Plant_7" };
                t.flowers = new[] { "Flower_3_Single", "Flower_4_Group", "Flower_3_Group", "Flower_4_Single" };
                break;
        }

        return t;
    }

    // Deadwood prefabs: generated logs and stumps of about 3.5 m and under (DeadwoodBuilder). A deadwood entry already on a prefab with this tag has its halved count.
    static readonly string[] DeadwoodModels = DeadwoodBuilder.Models;
    const string DeadwoodTag = "Nature_Wood_";

    // True once any entry of the biome uses a nature-kit prefab.
    public static bool IsConverted(Biome biome)
    {
        if (biome == null || biome.entries == null)
        {
            return false;
        }

        for (int i = 0; i < biome.entries.Length; i++)
        {
            BiomeEntry entry = biome.entries[i];
            if (entry != null && entry.prefab != null && entry.prefab.name.StartsWith("Nature_"))
            {
                return true;
            }
        }

        return false;
    }

    static float MaxExtent(GameObject prefab)
    {
        float extent = 0f;
        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
            {
                continue;
            }

            Vector3 size = Vector3.Scale(filters[i].sharedMesh.bounds.size, filters[i].transform.lossyScale);
            extent = Mathf.Max(extent, size.x, size.z);
        }

        return extent;
    }

    // Swaps every entry's prefab for a same-category nature-kit prefab of this island. Counts, categories and
    // placement limits stay; scale ranges are brought near 1 because the kit meshes already carry real-world size.
    // Idempotent: entries that already use a Nature_ prefab are left alone, except deadwood, which is re-targeted.
    // User-directed exception to "entry counts kept": each Deadwood entry count is halved (rounded up), once.
    public static void RebuildBiome(string biomeName, string levelId)
    {
        string path = "Assets/Game/Levels/Biomes/" + biomeName + ".asset";
        Biome biome = AssetDatabase.LoadAssetAtPath<Biome>(path);
        if (biome == null)
        {
            Debug.LogError("NatureKitImporter: missing biome " + path);
            return;
        }

        Table table = TableFor(biomeName);
        Dictionary<BiomeCategory, int> counters = new Dictionary<BiomeCategory, int>();
        int saplingIndex = 0;
        int bushIndex = 0;
        int grassIndex = 0;
        int mushroomIndex = 0;
        int flowerIndex = 0;
        int rockIndex = 0;
        int lowIndex = 0;
        int litterIndex = 0;
        StringBuilder log = new StringBuilder();
        BiomeEntry[] entries = biome.entries;
        for (int i = 0; i < entries.Length; i++)
        {
            BiomeEntry entry = entries[i];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            string oldName = entry.prefab.name;
            if (AllowList.ContainsKey(oldName))
            {
                continue;
            }

            int n;
            counters.TryGetValue(entry.category, out n);
            counters[entry.category] = n + 1;
            bool converted = oldName.StartsWith("Nature_");
            if (converted && entry.category != BiomeCategory.Deadwood)
            {
                continue;
            }

            string lower = oldName.ToLowerInvariant();
            string model = null;
            Vector2 scale = entry.scaleRange;
            switch (entry.category)
            {
                case BiomeCategory.Tree:
                    model = table.trees[n % table.trees.Length];
                    if (model == "")
                    {
                        model = LargestTree(levelId);
                    }

                    break;
                case BiomeCategory.Sapling:
                    model = table.saplings[saplingIndex++ % table.saplings.Length];
                    break;
                case BiomeCategory.Deadwood:
                    model = table.deadwood[n % table.deadwood.Length];
                    break;
                case BiomeCategory.Rock:
                {
                    // Keep the rock's real size: old mesh extent times old scale, in units of the 1.2 m Rock_Medium.
                    float extent = MaxExtent(entry.prefab);
                    float low = extent * scale.x / 1.2f;
                    float high = extent * scale.y / 1.2f;
                    if (extent < 0.5f)
                    {
                        model = (rockIndex % 2 == 0 ? "Pebble_Round_" : "Pebble_Square_") + (rockIndex / 2 % 3 + 1);
                        scale = new Vector2(0.4f, 0.6f);
                    }
                    else
                    {
                        model = "Rock_Medium_" + (rockIndex % 3 + 1);
                        scale = new Vector2(Mathf.Clamp(low, 0.7f, 3.3f), Mathf.Clamp(high, 0.8f, 3.4f));
                    }

                    rockIndex++;
                    break;
                }
                case BiomeCategory.Undergrowth:
                    model = lower.Contains("fern") ? "Fern_1" : table.bushes[bushIndex++ % table.bushes.Length];
                    break;
                case BiomeCategory.Flower:
                    model = table.flowers[flowerIndex++ % table.flowers.Length];
                    break;
                case BiomeCategory.GroundCover:
                    if (lower.Contains("moss"))
                    {
                        model = table.mushrooms[mushroomIndex++ % table.mushrooms.Length];
                    }
                    else if (lower.Contains("periwinkle") || lower.Contains("succulent") || lower.Contains("iceplant") || lower.Contains("leipoldtia"))
                    {
                        model = table.lowPlants[lowIndex++ % table.lowPlants.Length];
                    }
                    else if (lower.Contains("bark_debris") || lower.Contains("dry_quiver_leaf"))
                    {
                        model = table.litter[litterIndex++ % table.litter.Length];
                    }
                    else
                    {
                        model = table.grasses[grassIndex++ % table.grasses.Length];
                    }

                    break;
            }

            if (model == null)
            {
                continue;
            }

            string prefabPath = PrefabRoot + "/" + entry.category + "/Nature_" + model + "_" + levelId + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("NatureKitImporter: no prefab " + prefabPath + " for " + oldName);
                continue;
            }

            if (entry.category == BiomeCategory.Deadwood)
            {
                // Halve once: an entry already on a deadwood prefab was halved before.
                if (!converted || !oldName.Contains(DeadwoodTag))
                {
                    entry.count = (entry.count + 1) / 2;
                }

                scale = new Vector2(0.85f, 1.15f);
            }
            else if (entry.category != BiomeCategory.Rock && scale.x > 1.1f)
            {
                scale = new Vector2(0.9f, 1.15f);
            }

            entry.prefab = prefab;
            entry.scaleRange = scale;
            log.AppendLine(entry.category + ": " + oldName + " -> " + prefab.name + " x" + entry.count + " scale " + scale.x.ToString("0.00") + "-" + scale.y.ToString("0.00"));
        }

        EditorUtility.SetDirty(biome);
        AssetDatabase.SaveAssets();
        Debug.Log("NatureKitImporter: rebuilt " + biomeName + "\n" + log);
    }

    static string LargestTree(string levelId)
    {
        string[] candidates = { "Pine_1", "Pine_2", "Pine_3", "Pine_4", "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "TwistedTree_1", "TwistedTree_2", "TwistedTree_3" };
        string best = "Pine_2";
        float height = 0f;
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/Tree/Nature_" + candidates[i] + "_" + levelId + ".prefab");
            MeshFilter filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.bounds.size.y > height)
            {
                height = filter.sharedMesh.bounds.size.y;
                best = candidates[i];
            }
        }

        return best;
    }

    static void CopySources(List<Spec> specs)
    {
        EnsureFolder(ModelRoot);
        EnsureFolder(TextureRoot);
        HashSet<string> models = new HashSet<string>();
        for (int i = 0; i < specs.Count; i++)
        {
            models.Add(specs[i].model);
        }

        foreach (string model in models)
        {
            CopyIfMissing(SourceRoot + "/FBX (Unity)/" + model + ".fbx", ModelRoot + "/" + model + ".fbx");
        }

        for (int i = 0; i < Textures.Length; i++)
        {
            CopyIfMissing(SourceRoot + "/Textures/" + Textures[i] + ".png", TextureRoot + "/" + Textures[i] + ".png");
        }
    }

    static void CopyIfMissing(string source, string destination)
    {
        string from = ToFull(source);
        string to = ToFull(destination);
        if (File.Exists(to))
        {
            return;
        }

        if (!File.Exists(from))
        {
            Debug.LogError("NatureKitImporter: source missing " + source);
            return;
        }

        File.Copy(from, to);
    }

    static void PrepareTextures()
    {
        for (int i = 0; i < Textures.Length; i++)
        {
            string path = TextureRoot + "/" + Textures[i] + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool normal = Textures[i].EndsWith("_Normal");
            bool leaf = Textures[i].StartsWith("Leaf") || Textures[i] == "Flowers" || Textures[i] == "Grass";
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.alphaIsTransparency = leaf;
            importer.mipMapsPreserveCoverage = leaf;
            importer.alphaTestReferenceValue = 0.5f;
            importer.SaveAndReimport();
        }

        // Greyscale copies of the bark and rock colour textures, so the island bark / rock tint sets the hue instead of multiplying into the pack's own.
        string[] greys = { "Bark_NormalTree", "Bark_DeadTree", "Bark_TwistedTree", "Rocks_Diffuse", "PathRocks_Diffuse" };
        for (int i = 0; i < greys.Length; i++)
        {
            MakeGrey(greys[i]);
        }
    }

    static string GreyPath(string name)
    {
        return TextureRoot + "/" + name + "_Grey.png";
    }

    static void MakeGrey(string name)
    {
        string source = TextureRoot + "/" + name + ".png";
        string target = GreyPath(name);
        if (!File.Exists(ToFull(target)))
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!texture.LoadImage(File.ReadAllBytes(ToFull(source))))
            {
                Object.DestroyImmediate(texture);
                return;
            }

            Color32[] pixels = texture.GetPixels32();
            float sum = 0f;
            float[] luma = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                luma[i] = (0.2126f * pixels[i].r + 0.7152f * pixels[i].g + 0.0722f * pixels[i].b) / 255f;
                sum += luma[i];
            }

            // Mean maps to 0.8, so a mid tint colour keeps its own brightness.
            float mean = Mathf.Max(0.01f, sum / pixels.Length);
            float gain = 0.8f / mean;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(luma[i] * gain * 255f), 0, 255);
                pixels[i] = new Color32(g, g, g, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(ToFull(target), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(target);
        }

        TextureImporter importer = AssetImporter.GetAtPath(target) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }

    static void PrepareModels(List<Spec> specs)
    {
        HashSet<string> done = new HashSet<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < specs.Count; i++)
            {
                if (!done.Add(specs[i].model))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(ModelRoot + "/" + specs[i].model + ".fbx") as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.useFileScale = true;
                importer.isReadable = true;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
    }

    // Material kinds, keyed by the FBX material name.
    static Dictionary<string, Material> BuildMaterials(string id, LookProfile profile, Shader foliage, Shader lit)
    {
        Dictionary<string, Material> map = new Dictionary<string, Material>();
        Color leaf = Opaque(profile.foliage);
        Color treeLeaf = Opaque(LookMapping.OrDerived(profile.treeFoliage, profile.foliage));
        Color pine = Scale(treeLeaf, 0.82f);
        Color twisted = Scale(treeLeaf, 0.95f);
        Color plant = Scale(leaf, 1.08f);
        Color grass = Color.Lerp(Scale(leaf, 1.12f), Color.white, 0.12f);

        map["Leaves_NormalTree"] = Foliage(id, "LeavesNormal", foliage, "Leaves_NormalTree", treeLeaf, 0f, 4f, 0.12f);
        map["Leaves_Pine"] = Foliage(id, "LeavesPine", foliage, "Leaf_Pine", pine, 0f, 4f, 0.09f);
        map["Leaves_TwistedTree"] = Foliage(id, "LeavesTwisted", foliage, "Leaves_TwistedTree", twisted, 0f, 4f, 0.1f);
        // Bush_Common models use the TwistedTree leaf texture, but a bush takes the island foliage tint, not the tree tint.
        map[BushLeafKey] = Foliage(id, "LeavesBush", foliage, "Leaves_TwistedTree", Scale(leaf, 0.95f), 0f, 4f, 0.1f);
        map["Leaves"] = Foliage(id, "LeavesPlant", foliage, "Leaves", plant, 1f, 1f, 0.05f);
        map["Grass"] = Foliage(id, "Grass", foliage, "Grass", grass, 0f, 0.6f, 0.04f);
        map["Flowers"] = Foliage(id, "Flowers", foliage, "Flowers", Color.white, 0f, 0.8f, 0.04f);

        Color bark = Opaque(profile.bark);
        map["Bark_NormalTree"] = Lit(id, "BarkNormal", lit, GreyPath("Bark_NormalTree"), TextureRoot + "/Bark_NormalTree_Normal.png", bark, 0.1f);
        map["Bark_DeadTree"] = Lit(id, "BarkDead", lit, GreyPath("Bark_DeadTree"), TextureRoot + "/Bark_DeadTree_Normal.png", Scale(bark, 0.92f), 0.08f);
        map["Bark_TwistedTree"] = Lit(id, "BarkTwisted", lit, GreyPath("Bark_TwistedTree"), TextureRoot + "/Bark_TwistedTree_Normal.png", bark, 0.1f);

        Color rock = Opaque(profile.rockTint);
        map["Rocks"] = Lit(id, "Rock", lit, GreyPath("Rocks_Diffuse"), null, rock, 0.12f);
        map["PathRocks"] = Lit(id, "Pebble", lit, GreyPath("PathRocks_Diffuse"), null, rock, 0.12f);
        map["Mushrooms"] = Lit(id, "Mushroom", lit, TextureRoot + "/Mushrooms.png", null, Color.white, 0.2f);
        return map;
    }

    static Material Foliage(string id, string kind, Shader shader, string texture, Color tint, float desaturate, float swayHeight, float swayAmount)
    {
        Material mat = LoadOrCreate(MaterialRoot + "/Nature_" + kind + "_" + id + ".mat", shader);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/" + texture + ".png"));
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Desaturate", desaturate);
        mat.SetFloat("_SwayHeight", swayHeight);
        mat.SetFloat("_SwayAmount", swayAmount);
        mat.SetFloat("_Wrap", 0.5f);
        mat.enableInstancing = true;
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material Lit(string id, string kind, Shader shader, string albedo, string normal, Color tint, float smoothness)
    {
        Material mat = LoadOrCreate(MaterialRoot + "/Nature_" + kind + "_" + id + ".mat", shader);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreate(string path, Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        return mat;
    }

    static string PrefabName(Spec spec, string id)
    {
        return "Nature_" + spec.prefix + spec.model + "_" + id;
    }

    static string BuildPrefab(Spec spec, string id, Dictionary<string, Material> materials)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot + "/" + spec.model + ".fbx");
        MeshFilter sourceFilter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
        MeshRenderer sourceRenderer = model != null ? model.GetComponentInChildren<MeshRenderer>() : null;
        if (sourceFilter == null || sourceRenderer == null || sourceFilter.sharedMesh == null)
        {
            Debug.LogWarning("NatureKitImporter: no mesh in " + spec.model);
            return null;
        }

        Mesh mesh = BakedMesh(spec, sourceFilter.sharedMesh);
        Material[] source = sourceRenderer.sharedMaterials;
        Material[] assigned = new Material[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            string key = source[i] != null ? source[i].name : "";
            if (key == "Leaves_TwistedTree" && spec.model.StartsWith("Bush_"))
            {
                key = BushLeafKey;
            }

            if (!materials.TryGetValue(key, out assigned[i]))
            {
                Debug.LogWarning("NatureKitImporter: no material mapping for '" + key + "' on " + spec.model);
            }
        }

        string folder = PrefabRoot + "/" + spec.category;
        EnsureFolder(folder);
        string name = PrefabName(spec, id);
        GameObject root = new GameObject(name);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = assigned;
        PolyHavenImporter.AddCollider(root, spec.category);
        string path = folder + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Bounds b = mesh.bounds;
        return spec.category + "/" + name + " height=" + b.size.y.ToString("0.00") + " extent=" + Mathf.Max(b.size.x, b.size.z).ToString("0.00");
    }

    // The scaled (and, for deadwood, laid-down) copy of the source mesh. Shared by every island's prefab.
    static Mesh BakedMesh(Spec spec, Mesh source)
    {
        string path = MeshRoot + "/" + spec.prefix + spec.model + ".asset";
        Mesh baked = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        Mesh mesh = Object.Instantiate(source);
        mesh.name = spec.prefix + spec.model;
        Quaternion rotation = spec.layDown ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        Bounds original = source.bounds;
        // A laid-down trunk is sized by its length, which is the source height.
        float measure = spec.byExtent && !spec.layDown ? Mathf.Max(original.size.x, original.size.z) : original.size.y;
        float scale = 1f;
        if (spec.clampMax > 0f)
        {
            scale = Mathf.Clamp(measure, spec.clampMin, spec.clampMax) / measure;
        }
        else if (spec.target > 0f)
        {
            scale = spec.target / measure;
        }

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        float minY = float.MaxValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = rotation * (vertices[i] * scale);
            minY = Mathf.Min(minY, vertices[i].y);
        }

        // A laid-down trunk rests half sunk. Everything else keeps the source origin, which already sits at the base.
        float lift = spec.layDown ? -0.2f - minY : 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y += lift;
        }

        mesh.vertices = vertices;
        if (normals != null && normals.Length == vertices.Length)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = rotation * normals[i];
            }

            mesh.normals = normals;
        }

        if (tangents != null && tangents.Length == vertices.Length)
        {
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }

            mesh.tangents = tangents;
        }

        mesh.RecalculateBounds();
        if (baked != null)
        {
            EditorUtility.CopySerialized(mesh, baked);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(baked);
            SetReadable(baked, spec);
            return baked;
        }

        AssetDatabase.CreateAsset(mesh, path);
        SetReadable(mesh, spec);
        return mesh;
    }

    // Only meshes that feed a MeshCollider stay CPU-readable.
    static void SetReadable(Mesh mesh, Spec spec)
    {
        bool needsCollider = spec.category == BiomeCategory.Rock || spec.category == BiomeCategory.Deadwood || spec.category == BiomeCategory.SetPiece;
        SerializedObject serialized = new SerializedObject(mesh);
        SerializedProperty readable = serialized.FindProperty("m_IsReadable");
        if (readable != null && readable.boolValue != needsCollider)
        {
            readable.boolValue = needsCollider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
        }
    }

    static Color Opaque(Color c)
    {
        return new Color(c.r, c.g, c.b, 1f);
    }

    static Color Scale(Color c, float k)
    {
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
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

    static string ToFull(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }
}
}
