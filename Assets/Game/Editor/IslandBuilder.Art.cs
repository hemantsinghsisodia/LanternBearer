using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    class ArtKit
    {
        public Material cloak;
        public Material skin;
        public Material lanternMat;
        public Material beaconStone;
        public Material trunk;
        public Material leaves;
        public Material rock;
        public Material grass;
        public Material mushroomStem;
        public Material mushroomCap;
        public Material water;
        public Material moon;
        public Material unlit;
        public Material column;
        public Material silhouette;
        public Material path;
        public Material terrain;
        public Material sky;
        public Texture2D softCircle;
        public Sprite lanternSprite;
        public Sprite arrowSprite;
        public Sprite uiSprite;
        public TerrainLayer sandLayer;
        public TerrainLayer grassLayer;
        public TerrainLayer rockLayer;
        public VolumeProfile volume;
        public GameObject pineS;
        public GameObject pineM;
        public GameObject pineL;
        public GameObject rockA;
        public GameObject rockB;
        public GameObject grassTuft;
        public GameObject mushroom;
        public GameObject mushroomGlow;
        public GameObject beacon;
        public GameObject firefly;
        public GameObject moth;
        public GameObject keeper;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }

    static Mesh coneMesh;

    static GameObject MakeCone(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rotation)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = ConeMesh();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        return go;
    }

    static Mesh ConeMesh()
    {
        if (coneMesh != null)
        {
            return coneMesh;
        }

        const int segments = 12;
        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = new Vector3(0f, 1f, 0f);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, -1f, Mathf.Sin(angle) * 0.5f);
        }

        vertices[segments + 1] = new Vector3(0f, -1f, 0f);
        int[] triangles = new int[segments * 6];
        int cursor = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[cursor++] = 0;
            triangles[cursor++] = i + 1;
            triangles[cursor++] = next + 1;
            triangles[cursor++] = segments + 1;
            triangles[cursor++] = next + 1;
            triangles[cursor++] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "LanternCone";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        coneMesh = mesh;
        return mesh;
    }

    static ArtKit EnsureArt()
    {
        EnsureFolder("Assets/Game/Textures");
        EnsureFolder("Assets/Game/Materials/Generated");
        EnsureFolder("Assets/Game/Levels/Layers");
        EnsureFolder("Assets/Game/Prefabs/Props");
        EnsureFolder("Assets/Game/Prefabs/Gameplay");
        EnsureFolder("Assets/Game/Prefabs/Characters");

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader skyShader = Shader.Find("LanternKeeper/SkyGradient");
        Shader unlitShader = Shader.Find("LanternKeeper/SkyUnlitNoFog");
        Shader addShader = Shader.Find("LanternKeeper/AdditiveUnlit");
        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (lit == null || skyShader == null || unlitShader == null || addShader == null)
        {
            throw new System.InvalidOperationException("A Lantern Keeper shader is missing. Check the console for shader errors.");
        }

        ArtKit art = new ArtKit();
        Texture2D sand = Noise("Assets/Game/Textures/Sand.png", new Color(0.74f, 0.66f, 0.42f), 0.12f, 64, 3);
        Texture2D grassTex = Noise("Assets/Game/Textures/Grass.png", new Color(0.24f, 0.42f, 0.16f), 0.16f, 64, 9);
        Texture2D rockTex = Noise("Assets/Game/Textures/Rock.png", new Color(0.42f, 0.42f, 0.44f), 0.14f, 64, 15);
        Texture2D waterTex = Noise("Assets/Game/Textures/Water.png", new Color(0.02f, 0.08f, 0.11f), 0.08f, 64, 21);
        Texture2D waterNormal = NormalMap("Assets/Game/Textures/WaterNormal.png", 64, 2.2f, 6);
        Texture2D blade = GrassBlade("Assets/Game/Textures/GrassBlade.png");
        art.softCircle = SoftCircle("Assets/Game/Textures/SoftCircle.png");
        art.lanternSprite = SpriteTex("Assets/Game/Textures/LanternIcon.png", LanternIcon(64));
        art.arrowSprite = SpriteTex("Assets/Game/Textures/CompassArrow.png", ArrowIcon(64));
        art.uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        art.cloak = LitMat("Assets/Game/Materials/Generated/Cloak.mat", lit, new Color(0.07f, 0.07f, 0.11f), 0.25f, 0f, Color.black);
        art.skin = LitMat("Assets/Game/Materials/Generated/Skin.mat", lit, new Color(0.55f, 0.4f, 0.32f), 0.35f, 0f, Color.black);
        art.lanternMat = LitMat("Assets/Game/Materials/Generated/LanternBody.mat", lit, new Color(0.28f, 0.12f, 0.04f), 0.55f, 0f, new Color(1.8f, 0.75f, 0.22f));
        art.beaconStone = LitMat("Assets/Game/Materials/Generated/BeaconStone.mat", lit, new Color(0.38f, 0.37f, 0.35f), 0.18f, 0f, Color.black);
        art.trunk = LitMat("Assets/Game/Materials/Generated/Trunk.mat", lit, new Color(0.3f, 0.18f, 0.1f), 0.15f, 0f, Color.black);
        art.leaves = LitMat("Assets/Game/Materials/Generated/Leaves.mat", lit, new Color(0.08f, 0.2f, 0.1f), 0.12f, 0f, Color.black);
        art.rock = LitMat("Assets/Game/Materials/Generated/Rock.mat", lit, new Color(0.34f, 0.35f, 0.37f), 0.2f, 0f, Color.black);
        art.grass = LitMat("Assets/Game/Materials/Generated/GrassTuft.mat", lit, new Color(0.2f, 0.38f, 0.14f), 0.1f, 0f, Color.black);
        art.grass.SetTexture("_BaseMap", blade);
        SetupCutout(art.grass);
        art.mushroomStem = LitMat("Assets/Game/Materials/Generated/MushroomStem.mat", lit, new Color(0.78f, 0.74f, 0.66f), 0.3f, 0f, Color.black);
        art.mushroomCap = LitMat("Assets/Game/Materials/Generated/MushroomCap.mat", lit, new Color(0.15f, 0.55f, 0.62f), 0.45f, 0f, new Color(0.15f, 2.4f, 2.6f));
        art.water = LitMat("Assets/Game/Materials/Generated/Water.mat", lit, new Color(0.015f, 0.07f, 0.1f), 0.94f, 0.04f, Color.black);
        art.water.SetTexture("_BaseMap", waterTex);
        art.water.SetTexture("_BumpMap", waterNormal);
        art.water.SetFloat("_BumpScale", 0.35f);
        art.water.EnableKeyword("_NORMALMAP");
        art.water.SetTextureScale("_BaseMap", new Vector2(12f, 12f));
        art.water.SetTextureScale("_BumpMap", new Vector2(8f, 8f));
        art.moon = UnlitMat("Assets/Game/Materials/Generated/Moon.mat", unlitShader, new Color(2.7f, 2.5f, 2.15f, 1f));
        art.unlit = UnlitMat("Assets/Game/Materials/Generated/SkyUnlit.mat", unlitShader, Color.white);
        art.unlit.SetTexture("_BaseMap", art.softCircle);
        art.column = UnlitMat("Assets/Game/Materials/Generated/LightColumn.mat", addShader, new Color(1f, 0.55f, 0.16f, 0.38f));
        art.silhouette = LitMat("Assets/Game/Materials/Generated/Silhouette.mat", lit, new Color(0.02f, 0.025f, 0.03f), 0.05f, 0f, Color.black);
        art.path = LitMat("Assets/Game/Materials/Generated/PathStone.mat", lit, new Color(0.78f, 0.72f, 0.55f), 0.2f, 0f, Color.black);
        SetupTransparent(art.path);
        art.sky = UnlitMat("Assets/Game/Materials/Generated/NightSky.mat", skyShader, Color.white);
        art.sky.SetColor("_Top", new Color(0.015f, 0.03f, 0.09f, 1f));
        art.sky.SetColor("_Horizon", new Color(0.08f, 0.24f, 0.28f, 1f));
        art.sky.SetColor("_Ground", new Color(0.005f, 0.006f, 0.012f, 1f));
        art.sky.SetFloat("_Exponent", 1.55f);
        if (terrainShader != null)
        {
            art.terrain = LitMat("Assets/Game/Materials/Generated/TerrainLit.mat", terrainShader, Color.white, 0.1f, 0f, Color.black);
        }

        art.sandLayer = Layer("Assets/Game/Levels/Layers/Sand.terrainlayer", sand, new Vector2(7f, 7f));
        art.grassLayer = Layer("Assets/Game/Levels/Layers/Grass.terrainlayer", grassTex, new Vector2(8f, 8f));
        art.rockLayer = Layer("Assets/Game/Levels/Layers/Rock.terrainlayer", rockTex, new Vector2(6f, 6f));
        art.volume = EnsureVolume("Assets/Game/Materials/Generated/NightVolumeProfile.asset");

        art.pineS = SavePrefab(BuildPine(art, 1.05f), "Assets/Game/Prefabs/Props/PineSmall.prefab");
        art.pineM = SavePrefab(BuildPine(art, 1.45f), "Assets/Game/Prefabs/Props/PineMedium.prefab");
        art.pineL = SavePrefab(BuildPine(art, 1.9f), "Assets/Game/Prefabs/Props/PineLarge.prefab");
        art.rockA = SavePrefab(BuildRock(art, false), "Assets/Game/Prefabs/Props/RockCluster.prefab");
        art.rockB = SavePrefab(BuildRock(art, true), "Assets/Game/Prefabs/Props/RockWide.prefab");
        art.grassTuft = SavePrefab(BuildGrass(art), "Assets/Game/Prefabs/Props/GrassTuft.prefab");
        art.mushroom = SavePrefab(BuildMushroom(art, false), "Assets/Game/Prefabs/Props/Mushroom.prefab");
        art.mushroomGlow = SavePrefab(BuildMushroom(art, true), "Assets/Game/Prefabs/Props/MushroomGlow.prefab");
        art.beacon = SavePrefab(BuildBeacon(art), "Assets/Game/Prefabs/Gameplay/Beacon.prefab");
        art.firefly = SavePrefab(BuildFirefly(art), "Assets/Game/Prefabs/Gameplay/Firefly.prefab");
        art.moth = SavePrefab(BuildMoth(art), "Assets/Game/Prefabs/Gameplay/Moth.prefab");
        art.keeper = SavePrefab(BuildKeeper(art), "Assets/Game/Prefabs/Characters/Keeper.prefab");
        AssetDatabase.SaveAssets();
        return art;
    }

    static Material LitMat(string path, Shader shader, Color color, float smoothness, float metallic, Color emission)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0.01f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material UnlitMat(string path, Shader shader, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SetupCutout(Material mat)
    {
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.35f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
    }

    static void SetupTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
    }

    static TerrainLayer Layer(string path, Texture2D tex, Vector2 tile)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = tex;
        layer.tileSize = tile;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static VolumeProfile EnsureVolume(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        Tonemapping tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.ACES);
        Bloom bloom = profile.Add<Bloom>();
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.45f);
        bloom.scatter.Override(0.68f);
        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.42f);
        vignette.color.Override(Color.black);
        ColorAdjustments color = profile.Add<ColorAdjustments>();
        color.saturation.Override(0f);
        color.postExposure.Override(0.05f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static Texture2D Noise(string path, Color baseColor, float variation, int size, int seed)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.13f + seed, y * 0.13f + seed);
                float n2 = Mathf.PerlinNoise(x * 0.04f + seed * 0.2f, y * 0.04f);
                Color color = baseColor * Mathf.Lerp(1f - variation, 1f + variation, n);
                color *= Mathf.Lerp(0.88f, 1.08f, n2);
                color.a = 1f;
                tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();
        return SaveTexture(path, tex, false, false, true);
    }

    static Texture2D NormalMap(string path, int size, float strength, int seed)
    {
        float[,] height = new float[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                height[y, x] = Mathf.PerlinNoise(x * 0.11f + seed, y * 0.11f + seed);
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float left = height[y, (x + size - 1) % size];
                float right = height[y, (x + 1) % size];
                float down = height[(y + size - 1) % size, x];
                float up = height[(y + 1) % size, x];
                Vector3 normal = new Vector3((left - right) * strength, 1f, (down - up) * strength).normalized;
                tex.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
            }
        }

        tex.Apply();
        return SaveTexture(path, tex, true, false, true);
    }

    static Texture2D GrassBlade(string path)
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                float half = Mathf.Lerp(0.22f, 0.03f, v);
                bool on = v > 0.02f && v < 0.98f && Mathf.Abs(u - 0.5f) < half;
                Color color = new Color(0.15f, 0.4f, 0.12f, on ? 1f : 0f);
                tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();
        return SaveTexture(path, tex, false, false, false);
    }

    static Texture2D SoftCircle(string path)
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.15f, 1f, dist));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return SaveTexture(path, tex, false, false, false);
    }

    static Texture2D LanternIcon(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                bool body = u > 0.28f && u < 0.72f && v > 0.2f && v < 0.7f;
                bool neck = u > 0.4f && u < 0.6f && v >= 0.7f && v < 0.8f;
                bool cap = u > 0.32f && u < 0.68f && v >= 0.8f && v < 0.88f;
                bool foot = u > 0.36f && u < 0.64f && v > 0.1f && v <= 0.2f;
                bool on = body || neck || cap || foot;
                tex.SetPixel(x, y, on ? new Color(1f, 0.86f, 0.45f, 1f) : new Color(0f, 0f, 0f, 0f));
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D ArrowIcon(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                bool shaft = v > 0.08f && v < 0.52f && Mathf.Abs(u - 0.5f) < 0.09f;
                bool head = v >= 0.42f && v < 0.92f && Mathf.Abs(u - 0.5f) < (0.92f - v) * 0.72f;
                tex.SetPixel(x, y, (shaft || head) ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }

        tex.Apply();
        return tex;
    }

    static Sprite SpriteTex(string path, Texture2D tex)
    {
        SaveTexture(path, tex, false, true, false);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    static Texture2D SaveTexture(string path, Texture2D tex, bool normal, bool sprite, bool repeat)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = !sprite;
            importer.sRGBTexture = !normal;
            if (normal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = false;
            }
            else if (sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
            }

            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static GameObject SavePrefab(GameObject temp, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static void StripColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Object.DestroyImmediate(colliders[i]);
        }
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        return Prim(type, name, parent, pos, scale, mat, Quaternion.identity);
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rotation)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }

        return go;
    }

    static GameObject BuildPine(ArtKit art, float size)
    {
        GameObject root = new GameObject("Pine");
        root.transform.localScale = Vector3.one * size;
        Prim(PrimitiveType.Capsule, "Trunk", root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.18f, 0.38f, 0.18f), art.trunk);
        MakeCone("LeavesA", root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.15f, 0.72f, 1.15f), art.leaves, Quaternion.identity);
        MakeCone("LeavesB", root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.82f, 0.55f, 0.82f), art.leaves, Quaternion.identity);
        MakeCone("LeavesC", root.transform, new Vector3(0f, 2.05f, 0f), new Vector3(0.5f, 0.4f, 0.5f), art.leaves, Quaternion.identity);
        StripColliders(root);
        return root;
    }

    static GameObject BuildRock(ArtKit art, bool wide)
    {
        GameObject root = new GameObject(wide ? "RockWide" : "RockCluster");
        if (wide)
        {
            Prim(PrimitiveType.Sphere, "A", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(1.1f, 0.38f, 0.8f), art.rock, Quaternion.Euler(8f, 20f, 12f));
            Prim(PrimitiveType.Sphere, "B", root.transform, new Vector3(0.55f, 0.16f, 0.15f), new Vector3(0.55f, 0.28f, 0.5f), art.rock, Quaternion.Euler(0f, 40f, 18f));
        }
        else
        {
            Prim(PrimitiveType.Sphere, "A", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.7f, 0.48f, 0.62f), art.rock, Quaternion.Euler(10f, 15f, 8f));
            Prim(PrimitiveType.Cube, "B", root.transform, new Vector3(-0.32f, 0.16f, 0.12f), new Vector3(0.36f, 0.28f, 0.3f), art.rock, Quaternion.Euler(0f, 25f, 12f));
            Prim(PrimitiveType.Sphere, "C", root.transform, new Vector3(0.34f, 0.18f, -0.1f), new Vector3(0.32f, 0.24f, 0.3f), art.rock);
        }

        StripColliders(root);
        return root;
    }

    static GameObject BuildGrass(ArtKit art)
    {
        GameObject root = new GameObject("GrassTuft");
        Prim(PrimitiveType.Quad, "A", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.56f, 1f), art.grass);
        Prim(PrimitiveType.Quad, "B", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.56f, 1f), art.grass, Quaternion.Euler(0f, 70f, 0f));
        StripColliders(root);
        return root;
    }

    static GameObject BuildMushroom(ArtKit art, bool glow)
    {
        GameObject root = new GameObject(glow ? "MushroomGlow" : "Mushroom");
        Prim(PrimitiveType.Cylinder, "Stem", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.08f, 0.12f, 0.08f), art.mushroomStem);
        Prim(PrimitiveType.Sphere, "Cap", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.26f, 0.12f, 0.26f), art.mushroomCap);
        if (glow)
        {
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.95f, 1f);
            light.range = 2.4f;
            light.intensity = 0.7f;
            light.shadows = LightShadows.None;
        }

        StripColliders(root);
        return root;
    }

    static GameObject BuildBeacon(ArtKit art)
    {
        GameObject root = new GameObject("Beacon");
        Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.125f, 0f), new Vector3(1.15f, 0.125f, 1.15f), art.beaconStone);
        Prim(PrimitiveType.Cylinder, "Step", root.transform, new Vector3(0f, 0.33f, 0f), new Vector3(0.82f, 0.08f, 0.82f), art.beaconStone);
        Prim(PrimitiveType.Cylinder, "Pillar", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.28f, 0.45f, 0.28f), art.beaconStone);
        Prim(PrimitiveType.Sphere, "Brazier", root.transform, new Vector3(0f, 1.46f, 0f), new Vector3(0.5f, 0.2f, 0.5f), art.beaconStone);
        GameObject column = Prim(PrimitiveType.Cylinder, "LightColumn", root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.55f, 0.08f, 0.55f), art.column);
        StripColliders(column);
        column.SetActive(false);

        GameObject flame = new GameObject("Flame");
        flame.transform.SetParent(root.transform, false);
        flame.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        MakeParticles(flame, art.column, 0.7f, 0.8f, 0.18f, new Color(1f, 0.55f, 0.15f, 0.8f), 18f, 0, true, false, 0.12f);

        GameObject embers = new GameObject("Embers");
        embers.transform.SetParent(root.transform, false);
        embers.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        MakeParticles(embers, art.column, 1.1f, 2.2f, 0.12f, new Color(1f, 0.45f, 0.1f, 1f), 0f, 28, false, true, 0.2f);

        GameObject lightObject = new GameObject("BeaconLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.28f);
        light.range = 16f;
        light.intensity = 2.8f;
        light.shadows = LightShadows.None;
        light.enabled = false;

        Beacon beacon = root.AddComponent<Beacon>();
        SerializedObject so = new SerializedObject(beacon);
        so.FindProperty("beaconLight").objectReferenceValue = light;
        so.FindProperty("fire").objectReferenceValue = flame.GetComponent<ParticleSystem>();
        so.FindProperty("embers").objectReferenceValue = embers.GetComponent<ParticleSystem>();
        so.FindProperty("lightColumn").objectReferenceValue = column.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    static GameObject BuildFirefly(ArtKit art)
    {
        GameObject root = new GameObject("Firefly");
        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Prim(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, Vector3.one * 0.16f, art.mushroomCap);
        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(root.transform, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.45f, 1f, 0.4f);
        light.range = 2.8f;
        light.intensity = 1.3f;
        light.shadows = LightShadows.None;
        Material glow = new Material(art.mushroomCap);
        glow.SetColor("_EmissionColor", new Color(0.4f, 2.8f, 0.5f));
        glow.SetColor("_BaseColor", new Color(0.45f, 1f, 0.45f));
        root.transform.Find("Body").GetComponent<Renderer>().sharedMaterial = glow;
        // The instanced material must be an asset or it will leak. Save it once.
        string glowPath = "Assets/Game/Materials/Generated/FireflyGlow.mat";
        Material saved = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(glow, glowPath);
            saved = glow;
        }

        root.transform.Find("Body").GetComponent<Renderer>().sharedMaterial = saved;
        if (saved != glow)
        {
            Object.DestroyImmediate(glow);
        }

        GameObject burst = new GameObject("PickupBurst");
        burst.transform.SetParent(root.transform, false);
        MakeParticles(burst, art.unlit, 0.45f, 1.6f, 0.12f, new Color(0.6f, 1f, 0.45f, 1f), 0f, 16, false, true, 0.05f);
        root.AddComponent<Firefly>();
        StripColliders(root.transform.Find("Body").gameObject);
        return root;
    }

    static GameObject BuildMoth(ArtKit art)
    {
        GameObject root = new GameObject("Moth");
        Prim(PrimitiveType.Capsule, "Body", root.transform, Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f), art.cloak, Quaternion.Euler(90f, 0f, 0f));
        GameObject wings = new GameObject("Wings");
        wings.transform.SetParent(root.transform, false);
        Prim(PrimitiveType.Quad, "Left", wings.transform, new Vector3(-0.16f, 0f, 0f), new Vector3(0.28f, 0.16f, 1f), art.silhouette, Quaternion.Euler(80f, 0f, 20f));
        Prim(PrimitiveType.Quad, "Right", wings.transform, new Vector3(0.16f, 0f, 0f), new Vector3(0.28f, 0.16f, 1f), art.silhouette, Quaternion.Euler(80f, 0f, -20f));
        StripColliders(root);
        AudioSource source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.loop = true;
        source.minDistance = 1.5f;
        source.maxDistance = 18f;
        root.AddComponent<Moth>();
        return root;
    }

    static GameObject BuildKeeper(ArtKit art)
    {
        GameObject root = new GameObject("Keeper");
        MakeCone("Cloak", root.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0.72f, 0.62f, 0.72f), art.cloak, Quaternion.identity);
        Prim(PrimitiveType.Sphere, "Hood", root.transform, new Vector3(0f, 1.42f, -0.02f), new Vector3(0.42f, 0.4f, 0.4f), art.cloak);
        Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.4f, 0.06f), Vector3.one * 0.2f, art.skin);
        Prim(PrimitiveType.Sphere, "Hand", root.transform, new Vector3(0.24f, 0.95f, 0.18f), Vector3.one * 0.09f, art.skin);
        GameObject pivot = new GameObject("LanternPivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0.28f, 0.92f, 0.28f);
        GameObject lantern = Prim(PrimitiveType.Cube, "Lantern", pivot.transform, Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f), art.lanternMat);
        Prim(PrimitiveType.Cube, "Cap", lantern.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.7f, 0.18f, 0.7f), art.cloak);
        GameObject lightObject = new GameObject("LanternLight");
        lightObject.transform.SetParent(lantern.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.64f, 0.28f);
        light.range = 14f;
        light.intensity = 4.2f;
        light.shadows = LightShadows.None;
        GameObject dust = new GameObject("Dust");
        dust.transform.SetParent(root.transform, false);
        dust.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        MakeParticles(dust, art.unlit, 0.55f, 0.35f, 0.35f, new Color(0.75f, 0.75f, 0.7f, 0.22f), 0f, 0, true, true, 0.2f);
        StripColliders(root);
        return root;
    }

    static ParticleSystem MakeParticles(GameObject go, Material mat, float life, float speed, float size, Color color, float rate, int burst, bool loop, bool world, float radius)
    {
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = loop;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 80;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
        if (burst > 0)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
        }

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }
}
}
