using System.Collections.Generic;
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
        public Material wood;
        public Material iron;
        public Material charred;
        public Material smoke;
        public Material flameA;
        public Material flameB;
        public Material flameC;
        public Material heat;
        public Material silhouette;
        public Material path;
        public Material terrain;
        public Material sky;
        public Material foam;
        public Material beam;
        public Material lamp;
        public bool hdriSky;
        public Cubemap nightCube;
        public Cubemap dawnCube;
        public Vector3 moonDirection;
        public Vector3 dawnDirection;
        public Texture2D softCircle;
        public Sprite lanternSprite;
        public Sprite arrowSprite;
        public Sprite uiSprite;
        public TerrainLayer sandLayer;
        public TerrainLayer grassLayer;
        public TerrainLayer dirtLayer;
        public TerrainLayer rockLayer;
        public TerrainLayer mossLayer;
        public Texture2D detailGrass;
        public Texture2D detailReed;
        public VolumeProfile volume;
        public GameObject pineS;
        public GameObject pineM;
        public GameObject pineL;
        public GameObject rockA;
        public GameObject rockB;
        public GameObject grassTuft;
        public GameObject pebble;
        public GameObject log;
        public GameObject stump;
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
        Vector2[] uv = new Vector2[segments + 2];
        uv[0] = new Vector2(0.5f, 1f);
        uv[segments + 1] = new Vector2(0.5f, 0f);
        for (int i = 0; i < segments; i++)
        {
            uv[i + 1] = new Vector2(i / (float)segments, 0.15f);
        }

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
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        coneMesh = mesh;
        return mesh;
    }

    static ArtKit EnsureArt()
    {
        EnsureFolder("Assets/Game/Textures");
        EnsureFolder("Assets/Game/Textures/Generated");
        EnsureFolder("Assets/Game/Textures/Sky");
        EnsureFolder("Assets/Game/Textures/PolyHaven");
        EnsureFolder("Assets/Game/Models");
        EnsureFolder("Assets/Game/Models/Keeper");
        EnsureFolder("Assets/Game/Materials/Generated");
        EnsureFolder("Assets/Game/Levels/Layers");
        EnsureFolder("Assets/Game/Prefabs/Props");
        EnsureFolder("Assets/Game/Prefabs/Gameplay");
        EnsureFolder("Assets/Game/Prefabs/Characters");

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader skyShader = Shader.Find("LanternKeeper/SkyGradient");
        Shader unlitShader = Shader.Find("LanternKeeper/SkyUnlitNoFog");
        Shader addShader = Shader.Find("LanternKeeper/AdditiveUnlit");
        Shader flameShader = Shader.Find("LanternKeeper/Flame");
        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        Shader waterShader = Shader.Find("LanternKeeper/Water");
        Shader silhouetteShader = Shader.Find("LanternKeeper/Silhouette");
        Shader hazeShader = Shader.Find("LanternKeeper/HorizonHaze");
        Shader skyBlend = Shader.Find("LanternKeeper/SkyBlend");
        Shader pathReveal = Shader.Find("LanternKeeper/PathReveal");
        if (lit == null || skyShader == null || unlitShader == null || addShader == null || flameShader == null || waterShader == null || silhouetteShader == null || hazeShader == null || skyBlend == null || pathReveal == null)
        {
            throw new System.InvalidOperationException("A Lantern Keeper shader is missing. Check the console for shader errors.");
        }

        ArtKit art = new ArtKit();
        Texture2D sand = PolyOrNoise("coast_sand_01_Diffuse.jpg", new Color(0.74f, 0.66f, 0.42f), 3);
        Texture2D sandNormal = PolyOrNormal("coast_sand_01_nor_gl.jpg", 4);
        Texture2D sandMask = SmoothnessMask("coast_sand_01_Rough.jpg", "Assets/Game/Textures/PolyHaven/coast_sand_01_mask.png", 5);
        Texture2D grassTex = PolyOrNoise("forrest_ground_01_Diffuse.jpg", new Color(0.24f, 0.42f, 0.16f), 9);
        Texture2D grassNormal = PolyOrNormal("forrest_ground_01_nor_gl.jpg", 10);
        Texture2D grassMask = SmoothnessMask("forrest_ground_01_Rough.jpg", "Assets/Game/Textures/PolyHaven/forrest_ground_01_mask.png", 11);
        Texture2D dirtTex = PolyOrNoise("stony_dirt_path_Diffuse.jpg", new Color(0.36f, 0.26f, 0.16f), 13);
        Texture2D dirtNormal = PolyOrNormal("stony_dirt_path_nor_gl.jpg", 14);
        Texture2D dirtMask = SmoothnessMask("stony_dirt_path_Rough.jpg", "Assets/Game/Textures/PolyHaven/stony_dirt_path_mask.png", 15);
        Texture2D rockTex = PolyOrNoise("rock_face_03_Diffuse.jpg", new Color(0.42f, 0.42f, 0.44f), 17);
        Texture2D rockNormal = PolyOrNormal("rock_face_03_nor_gl.jpg", 18);
        Texture2D rockMask = SmoothnessMask("rock_face_03_Rough.jpg", "Assets/Game/Textures/PolyHaven/rock_face_03_mask.png", 19);
        Texture2D mossTex = PolyOrNoise("forest_leaves_02_Diffuse.jpg", new Color(0.22f, 0.32f, 0.16f), 21);
        Texture2D mossNormal = PolyOrNormal("forest_leaves_02_nor_gl.jpg", 22);
        Texture2D mossMask = SmoothnessMask("forest_leaves_02_Rough.jpg", "Assets/Game/Textures/PolyHaven/forest_leaves_02_mask.png", 23);
        Texture2D waterNormalA;
        Texture2D waterNormalB;
        Texture2D foamNoise;
        LoadWaterMaps(out waterNormalA, out waterNormalB, out foamNoise);
        Texture2D blade = GrassBlade("Assets/Game/Textures/GrassBlade.png");
        art.detailGrass = blade;
        art.detailReed = ReedBlade("Assets/Game/Textures/ReedBlade.png");
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
        art.water = ShaderMat("Assets/Game/Materials/Generated/Water.mat", waterShader);
        ApplyWaterLook(art.water, waterNormalA, waterNormalB, foamNoise);
        art.water.SetColor("_FoamColor", new Color(0.78f, 0.86f, 0.84f, 0.8f));
        art.water.SetFloat("_NormalScale", 0.28f);
        art.water.SetFloat("_DepthFade", 2.4f);
        art.water.SetFloat("_Refraction", 0.03f);
        art.water.SetFloat("_FoamDepth", 1.6f);
        art.water.SetFloat("_Glitter", 0.55f);
        art.water.SetFloat("_SkyExposure", 1.5f);
        art.water.SetFloat("_DawnExposure", 1.2f);
        art.water.SetFloat("_WaveAmp", 8f);
        art.water.SetFloat("_FadeStart", 520f);
        art.water.SetFloat("_FadeEnd", 630f);
        art.water.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        art.moon = UnlitMat("Assets/Game/Materials/Generated/Moon.mat", unlitShader, new Color(2.7f, 2.5f, 2.15f, 1f));
        art.unlit = UnlitMat("Assets/Game/Materials/Generated/SkyUnlit.mat", unlitShader, Color.white);
        art.unlit.SetTexture("_BaseMap", art.softCircle);
        art.column = UnlitMat("Assets/Game/Materials/Generated/LightColumn.mat", addShader, new Color(1f, 0.55f, 0.16f, 0.38f));
        art.wood = LitMat("Assets/Game/Materials/Generated/TorchWood.mat", lit, new Color(0.28f, 0.16f, 0.08f), 0.18f, 0f, Color.black);
        art.iron = LitMat("Assets/Game/Materials/Generated/TorchIron.mat", lit, new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.72f, Color.black);
        art.charred = LitMat("Assets/Game/Materials/Generated/CharredCloth.mat", lit, new Color(0.05f, 0.04f, 0.035f), 0.08f, 0f, new Color(0.15f, 0.05f, 0.01f));
        art.smoke = LitMat("Assets/Game/Materials/Generated/Smoke.mat", lit, new Color(0.18f, 0.18f, 0.2f, 0.22f), 0f, 0f, Color.black);
        SetupTransparent(art.smoke);
        art.flameA = FlameMat("Assets/Game/Materials/Generated/FlameA.mat", flameShader, 1.15f, 0f, 1.45f, 0.16f);
        art.flameB = FlameMat("Assets/Game/Materials/Generated/FlameB.mat", flameShader, 1.7f, 0.7f, 1.2f, 0.2f);
        art.flameC = FlameMat("Assets/Game/Materials/Generated/FlameC.mat", flameShader, 0.85f, 1.25f, 1.55f, 0.14f);
        art.heat = FlameMat("Assets/Game/Materials/Generated/HeatShimmer.mat", flameShader, 2.4f, 0.3f, 0.22f, 0.28f);
        art.heat.SetColor("_Core", new Color(1f, 0.85f, 0.55f, 0.15f));
        art.heat.SetColor("_Mid", new Color(1f, 0.45f, 0.15f, 0.08f));
        art.heat.SetColor("_Tip", new Color(0.4f, 0.05f, 0f, 0f));
        art.silhouette = LitMat("Assets/Game/Materials/Generated/Silhouette.mat", lit, new Color(0.02f, 0.025f, 0.03f), 0.05f, 0f, Color.black);
        art.foam = UnlitMat("Assets/Game/Materials/Generated/StoneFoam.mat", unlitShader, new Color(0.82f, 0.9f, 0.92f, 0.55f));
        art.foam.SetTexture("_BaseMap", art.softCircle);
        art.beam = UnlitMat("Assets/Game/Materials/Generated/LighthouseBeam.mat", addShader, new Color(1f, 0.94f, 0.78f, 0.08f));
        art.lamp = UnlitMat("Assets/Game/Materials/Generated/LighthouseLamp.mat", unlitShader, new Color(3.2f, 2.4f, 1.5f, 1f));
        ApplyPathRevealMaterial();
        art.path = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Generated/PathStone.mat");
        Cubemap nightCube = LoadSkyCubemap("Assets/Game/Textures/Sky/qwantani_moonrise_puresky_2k.hdr");
        Cubemap dawnCube = LoadSkyCubemap("Assets/Game/Textures/Sky/qwantani_dawn_puresky_2k.hdr");
        art.nightCube = nightCube;
        art.dawnCube = dawnCube;
        art.hdriSky = nightCube != null && dawnCube != null;
        if (art.hdriSky)
        {
            art.sky = ShaderMat("Assets/Game/Materials/Generated/NightSky.mat", skyBlend);
            art.sky.SetTexture("_NightCube", nightCube);
            art.sky.SetTexture("_DawnCube", dawnCube);
            art.sky.SetFloat("_Blend", 0f);
            art.sky.SetFloat("_Exposure", 1.5f);
            art.sky.SetFloat("_DawnExposure", 1.2f);
            art.sky.SetFloat("_Rotation", 0f);
            art.sky.SetColor("_Tint", Color.white);
            art.sky.SetColor("_HazeColor", new Color(0.07f, 0.16f, 0.2f, 1f));
            art.sky.SetFloat("_Haze", 0.48f);
            art.water.SetTexture("_SkyCube", nightCube);
            art.water.SetTexture("_DawnCube", dawnCube);
            art.moonDirection = BrightestCubemapDirection(nightCube);
            art.dawnDirection = BrightestCubemapDirection(dawnCube);
            ApplySkyExposure(art.sky, art.water);
            Debug.Log("HDRI sky night=qwantani_moonrise_puresky dawn=qwantani_dawn_puresky moon=" + art.moonDirection);
        }
        else
        {
            art.sky = UnlitMat("Assets/Game/Materials/Generated/NightSky.mat", skyShader, Color.white);
            art.sky.SetColor("_Top", new Color(0.015f, 0.03f, 0.09f, 1f));
            art.sky.SetColor("_Horizon", new Color(0.08f, 0.24f, 0.28f, 1f));
            art.sky.SetColor("_Ground", new Color(0.005f, 0.006f, 0.012f, 1f));
            art.sky.SetFloat("_Exponent", 1.55f);
            Debug.LogWarning("HDRI sky missing. Using SkyGradient.");
        }
        if (terrainShader != null)
        {
            art.terrain = LitMat("Assets/Game/Materials/Generated/TerrainLit.mat", terrainShader, Color.white, 0.1f, 0f, Color.black);
        }

        art.sandLayer = Layer("Assets/Game/Levels/Layers/Sand.terrainlayer", sand, sandNormal, sandMask, new Vector2(8f, 8f));
        art.grassLayer = Layer("Assets/Game/Levels/Layers/Grass.terrainlayer", grassTex, grassNormal, grassMask, new Vector2(6f, 6f));
        art.dirtLayer = Layer("Assets/Game/Levels/Layers/Dirt.terrainlayer", dirtTex, dirtNormal, dirtMask, new Vector2(5f, 5f));
        art.rockLayer = Layer("Assets/Game/Levels/Layers/Rock.terrainlayer", rockTex, rockNormal, rockMask, new Vector2(6f, 6f));
        art.mossLayer = Layer("Assets/Game/Levels/Layers/Moss.terrainlayer", mossTex, mossNormal, mossMask, new Vector2(6f, 6f));
        art.volume = EnsureVolume("Assets/Game/Materials/Generated/NightVolumeProfile.asset");

        art.pineS = SavePrefab(BuildPine(art, 1.05f), "Assets/Game/Prefabs/Props/PineSmall.prefab");
        art.pineM = SavePrefab(BuildPine(art, 1.45f), "Assets/Game/Prefabs/Props/PineMedium.prefab");
        art.pineL = SavePrefab(BuildPine(art, 1.9f), "Assets/Game/Prefabs/Props/PineLarge.prefab");
        art.rockA = SavePrefab(BuildRock(art, false), "Assets/Game/Prefabs/Props/RockCluster.prefab");
        art.rockB = SavePrefab(BuildRock(art, true), "Assets/Game/Prefabs/Props/RockWide.prefab");
        art.grassTuft = SavePrefab(BuildGrass(art), "Assets/Game/Prefabs/Props/GrassTuft.prefab");
        art.pebble = SavePrefab(BuildPebbles(art), "Assets/Game/Prefabs/Props/PebbleCluster.prefab");
        art.log = SavePrefab(BuildLog(art), "Assets/Game/Prefabs/Props/FallenLog.prefab");
        art.stump = SavePrefab(BuildStump(art), "Assets/Game/Prefabs/Props/Stump.prefab");
        art.mushroom = SavePrefab(BuildMushroom(art, false), "Assets/Game/Prefabs/Props/Mushroom.prefab");
        art.mushroomGlow = SavePrefab(BuildMushroom(art, true), "Assets/Game/Prefabs/Props/MushroomGlow.prefab");
        art.beacon = SavePrefab(BuildBeacon(art), "Assets/Game/Prefabs/Gameplay/Beacon.prefab");
        art.firefly = SavePrefab(BuildFirefly(art), "Assets/Game/Prefabs/Gameplay/Firefly.prefab");
        art.moth = SavePrefab(BuildMoth(art), "Assets/Game/Prefabs/Gameplay/Moth.prefab");
        PrepareKeeperImport();
        art.keeper = SavePrefab(BuildKeeper(art), "Assets/Game/Prefabs/Characters/Keeper.prefab");
        AssetDatabase.SaveAssets();
        return art;
    }

    public static void ApplyPathRevealMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/PathReveal");
        if (shader == null)
        {
            Debug.LogError("LanternKeeper/PathReveal is missing.");
            return;
        }

        Material mat = LitMat("Assets/Game/Materials/Generated/PathStone.mat", shader, new Color(0.78f, 0.72f, 0.55f, 1f), 0.2f, 0f, Color.black);
        mat.SetColor("_RimColor", new Color(1f, 0.58f, 0.24f, 1f));
        mat.SetFloat("_RimStrength", 1.65f);
        mat.SetFloat("_LKDissolve", 1f);
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetShaderPassEnabled("ShadowCaster", true);
        mat.SetShaderPassEnabled("DepthOnly", true);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        EditorUtility.SetDirty(mat);
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

    static TerrainLayer Layer(string path, Texture2D tex, Texture2D normal, Texture2D mask, Vector2 tile)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = tex;
        layer.normalMapTexture = normal;
        layer.maskMapTexture = mask;
        layer.normalScale = normal != null ? 1f : 0f;
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
        Prim(PrimitiveType.Cylinder, "Footing", root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.72f, 0.08f, 0.72f), art.beaconStone);
        Prim(PrimitiveType.Cylinder, "Pole", root.transform, new Vector3(0f, 1.46f, 0f), new Vector3(0.1f, 1.3f, 0.1f), art.wood);
        Prim(PrimitiveType.Cylinder, "BandLow", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.16f, 0.02f, 0.16f), art.iron);
        Prim(PrimitiveType.Cylinder, "BandMid", root.transform, new Vector3(0f, 1.45f, 0f), new Vector3(0.16f, 0.02f, 0.16f), art.iron);
        Prim(PrimitiveType.Cylinder, "BandHigh", root.transform, new Vector3(0f, 2.35f, 0f), new Vector3(0.16f, 0.022f, 0.16f), art.iron);
        float head = 2.58f;
        Prim(PrimitiveType.Sphere, "Cloth", root.transform, new Vector3(0f, head, 0f), new Vector3(0.2f, 0.26f, 0.2f), art.charred);
        Prim(PrimitiveType.Cube, "CageA", root.transform, new Vector3(0.12f, head, 0f), new Vector3(0.02f, 0.32f, 0.02f), art.iron);
        Prim(PrimitiveType.Cube, "CageB", root.transform, new Vector3(-0.12f, head, 0f), new Vector3(0.02f, 0.32f, 0.02f), art.iron);
        Prim(PrimitiveType.Cube, "CageC", root.transform, new Vector3(0f, head, 0.12f), new Vector3(0.02f, 0.32f, 0.02f), art.iron);
        Prim(PrimitiveType.Cube, "CageD", root.transform, new Vector3(0f, head, -0.12f), new Vector3(0.02f, 0.32f, 0.02f), art.iron);
        Prim(PrimitiveType.Cylinder, "CageRing", root.transform, new Vector3(0f, head + 0.16f, 0f), new Vector3(0.28f, 0.012f, 0.28f), art.iron);

        GameObject wisp = new GameObject("Wisp");
        wisp.transform.SetParent(root.transform, false);
        wisp.transform.localPosition = new Vector3(0f, head + 0.28f, 0f);
        MakeParticles(wisp, art.unlit, 1.6f, 0.35f, 0.12f, new Color(0.75f, 0.75f, 0.78f, 0.22f), 4f, 0, true, true, 0.04f, true);

        GameObject flameRoot = new GameObject("FlameRoot");
        flameRoot.transform.SetParent(root.transform, false);
        flameRoot.transform.localPosition = new Vector3(0f, head + 0.12f, 0f);
        Prim(PrimitiveType.Quad, "FlameA", flameRoot.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.95f, 1f), art.flameA);
        Prim(PrimitiveType.Quad, "FlameB", flameRoot.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.3f, 0.78f, 1f), art.flameB);
        Prim(PrimitiveType.Quad, "FlameC", flameRoot.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1.08f, 1f), art.flameC);
        Prim(PrimitiveType.Quad, "Heat", flameRoot.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.4f, 0.55f, 1f), art.heat);
        StripColliders(flameRoot);

        GameObject sparks = new GameObject("Sparks");
        sparks.transform.SetParent(flameRoot.transform, false);
        sparks.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem sparkSystem = MakeParticles(sparks, art.unlit, 0.9f, 1.4f, 0.07f, new Color(1f, 0.62f, 0.22f, 0.9f), 14f, 0, true, false, 0.04f);
        ParticleSystem.ShapeModule sparkShape = sparkSystem.shape;
        sparkShape.shapeType = ParticleSystemShapeType.Cone;
        sparkShape.angle = 10f;

        GameObject flare = new GameObject("Flare");
        flare.transform.SetParent(root.transform, false);
        flare.transform.localPosition = new Vector3(0f, head + 0.2f, 0f);
        MakeParticles(flare, art.unlit, 0.45f, 2.4f, 0.12f, new Color(1f, 0.85f, 0.45f, 0.85f), 0f, 18, false, true, 0.08f);

        GameObject smoke = new GameObject("Smoke");
        smoke.transform.SetParent(flameRoot.transform, false);
        smoke.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        MakeParticles(smoke, art.unlit, 2.2f, 0.45f, 0.22f, new Color(0.15f, 0.15f, 0.16f, 0.28f), 6f, 0, true, true, 0.05f);

        GameObject lightObject = new GameObject("BeaconLight");
        lightObject.transform.SetParent(flameRoot.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.58f, 0.22f);
        light.range = 16f;
        light.intensity = 3.4f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        flameRoot.transform.localScale = Vector3.one;

        Beacon beacon = root.AddComponent<Beacon>();
        SerializedObject so = new SerializedObject(beacon);
        so.FindProperty("beaconLight").objectReferenceValue = light;
        so.FindProperty("fire").objectReferenceValue = sparkSystem;
        so.FindProperty("embers").objectReferenceValue = flare.GetComponent<ParticleSystem>();
        so.FindProperty("smoke").objectReferenceValue = smoke.GetComponent<ParticleSystem>();
        so.FindProperty("flameRoot").objectReferenceValue = flameRoot.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        StripColliders(root);
        CapsuleCollider blocker = root.AddComponent<CapsuleCollider>();
        blocker.center = new Vector3(0f, 1.35f, 0f);
        blocker.height = 2.7f;
        blocker.radius = 0.22f;
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
        GameObject imported = BuildImportedKeeper(art);
        if (imported != null)
        {
            return imported;
        }

        return BuildPrimitiveKeeper(art);
    }

    static ParticleSystem MakeParticles(GameObject go, Material mat, float life, float speed, float size, Color color, float rate, int burst, bool loop, bool world, float radius, bool playAwake = false)
    {
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = playAwake;
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

    static Material ShaderMat(string path, Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null && mat.shader != shader)
        {
            AssetDatabase.DeleteAsset(path);
            mat = null;
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void LoadWaterMaps(out Texture2D normalA, out Texture2D normalB, out Texture2D foam)
    {
        normalA = LoadWaterTexture("Assets/Game/Textures/Water/WaterRippleNormalA.png", true);
        normalB = LoadWaterTexture("Assets/Game/Textures/Water/WaterRippleNormalB.png", true);
        foam = LoadWaterTexture("Assets/Game/Textures/Water/WaterFoam.png", false);
        if (normalA == null)
        {
            normalA = TileableNormal("Assets/Game/Textures/Generated/WaterNormalA.png", 256, 3f, 0.35f, 2.6f);
        }

        if (normalB == null)
        {
            normalB = TileableNormal("Assets/Game/Textures/Generated/WaterNormalB.png", 256, 5f, 1.8f, 3.1f);
        }

        if (foam == null)
        {
            foam = TileableFoam("Assets/Game/Textures/Generated/FoamNoise.png", 256);
        }
    }

    static void ApplyWaterLook(Material water, Texture2D normalA, Texture2D normalB, Texture2D foam)
    {
        water.SetTexture("_NormalA", normalA);
        water.SetTexture("_NormalB", normalB);
        water.SetTexture("_FoamNoise", foam);
        // Pack absorption (0.210, 0.688, 0.805), darkened so the night lake stays teal rather than daylight cyan.
        water.SetColor("_ShallowColor", new Color(0.105f, 0.344f, 0.402f, 0.42f));
        water.SetColor("_DeepColor", new Color(0.016f, 0.055f, 0.064f, 0.9f));
        EditorUtility.SetDirty(water);
    }

    static void ApplyWaterTextures(Material water)
    {
        if (water == null)
        {
            return;
        }

        Texture2D normalA;
        Texture2D normalB;
        Texture2D foam;
        LoadWaterMaps(out normalA, out normalB, out foam);
        ApplyWaterLook(water, normalA, normalB, foam);
    }

    static Texture2D LoadWaterTexture(string path, bool normal)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool typeOk = normal
                ? importer.textureType == TextureImporterType.NormalMap && !importer.convertToNormalmap
                : importer.textureType == TextureImporterType.Default;
            bool ready = typeOk
                && importer.mipmapEnabled
                && importer.wrapMode == TextureWrapMode.Repeat
                && importer.maxTextureSize == 1024
                && !importer.sRGBTexture
                && importer.textureCompression == TextureImporterCompression.Uncompressed;
            if (!ready)
            {
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.convertToNormalmap = false;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D TileableNormal(string path, int size, float frequency, float phase, float strength)
    {
        float[,] height = new float[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float h = Mathf.Sin((u * frequency + v * (frequency * 0.37f) + phase) * Mathf.PI * 2f);
                h += 0.45f * Mathf.Sin((u * (frequency + 3f) - v * (frequency + 1f) + phase * 1.7f) * Mathf.PI * 2f);
                h += 0.22f * Mathf.Sin((u * (frequency * 2f) + v * 5f + phase) * Mathf.PI * 2f);
                height[y, x] = h;
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
                Vector3 normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                tex.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
            }
        }

        tex.Apply();
        return SaveTexture(path, tex, true, false, true);
    }

    static Texture2D TileableFoam(string path, int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float n = 0.5f + 0.5f * Mathf.Sin((u * 4f + v * 2f) * Mathf.PI * 2f);
                n += 0.35f * (0.5f + 0.5f * Mathf.Sin((u * 9f - v * 7f) * Mathf.PI * 2f));
                n += 0.2f * (0.5f + 0.5f * Mathf.Sin((u * 15f + v * 13f) * Mathf.PI * 2f));
                n = Mathf.Clamp01(n / 1.55f);
                float g = 0.5f + 0.5f * Mathf.Sin((u * 6f - v * 5f + 0.4f) * Mathf.PI * 2f);
                g += 0.3f * (0.5f + 0.5f * Mathf.Sin((u * 11f + v * 3f) * Mathf.PI * 2f));
                g = Mathf.Clamp01(g / 1.3f);
                tex.SetPixel(x, y, new Color(n, g, n, 1f));
            }
        }

        tex.Apply();
        Texture2D saved = SaveTexture(path, tex, false, false, true);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return saved;
    }

    static Cubemap LoadSkyCubemap(string assetPath)
    {
        if (!System.IO.File.Exists(assetPath))
        {
            return null;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return null;
        }

        bool ready = importer.textureShape == TextureImporterShape.TextureCube
            && importer.generateCubemap == TextureImporterGenerateCubemap.Cylindrical
            && importer.mipmapEnabled
            && importer.isReadable
            && !importer.sRGBTexture
            && importer.textureCompression == TextureImporterCompression.Compressed;
        if (!ready)
        {
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Cubemap>(assetPath);
    }

    static Vector3 BrightestCubemapDirection(Cubemap cube)
    {
        if (cube == null)
        {
            return Vector3.zero;
        }

        float best = -1f;
        Vector3 bestDir = Vector3.zero;
        int size = cube.width;
        int step = size > 768 ? 2 : 1;
        try
        {
            for (int faceIndex = 0; faceIndex < 6; faceIndex++)
            {
                CubemapFace face = (CubemapFace)faceIndex;
                Color[] pixels = cube.GetPixels(face, 0);
                if (pixels == null)
                {
                    continue;
                }

                for (int y = 0; y < size; y += step)
                {
                    for (int x = 0; x < size; x += step)
                    {
                        int index = y * size + x;
                        if (index < 0 || index >= pixels.Length)
                        {
                            continue;
                        }

                        Color color = pixels[index];
                        float lum = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
                        if (lum > best)
                        {
                            best = lum;
                            float u = (x + 0.5f) / size;
                            float v = (y + 0.5f) / size;
                            bestDir = CubemapDirection(face, u, v);
                        }
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Could not read the sky cubemap. " + exception.Message);
            return Vector3.zero;
        }

        return bestDir.sqrMagnitude > 0.01f ? bestDir.normalized : Vector3.zero;
    }

    static Vector3 CubemapDirection(CubemapFace face, float u, float v)
    {
        float sc = u * 2f - 1f;
        float tc = v * 2f - 1f;
        Vector3 direction;
        switch (face)
        {
            case CubemapFace.PositiveX:
                direction = new Vector3(1f, -tc, -sc);
                break;
            case CubemapFace.NegativeX:
                direction = new Vector3(-1f, -tc, sc);
                break;
            case CubemapFace.PositiveY:
                direction = new Vector3(sc, 1f, tc);
                break;
            case CubemapFace.NegativeY:
                direction = new Vector3(sc, -1f, -tc);
                break;
            case CubemapFace.PositiveZ:
                direction = new Vector3(sc, -tc, 1f);
                break;
            default:
                direction = new Vector3(-sc, -tc, -1f);
                break;
        }

        return direction.normalized;
    }

    static Vector3 LightEulerFromSkyDirection(Vector3 skyDirection)
    {
        if (skyDirection.sqrMagnitude < 0.2f)
        {
            return new Vector3(10f, 28f, 0f);
        }

        Vector3 forward = -skyDirection.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(forward, up).eulerAngles;
    }

    static void ApplySkyExposure(Material sky, Material water)
    {
        if (sky == null)
        {
            return;
        }

        Cubemap nightCube = sky.GetTexture("_NightCube") as Cubemap;
        Cubemap dawnCube = sky.GetTexture("_DawnCube") as Cubemap;
        Color nightColor;
        float nightLum;
        int nightCount;
        SampleHorizon(nightCube, 32f, 70f, out nightColor, out nightLum, out nightCount);
        Color nightGlow;
        float nightGlowLum;
        int nightGlowCount;
        SampleHorizon(nightCube, 4f, 16f, out nightGlow, out nightGlowLum, out nightGlowCount);
        Color dawnColor;
        float dawnLum;
        int dawnCount;
        SampleHorizon(dawnCube, 4f, 16f, out dawnColor, out dawnLum, out dawnCount);
        Color dawnBody;
        float dawnBodyLum;
        int dawnBodyCount;
        SampleHorizon(dawnCube, 28f, 60f, out dawnBody, out dawnBodyLum, out dawnBodyCount);

        float nightExposure = 1.5f;
        if (nightLum > 0.0008f)
        {
            float maxChannel = Mathf.Max(nightColor.r, Mathf.Max(nightColor.g, nightColor.b));
            float desired = Mathf.Clamp(nightLum * 0.38f * 4f, 0.08f, 0.36f);
            nightExposure = desired / nightLum;
            if (maxChannel > 0.0001f)
            {
                nightExposure = Mathf.Min(nightExposure, 0.85f / maxChannel);
            }

            float glowMax = Mathf.Max(nightGlow.r, Mathf.Max(nightGlow.g, nightGlow.b));
            if (glowMax > 0.05f)
            {
                nightExposure = Mathf.Min(nightExposure, 0.5f / glowMax);
            }

            nightExposure = Mathf.Clamp(nightExposure, 0.45f, 0.65f);
        }

        float dawnExposure = 0.55f;
        if (dawnLum > 0.0008f)
        {
            float maxChannel = Mathf.Max(dawnColor.r, Mathf.Max(dawnColor.g, dawnColor.b));
            dawnExposure = 1.05f / Mathf.Max(maxChannel, 0.05f);
            float bodyMax = Mathf.Max(dawnBody.r, Mathf.Max(dawnBody.g, dawnBody.b));
            if (dawnBodyLum > 0.0008f && bodyMax * dawnExposure > 0.95f)
            {
                dawnExposure = 0.9f / Mathf.Max(bodyMax, 0.0001f);
            }

            dawnExposure = Mathf.Clamp(dawnExposure, 0.25f, 2.2f);
        }

        if (sky.HasProperty("_Exposure"))
        {
            sky.SetFloat("_Exposure", nightExposure);
        }

        if (sky.HasProperty("_DawnExposure"))
        {
            sky.SetFloat("_DawnExposure", dawnExposure);
        }

        if (sky.HasProperty("_Blend"))
        {
            sky.SetFloat("_Blend", 0f);
        }

        if (sky.HasProperty("_Haze"))
        {
            sky.SetFloat("_Haze", 0.48f);
        }

        if (nightCube != null && sky.HasProperty("_MoonDir"))
        {
            Vector3 moon = BrightestCubemapDirection(nightCube);
            if (moon.sqrMagnitude > 0.2f)
            {
                sky.SetVector("_MoonDir", new Vector4(moon.x, moon.y, moon.z, 0f));
            }
        }

        if (water != null)
        {
            water.SetFloat("_SkyExposure", nightExposure);
            water.SetFloat("_WaveAmp", 8f);
            water.SetFloat("_FoamDepth", 1.6f);
            if (water.HasProperty("_DawnExposure"))
            {
                water.SetFloat("_DawnExposure", dawnExposure);
            }

            if (water.HasProperty("_FadeStart"))
            {
                water.SetFloat("_FadeStart", 520f);
                water.SetFloat("_FadeEnd", 630f);
            }

            if (dawnCube != null)
            {
                water.SetTexture("_DawnCube", dawnCube);
            }

            EditorUtility.SetDirty(water);
        }

        EditorUtility.SetDirty(sky);
        Debug.Log("Sky exposure night=" + nightExposure.ToString("0.00")
            + " dawn=" + dawnExposure.ToString("0.00")
            + " nightLum=" + nightLum.ToString("0.000")
            + " dawnLum=" + dawnLum.ToString("0.000")
            + " nightRGB=" + nightColor
            + " dawnRGB=" + dawnColor
            + " dawnBody=" + dawnBody
            + " samples=" + nightCount + "/" + dawnCount + "/" + dawnBodyCount);
    }

    static void SampleHorizon(Cubemap cube, float minDegrees, float maxDegrees, out Color average, out float luminance, out int count)
    {
        average = Color.black;
        luminance = 0f;
        count = 0;
        if (cube == null)
        {
            return;
        }

        var colors = new List<Color>(1024);
        int size = cube.width;
        int step = Mathf.Max(1, size / 64);
        float minY = Mathf.Sin(minDegrees * Mathf.Deg2Rad);
        float maxY = Mathf.Sin(maxDegrees * Mathf.Deg2Rad);
        try
        {
            for (int faceIndex = 0; faceIndex < 6; faceIndex++)
            {
                CubemapFace face = (CubemapFace)faceIndex;
                Color[] pixels = cube.GetPixels(face, 0);
                if (pixels == null)
                {
                    continue;
                }

                for (int y = 0; y < size; y += step)
                {
                    for (int x = 0; x < size; x += step)
                    {
                        int index = y * size + x;
                        if (index < 0 || index >= pixels.Length)
                        {
                            continue;
                        }

                        float u = (x + 0.5f) / size;
                        float v = (y + 0.5f) / size;
                        Vector3 direction = CubemapDirection(face, u, v);
                        if (direction.y < minY || direction.y > maxY)
                        {
                            continue;
                        }

                        colors.Add(pixels[index]);
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Sky horizon sample failed. " + exception.Message);
            return;
        }

        if (colors.Count < 8)
        {
            return;
        }

        colors.Sort((a, b) => Luminance(a).CompareTo(Luminance(b)));
        int start = colors.Count / 8;
        int end = colors.Count - start;
        double r = 0;
        double g = 0;
        double b = 0;
        double lum = 0;
        int used = 0;
        for (int i = start; i < end; i++)
        {
            Color color = colors[i];
            r += color.r;
            g += color.g;
            b += color.b;
            lum += Luminance(color);
            used++;
        }

        if (used == 0)
        {
            return;
        }

        average = new Color((float)(r / used), (float)(g / used), (float)(b / used), 1f);
        luminance = (float)(lum / used);
        count = used;
    }

    static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }
}
}
