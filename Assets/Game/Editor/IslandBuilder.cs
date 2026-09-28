using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    const float WaterFraction = 0.2f;

    [MenuItem("Lantern Keeper/Build Island")]
    public static void BuildFromMenu()
    {
        LevelConfig config = Selection.activeObject as LevelConfig;
        if (config == null)
        {
            config = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Game/Levels/Island1.asset");
        }

        Build(config);
    }

    [MenuItem("Lantern Keeper/Build All Levels")]
    public static void BuildAll()
    {
        CreateDefaultConfigs();
        LevelConfig island1 = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Game/Levels/Island1.asset");
        Build(island1);
        LevelConfig island2 = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Game/Levels/Island2.asset");
        Build(island2);
        BuildMainMenu();
        ApplyBuildSettings();
    }

    public static void CreateDefaultConfigs()
    {
        EnsureFolder("Assets/Game/Levels");
        WriteConfig("Assets/Game/Levels/Island1.asset", "island1", "Island 1", "Island1", 28f, 9f, 1101, 5, 14, 4, 1, new Color(0.4f, 0.52f, 0.56f, 1f), 0.011f, "Island2");
        WriteConfig("Assets/Game/Levels/Island2.asset", "island2", "Island 2", "Island2", 40f, 12f, 2202, 7, 22, 8, 2, new Color(0.36f, 0.48f, 0.54f, 1f), 0.014f, "");
        AssetDatabase.SaveAssets();
    }

    public static void ApplyBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Game/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Game/Scenes/Island1.unity", true),
            new EditorBuildSettingsScene("Assets/Game/Scenes/Island2.unity", true)
        };
        Debug.Log("Build settings: MainMenu, Island1, Island2");
    }

    public static void BuildMainMenu()
    {
        LevelConfig preview = ScriptableObject.CreateInstance<LevelConfig>();
        preview.levelId = "menu";
        preview.displayName = "Menu";
        preview.sceneName = "MainMenu";
        preview.islandRadius = 20f;
        preview.hillHeight = 7.5f;
        preview.seed = 1101;
        preview.beaconCount = 0;
        preview.fireflyCount = 8;
        preview.mothCount = 0;
        preview.hiddenPathCount = 0;
        preview.fogColor = new Color(0.4f, 0.52f, 0.56f, 1f);
        preview.fogDensity = 0.013f;
        preview.grassDetailDensity = 8;
        preview.nextLevelScene = "";
        BuildInternal(preview, false);
        Object.DestroyImmediate(preview);
    }

    public static void Build(LevelConfig config)
    {
        BuildInternal(config, true);
    }

    static void BuildInternal(LevelConfig config, bool gameplay)
    {
        if (config == null)
        {
            Debug.LogError("LevelConfig is missing.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before building a level.");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Lantern Keeper", "Building " + config.sceneName, 0.15f);
            EnsureFolder("Assets/Game/Scenes");
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string terrainPath = "Assets/Game/Levels/" + config.sceneName + "_Terrain.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath) != null)
            {
                AssetDatabase.DeleteAsset(terrainPath);
            }

            ArtKit art = EnsureArt();
            Biome resolvedBiome = ResolveBiome(config);
            if (resolvedBiome != null)
            {
                config.biome = resolvedBiome;
            }

            EditorUtility.DisplayProgressBar("Lantern Keeper", "Shaping " + config.sceneName, 0.45f);
            Stage stage = new Stage();
            BuildSculptedTerrain(config, art, terrainPath, stage);
            CreateWater(config, art, stage);
            CreateAtmosphere(config, art, stage);
            if (gameplay)
            {
                PlaceBeacons(config, art, stage);
                PlacePaths(config, art, stage);
            }
            else
            {
                PlaceShowBeacon(art, stage);
            }

            Scatter(config, art, stage);
            PlaceFireflies(config, art, stage);
            if (gameplay)
            {
                SpawnKeeper(art, stage, true);
                CreateGameplay(config, art, stage);
                CreateHud(art, stage);
            }
            else
            {
                Vector3 statue = new Vector3(2.2f, 0f, 2.4f);
                statue.y = GroundY(stage.terrain, statue.x, statue.z);
                SpawnKeeper(art, stage, false, statue);
                CreateMenu(art, stage);
            }

            string scenePath = "Assets/Game/Scenes/" + config.sceneName + ".unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Built " + scenePath + " beacons=" + stage.beacons + " fireflies=" + stage.fireflies + " stones=" + stage.stones + " waterY=" + stage.waterY.ToString("0.00"));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    class Stage
    {
        public Terrain terrain;
        public float waterY;
        public float worldSize;
        public Vector3 spawn;
        public Light sun;
        public StarTwinkle stars;
        public Volume volume;
        public GameObject player;
        public Lantern lantern;
        public int beacons;
        public int fireflies;
        public int stones;
        public readonly List<Vector3> beaconSpots = new List<Vector3>();
        public readonly List<Vector3> islets = new List<Vector3>();
        public readonly List<List<Vector2>> trails = new List<List<Vector2>>();
    }

    static void BuildTerrain(LevelConfig config, ArtKit art, string terrainPath, Stage stage)
    {
        int resolution = config.islandRadius < 22f ? 129 : 257;
        stage.worldSize = config.islandRadius * 2f;
        float[,] heights = BuildHeights(config, resolution, stage.islets);
        TerrainData data = new TerrainData();
        data.heightmapResolution = resolution;
        data.size = new Vector3(stage.worldSize, config.hillHeight, stage.worldSize);
        AssetDatabase.CreateAsset(data, terrainPath);
        data.size = new Vector3(stage.worldSize, config.hillHeight, stage.worldSize);
        data.SetHeights(0, 0, heights);
        data.alphamapResolution = 128;
        data.baseMapResolution = 256;
        data.terrainLayers = new[] { art.sandLayer, art.grassLayer, art.rockLayer };
        data.SetAlphamaps(0, 0, BuildAlpha(heights, resolution, 128, config.hillHeight, stage.worldSize));
        EditorUtility.SetDirty(data);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Island";
        terrainObject.transform.position = new Vector3(-stage.worldSize * 0.5f, 0f, -stage.worldSize * 0.5f);
        stage.terrain = terrainObject.GetComponent<Terrain>();
        if (art.terrain != null)
        {
            stage.terrain.materialTemplate = art.terrain;
        }

        stage.terrain.drawInstanced = true;
        stage.terrain.basemapDistance = 300f;
        stage.terrain.heightmapPixelError = 5f;
        stage.waterY = WaterFraction * config.hillHeight;
        stage.spawn = new Vector3(0f, GroundY(stage.terrain, 0f, 0f) + 0.05f, 0f);
    }

    static float[,] BuildHeights(LevelConfig config, int resolution, List<Vector3> islets)
    {
        float[,] heights = new float[resolution, resolution];
        float ox = (config.seed % 500) * 0.13f;
        float oz = (config.seed % 320) * 0.17f;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x / (float)(resolution - 1) - 0.5f) * 2f;
                float dz = (z / (float)(resolution - 1) - 0.5f) * 2f;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float n1 = Mathf.PerlinNoise(dx * 1.7f + ox, dz * 1.7f + oz);
                float n2 = Mathf.PerlinNoise(dx * 3.4f + ox * 1.6f, dz * 3.4f + 5f);
                float inland = 0.58f + n1 * 0.28f + n2 * 0.12f;
                float shore = Mathf.SmoothStep(0.42f, 0.96f, dist);
                float h = Mathf.Lerp(inland, WaterFraction * 0.25f, shore);
                if (dist < 0.08f)
                {
                    h = Mathf.Lerp(h, 0.52f, 1f - dist / 0.08f);
                }

                heights[z, x] = Mathf.Clamp01(h);
            }
        }

        int paths = config.hiddenPathCount;
        if (paths <= 0)
        {
            return heights;
        }

        float start = (config.seed % 360) * Mathf.Deg2Rad;
        for (int i = 0; i < paths; i++)
        {
            float angle = start + i * (Mathf.PI * 2f / paths);
            CarveChannel(heights, resolution, angle);
            AddIslet(heights, resolution, angle, config.islandRadius);
            float reach = 0.95f * config.islandRadius;
            islets.Add(new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach));
        }

        return heights;
    }

    static void CarveChannel(float[,] heights, int resolution, float angle)
    {
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x / (float)(resolution - 1) - 0.5f) * 2f;
                float dz = (z / (float)(resolution - 1) - 0.5f) * 2f;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < 0.68f || dist > 0.9f || dist < 0.001f)
                {
                    continue;
                }

                float ang = Mathf.Atan2(dz, dx);
                float delta = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float lateral = Mathf.Abs(delta) * dist;
                if (lateral < 0.07f)
                {
                    heights[z, x] = Mathf.Min(heights[z, x], WaterFraction * 0.35f);
                }
            }
        }
    }

    static void AddIslet(float[,] heights, int resolution, float angle, float radius)
    {
        float distNorm = 0.95f;
        float cx = Mathf.Cos(angle) * distNorm;
        float cz = Mathf.Sin(angle) * distNorm;
        float sigma = 2.4f / Mathf.Max(8f, radius);
        float target = WaterFraction + 0.16f;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x / (float)(resolution - 1) - 0.5f) * 2f;
                float dz = (z / (float)(resolution - 1) - 0.5f) * 2f;
                float ddx = dx - cx;
                float ddz = dz - cz;
                float bump = Mathf.Exp(-(ddx * ddx + ddz * ddz) / (2f * sigma * sigma));
                float raised = target * bump;
                if (heights[z, x] < raised)
                {
                    heights[z, x] = raised;
                }
            }
        }
    }

    static float[,,] BuildAlpha(float[,] heights, int heightRes, int alphaRes, float hill, float worldSize)
    {
        float[,,] maps = new float[alphaRes, alphaRes, 3];
        float run = worldSize / (heightRes - 1);
        for (int z = 0; z < alphaRes; z++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                int hx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(alphaRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(z / (float)(alphaRes - 1) * (heightRes - 1)), 0, heightRes - 1);
                int hx2 = Mathf.Clamp(hx + 1, 0, heightRes - 1);
                float h = heights[hz, hx];
                float slope = Mathf.Abs(heights[hz, hx2] - h) * hill / run;
                float sand = Mathf.SmoothStep(WaterFraction + 0.16f, WaterFraction + 0.02f, h);
                float rock = Mathf.Max(Mathf.SmoothStep(0.5f, 1f, slope), Mathf.SmoothStep(0.68f, 0.9f, h));
                float grass = Mathf.Clamp01(1f - sand * 0.9f - rock);
                float sum = sand + rock + grass;
                if (sum < 0.001f)
                {
                    grass = 1f;
                    sum = 1f;
                }

                maps[z, x, 0] = sand / sum;
                maps[z, x, 1] = grass / sum;
                maps[z, x, 2] = rock / sum;
            }
        }

        return maps;
    }

    static float GroundY(Terrain terrain, float x, float z)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return 0f;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float u = Mathf.Clamp01((x - origin.x) / size.x);
        float v = Mathf.Clamp01((z - origin.z) / size.z);
        return origin.y + terrain.terrainData.GetInterpolatedHeight(u, v);
    }

    static void CreateAtmosphere(LevelConfig config, ArtKit art, Stage stage)
    {
        RenderSettings.skybox = art.sky;
        RenderSettings.ambientSkyColor = new Color(0.09f, 0.12f, 0.22f);
        RenderSettings.ambientEquatorColor = new Color(0.1f, 0.28f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.028f, 0.035f);
        if (art.hdriSky)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.18f;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = config.fogColor;
        RenderSettings.fogDensity = config.fogDensity;

        GameObject moon = new GameObject("Moonlight");
        Light sun = moon.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.82f, 0.88f, 1f);
        sun.intensity = 1.08f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        if (art.moonDirection.sqrMagnitude > 0.25f)
        {
            moon.transform.rotation = Quaternion.LookRotation(-art.moonDirection.normalized, Mathf.Abs(art.moonDirection.y) > 0.92f ? Vector3.forward : Vector3.up);
        }
        else
        {
            moon.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
        }

        RenderSettings.sun = sun;
        stage.sun = sun;

        GameObject fill = new GameObject("SkyFill");
        Light fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.35f, 0.6f, 0.62f);
        fillLight.intensity = 0.2f;
        fillLight.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(18f, 150f, 0f);

        GameObject volumeObject = new GameObject("Global Volume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = art.volume;
        volume.weight = 1f;
        stage.volume = volume;

        GameObject sky = new GameObject("Sky");
        stage.stars = CreateStars(sky.transform, art);
        if (art.hdriSky)
        {
            stage.stars.gameObject.SetActive(false);
        }

        CreateHorizon(config, art, stage);
        CreateReflectionProbe(stage);
        DawnSequence.ApplyNightGlobals();
        CreateMist(art, stage);
        CreateCameraShell(stage, config.islandRadius);
    }

    static StarTwinkle CreateStars(Transform parent, ArtKit art)
    {
        GameObject stars = new GameObject("Stars");
        stars.transform.SetParent(parent, false);
        ParticleSystem system = stars.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = 8000f;
        main.startSpeed = 0f;
        main.startSize = 0.55f;
        main.startColor = Color.white;
        main.maxParticles = 240;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)200) });
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 95f;
        shape.radiusThickness = 0f;
        ParticleSystemRenderer renderer = stars.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = art.unlit;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return stars.AddComponent<StarTwinkle>();
    }

    static void CreateMist(ArtKit art, Stage stage)
    {
        GameObject mist = new GameObject("GroundMist");
        mist.transform.position = new Vector3(0f, stage.waterY + 0.7f, 0f);
        ParticleSystem system = mist.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = 0.18f;
        main.startSize = 7f;
        main.startColor = new Color(0.75f, 0.86f, 0.9f, 0.07f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 10f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(stage.worldSize * 0.7f, 1.2f, stage.worldSize * 0.7f);
        ParticleSystemRenderer renderer = mist.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = art.unlit;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static void CreateCameraShell(Stage stage, float radius)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 900f;
        camera.fieldOfView = 58f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;
        cameraObject.AddComponent<AudioListener>();
        UniversalAdditionalCameraData data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
        {
            data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }

        data.renderPostProcessing = true;
        cameraObject.transform.position = new Vector3(0f, 5.2f, -7.2f);
        cameraObject.transform.LookAt(new Vector3(0f, 2f, 0f));
    }

    static void PlaceBeacons(LevelConfig config, ArtKit art, Stage stage)
    {
        Transform parent = Folder("Beacons");
        List<Vector3> planned = new List<Vector3>(stage.beaconSpots);
        if (planned.Count == 0)
        {
            List<Vector2> fallback = PlanBeaconXZ(config, out _);
            for (int i = 0; i < fallback.Count; i++)
            {
                planned.Add(new Vector3(fallback[i].x, 0f, fallback[i].y));
            }
        }

        stage.beaconSpots.Clear();
        for (int i = 0; i < planned.Count; i++)
        {
            Vector3 spot = planned[i];
            spot.y = GroundY(stage.terrain, spot.x, spot.z);
            if (spot.y < stage.waterY + 0.55f)
            {
                Vector3 inward = new Vector3(spot.x, 0f, spot.z);
                if (inward.sqrMagnitude > 0.01f)
                {
                    inward = inward.normalized * config.islandRadius * 0.28f;
                }

                spot = new Vector3(inward.x, 0f, inward.z);
                spot.y = GroundY(stage.terrain, spot.x, spot.z);
            }

            SpawnBeacon(art, parent, spot, true);
            stage.beaconSpots.Add(spot);
            stage.beacons++;
        }
    }

    static void PlaceShowBeacon(ArtKit art, Stage stage)
    {
        Vector3 spot = new Vector3(8f, 0f, 5f);
        spot.y = GroundY(stage.terrain, spot.x, spot.z);
        GameObject beacon = SpawnBeacon(art, Folder("Beacons"), spot, false);
        Light light = beacon.GetComponentInChildren<Light>(true);
        if (light != null)
        {
            light.enabled = true;
            light.intensity = 3.4f;
            light.range = 16f;
        }

        Transform flame = beacon.transform.Find("FlameRoot");
        if (flame != null)
        {
            flame.localScale = Vector3.one;
        }

        ParticleSystem[] particles = beacon.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.playOnAwake = true;
        }

        Beacon script = beacon.GetComponent<Beacon>();
        if (script != null)
        {
            Object.DestroyImmediate(script);
        }

        stage.beaconSpots.Add(spot);
        stage.beacons++;
    }

    static GameObject SpawnBeacon(ArtKit art, Transform parent, Vector3 spot, bool gameplay)
    {
        GameObject beacon = (GameObject)PrefabUtility.InstantiatePrefab(art.beacon);
        beacon.transform.SetParent(parent, true);
        beacon.transform.position = spot;
        beacon.transform.rotation = Quaternion.Euler(0f, spot.x * 10f, 0f);
        if (!gameplay)
        {
            return beacon;
        }

        return beacon;
    }

    static void PlacePaths(LevelConfig config, ArtKit art, Stage stage)
    {
        Transform parent = Folder("HiddenPaths");
        for (int i = 0; i < stage.islets.Count; i++)
        {
            Vector3 islet = stage.islets[i];
            Vector3 inward = new Vector3(-islet.x, 0f, -islet.z);
            if (inward.sqrMagnitude < 0.01f)
            {
                inward = Vector3.back;
            }

            inward.Normalize();
            Vector3 shore = new Vector3(islet.x, stage.waterY, islet.z) + inward * 8f;
            bool crossedWater = false;
            for (float distance = 1.2f; distance < config.islandRadius; distance += 0.6f)
            {
                Vector3 probe = new Vector3(islet.x, 0f, islet.z) + inward * distance;
                float y = GroundY(stage.terrain, probe.x, probe.z);
                if (y < stage.waterY + 0.2f)
                {
                    crossedWater = true;
                    continue;
                }

                if (crossedWater && y > stage.waterY + 0.4f)
                {
                    shore = new Vector3(probe.x, y, probe.z);
                    break;
                }
            }

            if (!crossedWater)
            {
                shore = inward * (config.islandRadius * 0.62f);
                shore.y = GroundY(stage.terrain, shore.x, shore.z);
            }

            Vector3 from = new Vector3(shore.x, stage.waterY + 0.12f, shore.z);
            Vector3 to = new Vector3(islet.x, stage.waterY + 0.12f, islet.z);
            float span = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(to.x, 0f, to.z));
            int steps = Mathf.Max(4, Mathf.RoundToInt(span / 0.82f));
            for (int s = 1; s < steps; s++)
            {
                float t = s / (float)steps;
                Vector3 pos = Vector3.Lerp(from, to, t);
                float ground = GroundY(stage.terrain, pos.x, pos.z);
                if (ground > stage.waterY + 1.1f)
                {
                    continue;
                }

                if (ground + 0.02f > pos.y)
                {
                    pos.y = ground + 0.02f;
                }

                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stone.name = "SteppingStone";
                stone.transform.SetParent(parent, true);
                stone.transform.position = pos;
                stone.transform.localScale = new Vector3(1.15f, 0.07f, 1.15f);
                stone.GetComponent<Renderer>().sharedMaterial = art.path;
                stone.AddComponent<LightRevealed>();
                stage.stones++;

                GameObject foam = GameObject.CreatePrimitive(PrimitiveType.Quad);
                foam.name = "StoneFoam";
                foam.transform.SetParent(parent, true);
                foam.transform.position = pos + new Vector3(0f, 0.06f, 0f);
                foam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                foam.transform.localScale = new Vector3(2.35f, 2.35f, 1f);
                foam.GetComponent<Renderer>().sharedMaterial = art.foam;
                foam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                StripColliders(foam);
                foam.AddComponent<LightRevealed>();
            }
        }
    }

    static void Scatter(LevelConfig config, ArtKit art, Stage stage)
    {
        Biome biome = ResolveBiome(config);
        if (biome != null && biome.entries != null && biome.entries.Length > 0)
        {
            config.biome = biome;
            ScatterBiome(config, art, stage);
            return;
        }

        Transform parent = Folder("Props");
        System.Random random = new System.Random(config.seed + 40);
        int pines = Mathf.RoundToInt(config.islandRadius * 0.65f);
        int rocks = Mathf.RoundToInt(config.islandRadius * 0.4f);
        int grasses = Mathf.RoundToInt(config.islandRadius * 2f);
        int mushrooms = Mathf.RoundToInt(config.islandRadius * 0.32f);
        float step = 3.3f;
        float limit = stage.worldSize * 0.5f - 2f;
        for (float x = -limit; x <= limit && (pines > 0 || rocks > 0 || grasses > 0 || mushrooms > 0); x += step)
        {
            for (float z = -limit; z <= limit && (pines > 0 || rocks > 0 || grasses > 0 || mushrooms > 0); z += step)
            {
                float jx = x + ((float)random.NextDouble() - 0.5f) * step;
                float jz = z + ((float)random.NextDouble() - 0.5f) * step;
                float flat = Mathf.Sqrt(jx * jx + jz * jz);
                if (flat < 5.2f || flat > config.islandRadius * 0.72f)
                {
                    continue;
                }

                float y = GroundY(stage.terrain, jx, jz);
                if (y < stage.waterY + 0.55f)
                {
                    continue;
                }

                if (NearSpot(stage.beaconSpots, jx, jz, 3.3f))
                {
                    continue;
                }

                Vector3 pos = new Vector3(jx, y, jz);
                int roll = random.Next(0, 100);
                float slope = Mathf.Abs(GroundY(stage.terrain, jx + 1.4f, jz) - y) / 1.4f;
                if (roll < 22 && pines > 0 && slope < 0.6f)
                {
                    int pick = random.Next(0, 3);
                    GameObject prefab = pick == 0 ? art.pineS : pick == 1 ? art.pineM : art.pineL;
                    PlacePrefab(prefab, parent, pos, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    pines--;
                }
                else if (roll < 40 && rocks > 0)
                {
                    PlacePrefab(random.Next(0, 2) == 0 ? art.rockA : art.rockB, parent, pos, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    rocks--;
                }
                else if (roll < 55 && mushrooms > 0)
                {
                    PlacePrefab(random.Next(0, 3) == 0 ? art.mushroomGlow : art.mushroom, parent, pos, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    mushrooms--;
                }
                else if (grasses > 0)
                {
                    PlacePrefab(art.grassTuft, parent, pos, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                    grasses--;
                }
            }
        }

        DressTrails(config, art, stage);
    }

    static Biome ResolveBiome(LevelConfig config)
    {
        if (config.biome != null && config.biome.entries != null && config.biome.entries.Length > 0)
        {
            return config.biome;
        }

        if (config.levelId == "island1")
        {
            return AssetDatabase.LoadAssetAtPath<Biome>("Assets/Game/Levels/Biomes/PineForest.asset");
        }

        if (config.levelId == "island2")
        {
            return AssetDatabase.LoadAssetAtPath<Biome>("Assets/Game/Levels/Biomes/Namaqualand.asset");
        }

        return null;
    }

    static void ScatterBiome(LevelConfig config, ArtKit art, Stage stage)
    {
        Transform parent = Folder("Props");
        System.Random random = new System.Random(config.seed + 40);
        float scale = config.islandRadius / 28f;
        List<Vector3> trees = new List<Vector3>();
        List<BiomeEntry> treeEntries = Entries(config, BiomeCategory.Tree);
        List<BiomeEntry> saplingEntries = Entries(config, BiomeCategory.Sapling);
        int treeTarget = ScaledTotal(treeEntries, scale);
        int saplingTarget = ScaledTotal(saplingEntries, scale);
        int treesPlaced = PlaceClusters(config, stage, parent, random, treeEntries, treeTarget, 3, 5, 2.4f, 6.2f, 3.1f, trees);
        int saplingsPlaced = PlaceClusters(config, stage, parent, random, saplingEntries, saplingTarget, 2, 4, 1.6f, 4.2f, 2.2f, null);
        int deadwood = PlaceNear(config, stage, parent, random, Entries(config, BiomeCategory.Deadwood), trees, scale, 1.4f, 3.6f);
        int ferns = PlaceNamedNear(config, stage, parent, random, Entries(config, BiomeCategory.Undergrowth), trees, scale, 1.1f, 3.2f, "fern");
        int undergrowth = PlaceScattered(config, stage, parent, random, Entries(config, BiomeCategory.Undergrowth), scale, "fern");
        int rocks = PlaceScattered(config, stage, parent, random, Entries(config, BiomeCategory.Rock), scale, null);
        int pieces = PlaceCliff(config, stage, parent, random);
        PlaceMushrooms(config, art, stage, parent, random);
        Debug.Log(config.sceneName + " biome trees=" + treesPlaced + "/" + treeTarget + " saplings=" + saplingsPlaced + "/" + saplingTarget + " deadwood=" + deadwood + " ferns=" + ferns + " undergrowth=" + undergrowth + " rocks=" + rocks + " setpieces=" + pieces);
    }

    static List<BiomeEntry> Entries(LevelConfig config, BiomeCategory category)
    {
        List<BiomeEntry> list = new List<BiomeEntry>();
        BiomeEntry[] entries = config.biome.entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].prefab != null && entries[i].category == category && entries[i].count > 0)
            {
                list.Add(entries[i]);
            }
        }

        return list;
    }

    static int ScaledTotal(List<BiomeEntry> entries, float scale)
    {
        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            total += Mathf.Max(1, Mathf.RoundToInt(entries[i].count * scale));
        }

        return total;
    }

    static int PlaceClusters(LevelConfig config, Stage stage, Transform parent, System.Random random, List<BiomeEntry> entries, int target, int clusterMin, int clusterMax, float near, float far, float spacing, List<Vector3> planted)
    {
        if (entries.Count == 0 || target <= 0)
        {
            return 0;
        }

        int placed = 0;
        int guard = 0;
        while (placed < target && guard < target * 50)
        {
            guard++;
            Vector3 center;
            if (!SamplePoint(config, stage, random, entries[0], 7f, config.islandRadius * 0.78f, out center))
            {
                continue;
            }

            if (Mathf.PerlinNoise(center.x * 0.07f + config.seed, center.z * 0.07f) < 0.34f)
            {
                continue;
            }

            int cluster = random.Next(clusterMin, clusterMax + 1);
            for (int i = 0; i < cluster && placed < target; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = near + (float)random.NextDouble() * (far - near);
                Vector3 pos = new Vector3(center.x + Mathf.Cos(angle) * radius, 0f, center.z + Mathf.Sin(angle) * radius);
                BiomeEntry entry = entries[random.Next(0, entries.Count)];
                if (!SampleAt(config, stage, entry, pos.x, pos.z, out pos))
                {
                    continue;
                }

                if (planted != null && NearSpot(planted, pos.x, pos.z, spacing))
                {
                    continue;
                }

                PlaceEntry(parent, random, entry, stage, pos);
                if (planted != null)
                {
                    planted.Add(pos);
                }

                placed++;
            }
        }

        return placed;
    }

    static int PlaceNear(LevelConfig config, Stage stage, Transform parent, System.Random random, List<BiomeEntry> entries, List<Vector3> anchors, float scale, float minDist, float maxDist)
    {
        return PlaceNearFiltered(config, stage, parent, random, entries, anchors, scale, minDist, maxDist, null);
    }

    static int PlaceNamedNear(LevelConfig config, Stage stage, Transform parent, System.Random random, List<BiomeEntry> entries, List<Vector3> anchors, float scale, float minDist, float maxDist, string namePart)
    {
        return PlaceNearFiltered(config, stage, parent, random, entries, anchors, scale, minDist, maxDist, namePart);
    }

    static int PlaceNearFiltered(LevelConfig config, Stage stage, Transform parent, System.Random random, List<BiomeEntry> entries, List<Vector3> anchors, float scale, float minDist, float maxDist, string namePart)
    {
        if (anchors == null || anchors.Count == 0)
        {
            return 0;
        }

        int placed = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (namePart != null && entries[i].prefab.name.ToLowerInvariant().IndexOf(namePart.ToLowerInvariant(), System.StringComparison.Ordinal) < 0)
            {
                continue;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(entries[i].count * scale));
            int guard = 0;
            int made = 0;
            while (made < count && guard < count * 20)
            {
                guard++;
                Vector3 anchor = anchors[random.Next(0, anchors.Count)];
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = minDist + (float)random.NextDouble() * (maxDist - minDist);
                Vector3 pos;
                if (!SampleAt(config, stage, entries[i], anchor.x + Mathf.Cos(angle) * radius, anchor.z + Mathf.Sin(angle) * radius, out pos))
                {
                    continue;
                }

                PlaceEntry(parent, random, entries[i], stage, pos);
                made++;
                placed++;
            }
        }

        return placed;
    }

    static int PlaceScattered(LevelConfig config, Stage stage, Transform parent, System.Random random, List<BiomeEntry> entries, float scale, string skipName)
    {
        int placed = 0;
        List<Vector3> spots = new List<Vector3>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (skipName != null && entries[i].prefab.name.ToLowerInvariant().IndexOf(skipName.ToLowerInvariant(), System.StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(entries[i].count * scale));
            int guard = 0;
            int made = 0;
            while (made < count && guard < count * 30)
            {
                guard++;
                Vector3 pos;
                if (!SamplePoint(config, stage, random, entries[i], 6f, config.islandRadius * 0.8f, out pos))
                {
                    continue;
                }

                if (NearSpot(spots, pos.x, pos.z, entries[i].category == BiomeCategory.Rock ? 2.4f : 1.6f))
                {
                    continue;
                }

                PlaceEntry(parent, random, entries[i], stage, pos);
                spots.Add(pos);
                made++;
                placed++;
            }
        }

        return placed;
    }

    static int PlaceCliff(LevelConfig config, Stage stage, Transform parent, System.Random random)
    {
        if (config.levelId != "island2")
        {
            return 0;
        }

        BiomeEntry piece = null;
        BiomeEntry[] entries = config.biome.entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].prefab != null && entries[i].category == BiomeCategory.SetPiece && entries[i].count > 0)
            {
                piece = entries[i];
                break;
            }
        }

        if (piece == null)
        {
            return 0;
        }

        float cliffAngle = (config.seed % 360) * Mathf.Deg2Rad;
        Vector3 pos = Vector3.zero;
        bool found = false;
        for (float dist = 0.66f; dist >= 0.48f && !found; dist -= 0.04f)
        {
            for (int nudge = 0; nudge < 7 && !found; nudge++)
            {
                float angle = cliffAngle + (nudge - 3) * 0.08f;
                float reach = config.islandRadius * dist;
                float x = Mathf.Cos(angle) * reach;
                float z = Mathf.Sin(angle) * reach;
                float y = GroundY(stage.terrain, x, z);
                if (y < stage.waterY + 0.5f)
                {
                    continue;
                }

                if (NearSpot(stage.beaconSpots, x, z, 4f))
                {
                    continue;
                }

                pos = new Vector3(x, y - 0.35f, z);
                found = true;
                cliffAngle = angle;
            }
        }

        if (!found)
        {
            return 0;
        }

        Vector3 outward = new Vector3(Mathf.Cos(cliffAngle), 0f, Mathf.Sin(cliffAngle));
        PlacePrefab(piece.prefab, parent, pos, Quaternion.LookRotation(outward, Vector3.up));
        return 1;
    }

    static void PlaceMushrooms(LevelConfig config, ArtKit art, Stage stage, Transform parent, System.Random random)
    {
        int mushrooms = Mathf.RoundToInt(config.islandRadius * 0.32f);
        int guard = 0;
        while (mushrooms > 0 && guard < 5000)
        {
            guard++;
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float dist = 6.5f + (float)random.NextDouble() * (config.islandRadius * 0.62f);
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            if (Blocked(stage, x, z, 5f, 4f, 1.5f))
            {
                continue;
            }

            float y = GroundY(stage.terrain, x, z);
            if (y < stage.waterY + 0.55f)
            {
                continue;
            }

            GameObject prefab = random.Next(0, 3) == 0 ? art.mushroomGlow : art.mushroom;
            PlacePrefab(prefab, parent, new Vector3(x, y, z), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
            mushrooms--;
        }
    }

    static void PlaceEntry(Transform parent, System.Random random, BiomeEntry entry, Stage stage, Vector3 pos)
    {
        float yaw = (float)random.NextDouble() * 360f;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        if (entry.alignToSlope && stage.terrain != null)
        {
            Vector3 origin = stage.terrain.transform.position;
            Vector3 size = stage.terrain.terrainData.size;
            float u = Mathf.Clamp01((pos.x - origin.x) / size.x);
            float v = Mathf.Clamp01((pos.z - origin.z) / size.z);
            Vector3 normal = stage.terrain.terrainData.GetInterpolatedNormal(u, v);
            Vector3 tilt = Vector3.Slerp(Vector3.up, normal, 0.65f);
            rotation = Quaternion.FromToRotation(Vector3.up, tilt) * Quaternion.Euler(0f, yaw, 0f);
        }

        GameObject placed = PlacePrefab(entry.prefab, parent, pos, rotation);
        float min = entry.scaleRange.x <= 0f ? 1f : entry.scaleRange.x;
        float max = entry.scaleRange.y <= 0f ? min : entry.scaleRange.y;
        float scaleValue = Mathf.Lerp(min, max, (float)random.NextDouble());
        placed.transform.localScale = Vector3.one * scaleValue;
    }

    static bool SamplePoint(LevelConfig config, Stage stage, System.Random random, BiomeEntry entry, float minRadius, float maxRadius, out Vector3 pos)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float dist = minRadius + (float)random.NextDouble() * Mathf.Max(0.5f, maxRadius - minRadius);
        return SampleAt(config, stage, entry, Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, out pos);
    }

    static bool SampleAt(LevelConfig config, Stage stage, BiomeEntry entry, float x, float z, out Vector3 pos)
    {
        pos = Vector3.zero;
        float flat = Mathf.Sqrt(x * x + z * z);
        if (flat > config.islandRadius * 0.84f)
        {
            return false;
        }

        if (Blocked(stage, x, z, 5f, 4f, 1.5f))
        {
            return false;
        }

        float y = GroundY(stage.terrain, x, z);
        if (y < stage.waterY + 0.45f)
        {
            return false;
        }

        float slope = SlopeAt(stage, x, z, y);
        float norm = config.hillHeight > 0.01f ? y / config.hillHeight : 0f;
        if (entry.limitSlope && (slope < entry.minSlope || slope > entry.maxSlope))
        {
            return false;
        }

        if (entry.limitHeight && (norm < entry.minHeight || norm > entry.maxHeight))
        {
            return false;
        }

        pos = new Vector3(x, y, z);
        return true;
    }

    static float SlopeAt(Stage stage, float x, float z, float y)
    {
        float dx = Mathf.Abs(GroundY(stage.terrain, x + 1.3f, z) - y);
        float dz = Mathf.Abs(GroundY(stage.terrain, x, z + 1.3f) - y);
        return Mathf.Max(dx, dz) / 1.3f;
    }

    static bool Blocked(Stage stage, float x, float z, float spawnClear, float beaconClear, float trailClear)
    {
        float dx = x - stage.spawn.x;
        float dz = z - stage.spawn.z;
        if (dx * dx + dz * dz < spawnClear * spawnClear)
        {
            return true;
        }

        if (NearSpot(stage.beaconSpots, x, z, beaconClear))
        {
            return true;
        }

        return NearTrail(stage, x, z, trailClear);
    }

    static bool NearTrail(Stage stage, float x, float z, float radius)
    {
        Vector2 point = new Vector2(x, z);
        for (int i = 0; i < stage.trails.Count; i++)
        {
            List<Vector2> path = stage.trails[i];
            for (int s = 1; s < path.Count; s++)
            {
                float unused;
                if (DistanceToSegment(point, path[s - 1], path[s], out unused) < radius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool NearSpot(List<Vector3> spots, float x, float z, float radius)
    {
        float sqr = radius * radius;
        for (int i = 0; i < spots.Count; i++)
        {
            float dx = spots[i].x - x;
            float dz = spots[i].z - z;
            if (dx * dx + dz * dz < sqr)
            {
                return true;
            }
        }

        return false;
    }

    static void PlaceFireflies(LevelConfig config, ArtKit art, Stage stage)
    {
        Transform parent = Folder("Fireflies");
        int count = Mathf.Max(0, config.fireflyCount);
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.399963f + config.seed * 0.01f;
            float dist = i < 3 ? 7f + i * 1.6f : config.islandRadius * (0.22f + 0.45f * ((i % 7) / 7f));
            Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            float ground = GroundY(stage.terrain, pos.x, pos.z);
            if (ground < stage.waterY + 0.5f)
            {
                dist *= 0.55f;
                pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                ground = GroundY(stage.terrain, pos.x, pos.z);
            }

            pos.y = ground + 1.35f;
            GameObject fly = PlacePrefab(art.firefly, parent, pos, Quaternion.identity);
            Firefly script = fly.GetComponent<Firefly>();
            if (script != null)
            {
                SerializedObject so = new SerializedObject(script);
                so.FindProperty("islandRadius").floatValue = config.islandRadius * 0.78f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            stage.fireflies++;
        }
    }

    static void SpawnKeeper(ArtKit art, Stage stage, bool gameplay)
    {
        SpawnKeeper(art, stage, gameplay, stage.spawn);
    }

    static void SpawnKeeper(ArtKit art, Stage stage, bool gameplay, Vector3 position)
    {
        GameObject keeper = (GameObject)PrefabUtility.InstantiatePrefab(art.keeper);
        keeper.name = gameplay ? "Keeper" : "Statue";
        keeper.transform.position = position;
        if (!gameplay)
        {
            PlayerController walker = keeper.GetComponent<PlayerController>();
            if (walker != null)
            {
                walker.enabled = false;
            }

            CharacterController body = keeper.GetComponent<CharacterController>();
            if (body != null)
            {
                body.enabled = false;
            }

            return;
        }

        keeper.tag = "Player";
        CharacterController controller = keeper.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = keeper.AddComponent<CharacterController>();
        }

        controller.height = 1.7f;
        controller.radius = 0.28f;
        controller.center = new Vector3(0f, 0.88f, 0f);
        controller.stepOffset = 0.4f;
        controller.slopeLimit = 52f;
        if (keeper.GetComponent<PlayerController>() == null)
        {
            keeper.AddComponent<PlayerController>();
        }

        if (keeper.GetComponent<KeeperAnimator>() == null)
        {
            keeper.AddComponent<KeeperAnimator>();
        }
        Transform pivot = FindDeep(keeper.transform, "LanternPivot");
        Transform lanternTransform = pivot != null ? pivot.Find("Lantern") : null;
        if (pivot != null && pivot.GetComponent<LanternSway>() == null)
        {
            LanternSway sway = pivot.gameObject.AddComponent<LanternSway>();
            SerializedObject swayObject = new SerializedObject(sway);
            swayObject.FindProperty("trackedBody").objectReferenceValue = keeper.transform;
            swayObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (lanternTransform != null)
        {
            Light light = lanternTransform.GetComponentInChildren<Light>();
            Lantern lantern = lanternTransform.GetComponent<Lantern>();
            if (lantern == null)
            {
                lantern = lanternTransform.gameObject.AddComponent<Lantern>();
            }
            SerializedObject lanternObject = new SerializedObject(lantern);
            lanternObject.FindProperty("lanternLight").objectReferenceValue = light;
            lanternObject.ApplyModifiedPropertiesWithoutUndo();
            LanternFlicker flicker = lanternTransform.GetComponent<LanternFlicker>();
            if (flicker == null)
            {
                flicker = lanternTransform.gameObject.AddComponent<LanternFlicker>();
            }
            SerializedObject flickerObject = new SerializedObject(flicker);
            flickerObject.FindProperty("targetLight").objectReferenceValue = light;
            flickerObject.FindProperty("lantern").objectReferenceValue = lantern;
            flickerObject.ApplyModifiedPropertiesWithoutUndo();
            stage.lantern = lantern;
        }

        stage.player = keeper;
        Camera view = Camera.main;
        if (view == null)
        {
            view = Object.FindAnyObjectByType<Camera>();
        }

        CameraFollow follow = view != null ? view.GetComponent<CameraFollow>() : null;
        if (follow == null && view != null)
        {
            follow = view.gameObject.AddComponent<CameraFollow>();
        }

        if (follow != null)
        {
            SerializedObject followObject = new SerializedObject(follow);
            followObject.FindProperty("target").objectReferenceValue = keeper.transform;
            followObject.FindProperty("offset").vector3Value = new Vector3(0f, 4f, -6f);
            followObject.FindProperty("lookHeight").floatValue = 1.25f;
            followObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void CreateGameplay(LevelConfig config, ArtKit art, Stage stage)
    {
        GameObject systems = new GameObject("Systems");
        GameManager manager = systems.AddComponent<GameManager>();
        systems.AddComponent<AudioManager>();
        DawnSequence dawn = systems.AddComponent<DawnSequence>();
        LowFuelFX fx = systems.AddComponent<LowFuelFX>();
        MothSpawner spawner = systems.AddComponent<MothSpawner>();

        SerializedObject managerObject = new SerializedObject(manager);
        managerObject.FindProperty("beaconsToWin").intValue = Mathf.Max(1, stage.beacons);
        managerObject.FindProperty("lantern").objectReferenceValue = stage.lantern;
        managerObject.FindProperty("levelId").stringValue = config.levelId;
        managerObject.FindProperty("nextLevelScene").stringValue = config.nextLevelScene == null ? "" : config.nextLevelScene;
        managerObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject dawnObject = new SerializedObject(dawn);
        dawnObject.FindProperty("sun").objectReferenceValue = stage.sun;
        dawnObject.FindProperty("stars").objectReferenceValue = stage.stars;
        dawnObject.FindProperty("duration").floatValue = 6f;
        if (art.dawnDirection.sqrMagnitude > 0.25f)
        {
            dawnObject.FindProperty("dawnEuler").vector3Value = LightEulerFromSkyDirection(art.dawnDirection);
        }

        dawnObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject fxObject = new SerializedObject(fx);
        fxObject.FindProperty("volume").objectReferenceValue = stage.volume;
        fxObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject mothObject = new SerializedObject(spawner);
        mothObject.FindProperty("baseCount").intValue = config.mothCount;
        mothObject.FindProperty("mothPrefab").objectReferenceValue = art.moth;
        mothObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateCameraMenu(Stage stage)
    {
        if (Camera.main == null)
        {
            return;
        }

        MenuOrbit orbit = Camera.main.gameObject.AddComponent<MenuOrbit>();
        GameObject focus = new GameObject("IslandFocus");
        focus.transform.position = new Vector3(0f, stage.spawn.y, 0f);
        orbit.Configure(focus.transform, 24f, 10f);
    }

    static void CreateMenu(ArtKit art, Stage stage)
    {
        new GameObject("Systems").AddComponent<AudioManager>();
        CreateCameraMenu(stage);
        EnsureEventSystem();
        Font font = BuiltinFont();
        GameObject canvasObject = MakeCanvas("MenuCanvas");
        canvasObject.AddComponent<MainMenu>();
        RectTransform panel = MakeRect(canvasObject.transform, "Panel", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(520f, 860f));
        panel.pivot = new Vector2(0f, 0.5f);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = art.uiSprite;
        panelImage.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);
        MakeText(panel, "Title", "Lantern Keeper", 58, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 80f), new Color(1f, 0.84f, 0.45f), font, TextAnchor.MiddleCenter);
        MakeText(panel, "Subtitle", "Light the beacons before the flame dies.", 20, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(460f, 40f), new Color(0.8f, 0.86f, 0.9f), font, TextAnchor.MiddleCenter);
        MakeButton(art, panel, "PlayButton", "Play", new Vector2(0f, 150f));
        MakeButton(art, panel, "DifficultyButton", "Difficulty: Normal", new Vector2(0f, 80f));
        MakeButton(art, panel, "MusicButton", "Music: On", new Vector2(0f, 10f));
        MakeButton(art, panel, "Island1Button", "Island 1", new Vector2(0f, -70f));
        MakeText(panel, "BestIsland1", "Best --:--", 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -112f), new Vector2(300f, 28f), new Color(0.75f, 0.8f, 0.84f), font, TextAnchor.MiddleCenter);
        MakeButton(art, panel, "Island2Button", "Island 2", new Vector2(0f, -180f));
        MakeText(panel, "BestIsland2", "Best --:--", 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -222f), new Vector2(300f, 28f), new Color(0.75f, 0.8f, 0.84f), font, TextAnchor.MiddleCenter);
        MakeButton(art, panel, "QuitButton", "Quit", new Vector2(0f, -300f));
    }

    static void CreateHud(ArtKit art, Stage stage)
    {
        EnsureEventSystem();
        Font font = BuiltinFont();
        GameObject canvasObject = MakeCanvas("HUD");
        HUD hud = canvasObject.AddComponent<HUD>();
        canvasObject.AddComponent<BeaconCompass>();

        RectTransform fuel = MakeRect(canvasObject.transform, "FuelMeter", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(64f, 112f));
        fuel.pivot = new Vector2(0f, 1f);
        Image frame = fuel.gameObject.AddComponent<Image>();
        frame.sprite = art.lanternSprite != null ? art.lanternSprite : art.uiSprite;
        frame.color = new Color(0.45f, 0.32f, 0.16f, 0.95f);
        fuel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        RectTransform fill = MakeRect(fuel, "FuelFill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fill.offsetMin = new Vector2(8f, 10f);
        fill.offsetMax = new Vector2(-8f, -16f);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = art.uiSprite;
        fillImage.color = new Color(1f, 0.62f, 0.22f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Vertical;
        fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImage.fillAmount = 1f;

        RectTransform dots = MakeRect(canvasObject.transform, "BeaconDots", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -48f), new Vector2(240f, 28f));
        dots.pivot = new Vector2(0f, 1f);
        HorizontalLayoutGroup layout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        Text timer = MakeText(canvasObject.transform, "TimerText", "00:00", 32, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(220f, 48f), new Color(0.9f, 0.93f, 0.95f), font, TextAnchor.MiddleRight);
        timer.rectTransform.pivot = new Vector2(1f, 1f);
        Text prompt = MakeText(canvasObject.transform, "PromptText", "Press E", 28, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(240f, 40f), new Color(1f, 0.9f, 0.7f), font, TextAnchor.MiddleCenter);
        prompt.gameObject.SetActive(false);

        RectTransform compass = MakeRect(canvasObject.transform, "Compass", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(90f, 88f));
        compass.pivot = new Vector2(0.5f, 1f);
        RectTransform arrow = MakeRect(compass, "Arrow", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(36f, 36f));
        arrow.pivot = new Vector2(0.5f, 1f);
        Image arrowImage = arrow.gameObject.AddComponent<Image>();
        arrowImage.sprite = art.arrowSprite != null ? art.arrowSprite : art.uiSprite;
        arrowImage.color = new Color(1f, 0.82f, 0.4f, 1f);
        MakeText(compass, "Distance", "0m", 18, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(80f, 24f), Color.white, font, TextAnchor.MiddleCenter);

        GameObject win = MakePanel(art, canvasObject.transform, "WinPanel", "The island wakes", font);
        MakeText(win.transform, "WinDetail", "Time 00:00", 24, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(460f, 80f), Color.white, font, TextAnchor.MiddleCenter);
        MakeButton(art, win.transform, "RetryButton", "Retry", new Vector2(-220f, -140f), new Vector2(200f, 52f));
        MakeButton(art, win.transform, "NextButton", "Next Island", new Vector2(0f, -140f), new Vector2(220f, 52f));
        MakeButton(art, win.transform, "MenuButton", "Menu", new Vector2(220f, -140f), new Vector2(200f, 52f));
        win.SetActive(false);

        GameObject lose = MakePanel(art, canvasObject.transform, "LosePanel", "The flame went out", font);
        MakeText(lose.transform, "LoseDetail", "The lantern went out.", 24, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(460f, 80f), Color.white, font, TextAnchor.MiddleCenter);
        MakeButton(art, lose.transform, "RetryButton", "Retry", new Vector2(-220f, -140f), new Vector2(200f, 52f));
        MakeButton(art, lose.transform, "NextButton", "Next Island", new Vector2(0f, -140f), new Vector2(220f, 52f));
        MakeButton(art, lose.transform, "MenuButton", "Menu", new Vector2(220f, -140f), new Vector2(200f, 52f));
        lose.SetActive(false);

        BeaconCompass compassScript = canvasObject.GetComponent<BeaconCompass>();
        SerializedObject compassObject = new SerializedObject(compassScript);
        compassObject.FindProperty("arrow").objectReferenceValue = arrow;
        compassObject.FindProperty("distanceText").objectReferenceValue = compass.Find("Distance").GetComponent<Text>();
        compassObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject hudObject = new SerializedObject(hud);
        hudObject.FindProperty("fuelFill").objectReferenceValue = fillImage;
        hudObject.FindProperty("fuelMeter").objectReferenceValue = fuel;
        hudObject.FindProperty("beaconDots").objectReferenceValue = dots;
        hudObject.FindProperty("promptText").objectReferenceValue = prompt;
        hudObject.FindProperty("timerText").objectReferenceValue = canvasObject.transform.Find("TimerText").GetComponent<Text>();
        hudObject.FindProperty("winPanel").objectReferenceValue = win;
        hudObject.FindProperty("losePanel").objectReferenceValue = lose;
        hudObject.FindProperty("winDetailText").objectReferenceValue = win.transform.Find("WinDetail").GetComponent<Text>();
        hudObject.FindProperty("loseDetailText").objectReferenceValue = lose.transform.Find("LoseDetail").GetComponent<Text>();
        hudObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject MakePanel(ArtKit art, Transform parent, string name, string title, Font font)
    {
        RectTransform rect = MakeRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 420f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = art.uiSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        MakeText(rect, "Title", title, 36, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(500f, 56f), new Color(1f, 0.82f, 0.45f), font, TextAnchor.MiddleCenter);
        return rect.gameObject;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject system = new GameObject("EventSystem");
        system.AddComponent<EventSystem>();
        system.AddComponent<InputSystemUIInputModule>();
    }

    static GameObject MakeCanvas(string name)
    {
        GameObject canvasObject = new GameObject(name);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }

    static RectTransform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static Text MakeText(Transform parent, string name, string value, int size, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 box, Color color, Font font, TextAnchor anchor)
    {
        RectTransform rect = MakeRect(parent, name, anchorMin, anchorMax, position, box);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Button MakeButton(ArtKit art, Transform parent, string name, string label, Vector2 position)
    {
        return MakeButton(art, parent, name, label, position, new Vector2(280f, 48f));
    }

    static Button MakeButton(ArtKit art, Transform parent, string name, string label, Vector2 position, Vector2 size)
    {
        RectTransform rect = MakeRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = art.uiSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.14f, 0.16f, 0.2f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.55f, 0.25f, 1f);
        colors.pressedColor = new Color(1f, 0.7f, 0.3f, 1f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.6f);
        button.colors = colors;
        MakeText(rect, "Label", label, 22, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.92f, 0.78f), BuiltinFont(), TextAnchor.MiddleCenter);
        RectTransform labelRect = rect.Find("Label") as RectTransform;
        if (labelRect != null)
        {
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        return button;
    }

    static Font BuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static Transform Folder(string name)
    {
        GameObject folder = new GameObject(name);
        return folder.transform;
    }

    static GameObject PlacePrefab(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = rotation;
        return go;
    }

    static LevelConfig WriteConfig(string path, string levelId, string displayName, string sceneName, float radius, float hill, int seed, int beacons, int fireflies, int moths, int paths, Color fog, float density, string nextScene)
    {
        LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<LevelConfig>();
            AssetDatabase.CreateAsset(config, path);
        }

        config.levelId = levelId;
        config.displayName = displayName;
        config.sceneName = sceneName;
        config.islandRadius = radius;
        config.hillHeight = hill;
        config.seed = seed;
        config.beaconCount = beacons;
        config.fireflyCount = fireflies;
        config.mothCount = moths;
        config.hiddenPathCount = paths;
        config.fogColor = fog;
        config.fogDensity = density;
        config.grassDetailDensity = 8;
        config.nextLevelScene = nextScene;
        EditorUtility.SetDirty(config);
        return config;
    }
}
}
