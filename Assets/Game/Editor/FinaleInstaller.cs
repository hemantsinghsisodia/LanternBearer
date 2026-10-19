using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Installs the Island 5 finale cinematic on the built scene (BuildIsland5 runs it after the performance pass, before the audio).
//  - Bakes detailed miniatures of Islands 1-4 (FinaleInstaller.Islands.cs): a 128x128 coloured terrain (and 64x64 for Low), merged low-poly
//    trees and rocks, and the real beacon model, from each island's TerrainData and, read only, its scene (opened additively, closed unsaved).
//    Assets/Game/Art/Finale/Finale_Island{1..4}_{Terrain,TerrainLow,Props,Base,Glass,Beams,Flame}.asset. The meshes are rewritten only if they
//    differ, so a second run is byte-identical. The old 48x48 Silhouette_Island*.asset files are removed.
//  - Bakes the storm ring meshes (full and Low) and the cloud noise texture, and the materials for the ring, the islands and the glow.
//  - Builds the "Finale" object in Island5: FinaleSequence + FinaleCamera, StormRing, FinaleIslands (the four islands, each with terrain, props,
//    beacon bases, glass, beams, flame cards, a glow sprite and a column) and the ember burst; wires GameManager.finale.
// Idempotent: the Finale object is rebuilt each run. The scene is edited through EditorSceneManager, never as text.
public static partial class FinaleInstaller
{
    public const string Folder = "Assets/Game/Art/Finale";
    public const string RootName = "Finale";
    const string CloudTexturePath = Folder + "/StormCloud.png";
    const string StormMaterialPath = Folder + "/StormRing.mat";
    const string SilhouetteMaterialPath = Folder + "/FinaleSilhouette.mat";
    const string IslandMaterialPath = Folder + "/FinaleIsland.mat";
    const string GlowMaterialPath = Folder + "/FinaleGlow.mat";
    const string StormFullPath = Folder + "/StormRing_Full.asset";
    const string StormLowPath = Folder + "/StormRing_Low.asset";
    const string GlowQuadPath = Folder + "/FinaleGlowQuad.asset";
    const string SunPathMeshPath = Folder + "/FinaleSunPath.asset";
    const string SunMaterialPath = Folder + "/FinaleSun.mat";
    const string ColumnMeshPath = Folder + "/FinaleColumn.asset";
    const string ColumnMaterialPath = Folder + "/FinaleColumn.mat";
    public const float ColumnAlpha = 0.9f;
    const string SoftTexturePath = "Assets/Game/Art/Look/Mist/MistSoft.png";
    // IslandBuilder.WaterFraction: the sea stands at 20% of the terrain height.
    const float WaterFraction = 0.2f;
    const float SkirtFloor = -2f;
    // The miniature islands at night are dark shapes (lit by their own beacon flare and the moon rim); they warm up to the dawn values at the sunrise.
    public const float IslandNightAmbient = 0.08f, IslandNightWrap = 0.3f, IslandDawnAmbient = 0.8f, IslandDawnWrap = 0.75f, IslandNightDim = 0.5f;
    // Continuous cloud ribbons round the island: 4 layers on the full level, 2 on Low (half the cards, half the overdraw).
    const int StormSegments = 48;
    const int StormLayersFull = 4;
    const int StormLayersLow = 2;

    [MenuItem("Lantern Keeper/Install Finale (Island 5)")]
    public static void InstallMenu()
    {
        Install();
    }


    public static string TerrainPath(int island)
    {
        return "Assets/Game/Levels/Island" + island + "_Terrain.asset";
    }

    public static void Install()
    {
        Scene scene = OpenScene();
        if (Rebuild(scene))
        {
            InstallCredits();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FinaleInstaller: installed the finale in " + scene.name + ".");
        }
    }

    // The credits that follow the finale: Resources/Credits.txt (a copy of CREDITS.txt) and the overlay on the Island 5 HUD canvas.
    static void InstallCredits()
    {
        CreditsSync.Sync();
        HUD hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            Debug.LogError("FinaleInstaller: Island 5 has no HUD for the credits roll.");
            return;
        }

        UIBuilder.InstallCredits(hud);
    }

    // Rebuilds the finale in an open Island 5 scene without saving it (the assets are written). False, and the scene untouched, if something is missing.
    public static bool Rebuild(Scene scene)
    {
        GameObject lampObject = null;
        GameObject water = null;
        GameManager manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == LighthouseInstaller.LampName) lampObject = root;
            else if (root.name == "Water") water = root;
            else if (root.name == "Systems") manager = root.GetComponent<GameManager>();
        }

        GreatLamp lamp = lampObject != null ? lampObject.GetComponent<GreatLamp>() : null;
        GreatLampBeam beam = lampObject != null ? lampObject.GetComponentInChildren<GreatLampBeam>(true) : null;
        Transform flame = lampObject != null ? lampObject.transform.Find("FlameRoot") : null;
        DawnSequence dawn = manager != null ? manager.GetComponentInChildren<DawnSequence>(true) : null;
        if (lamp == null || beam == null || flame == null || water == null || manager == null || dawn == null)
        {
            Debug.LogError("FinaleInstaller: Island 5 lacks the lighthouse (GreatLamp, FlameRoot, LampBeam), Water, or Systems/DawnSequence. Run the earlier post steps first.");
            return false;
        }

        Shader stormShader = Shader.Find("LanternKeeper/StormRing");
        Shader islandShader = Shader.Find("LanternKeeper/FinaleIsland");
        Shader additive = Shader.Find("LanternKeeper/AdditiveUnlit");
        Material beamMaterial = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconBeamMaterialPath);
        Material emberMaterial = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconEmberMaterialPath);
        Material glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconGlassMaterialPath);
        Texture2D soft = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftTexturePath);
        if (stormShader == null || islandShader == null || additive == null || beamMaterial == null || emberMaterial == null || glassMaterial == null || soft == null)
        {
            Debug.LogError("FinaleInstaller: a shader, material (beacon glass, beam or ember) or MistSoft is missing.");
            return false;
        }

        EnsureFolder(Folder);

        // ---- assets ----
        Texture2D cloud = EnsureCloudTexture();
        Material stormMaterial = EnsureMaterial(StormMaterialPath, stormShader, "StormRing");
        stormMaterial.SetTexture("_CloudTex", cloud);
        stormMaterial.SetColor("_DarkColor", new Color(0.035f, 0.04f, 0.075f, 1f));
        stormMaterial.SetColor("_LitColor", new Color(0.55f, 0.62f, 1f, 1f));
        stormMaterial.SetColor("_WarmColor", new Color(1f, 0.62f, 0.4f, 1f));
        stormMaterial.SetFloat("_Alpha", 0.85f);
        EditorUtility.SetDirty(stormMaterial);

        Material islandMaterial = EnsureMaterial(IslandMaterialPath, islandShader, "FinaleIsland");
        islandMaterial.SetColor("_FarColor", new Color(0.07f, 0.075f, 0.11f, 1f));
        islandMaterial.SetColor("_RimColor", new Color(1f, 0.72f, 0.42f, 1f));
        islandMaterial.SetColor("_FlareColor", new Color(1f, 0.62f, 0.28f, 1f));
        islandMaterial.SetFloat("_RimStrength", 1f);
        islandMaterial.SetFloat("_Ambient", IslandNightAmbient);
        islandMaterial.SetFloat("_DawnAmbient", IslandDawnAmbient);
        islandMaterial.SetFloat("_Wrap", IslandNightWrap);
        islandMaterial.SetFloat("_DawnWrap", IslandDawnWrap);
        islandMaterial.SetFloat("_NightDim", IslandNightDim);
        islandMaterial.SetFloat("_Fade", 1f);
        EditorUtility.SetDirty(islandMaterial);

        Material glowMaterial = EnsureMaterial(GlowMaterialPath, additive, "FinaleGlow");
        glowMaterial.SetTexture("_BaseMap", soft);
        glowMaterial.SetColor("_BaseColor", new Color(1f, 0.694f, 0.361f, 0.8f));
        EditorUtility.SetDirty(glowMaterial);

        Mesh stormFull = EnsureMesh(StormFullPath, "StormRing_Full", BuildStormMesh(StormLayersFull, 5505));
        Mesh stormLow = EnsureMesh(StormLowPath, "StormRing_Low", BuildStormMesh(StormLayersLow, 5505));
        Material columnMaterial = EnsureMaterial(ColumnMaterialPath, additive, "FinaleColumn");
        columnMaterial.SetTexture("_BaseMap", soft);
        // Over the pale sunrise sky pure additive amber washes out to a faint pink-white, while over the dark sea it reads as strong amber: the shaft looks
        // cut at the horizon. Premultiplied alpha (_AlphaMix 1) tints both backgrounds the same amber, so it stays one shaft. Everything else that uses
        // AdditiveUnlit keeps _AlphaMix 0 (purely additive).
        columnMaterial.SetColor("_BaseColor", new Color(1f, 0.78f, 0.45f, ColumnAlpha));
        columnMaterial.SetFloat("_AlphaMix", 1f);
        EditorUtility.SetDirty(columnMaterial);
        Mesh columnMesh = EnsureMesh(ColumnMeshPath, "FinaleColumn", BuildColumn());
        Material sunMaterial = EnsureMaterial(SunMaterialPath, additive, "FinaleSun");
        sunMaterial.SetTexture("_BaseMap", soft);
        sunMaterial.SetColor("_BaseColor", new Color(1f, 0.74f, 0.38f, 0.9f));
        EditorUtility.SetDirty(sunMaterial);
        Mesh sunPathMesh = EnsureMesh(SunPathMeshPath, "FinaleSunPath", BuildSunPath());
        Mesh glowQuad = EnsureMesh(GlowQuadPath, "FinaleGlowQuad", BuildQuad());

        IslandBake[] bakes = new IslandBake[4];
        for (int i = 0; i < 4; i++)
        {
            if (!BakeIsland(i + 1, out bakes[i]))
            {
                return false;
            }

            // Saved as assets (rewritten only when the geometry changed).
            bakes[i].terrain = EnsureMesh(IslandMeshPath(i + 1, "Terrain"), bakes[i].terrain.name, bakes[i].terrain);
            bakes[i].terrainLow = EnsureMesh(IslandMeshPath(i + 1, "TerrainLow"), bakes[i].terrainLow.name, bakes[i].terrainLow);
            bakes[i].props = EnsureMesh(IslandMeshPath(i + 1, "Props"), bakes[i].props.name, bakes[i].props);
            bakes[i].beaconBase = EnsureMesh(IslandMeshPath(i + 1, "Base"), bakes[i].beaconBase.name, bakes[i].beaconBase);
            bakes[i].glass = EnsureMesh(IslandMeshPath(i + 1, "Glass"), bakes[i].glass.name, bakes[i].glass);
            bakes[i].beams = EnsureMesh(IslandMeshPath(i + 1, "Beams"), bakes[i].beams.name, bakes[i].beams);
            bakes[i].flame = EnsureMesh(IslandMeshPath(i + 1, "Flame"), bakes[i].flame.name, bakes[i].flame);
        }

        RemoveOldSilhouettes();

        // ---- scene ----
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName)
            {
                Object.DestroyImmediate(root);
            }
        }

        float seaY = water.transform.position.y;
        GameObject finaleObject = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(finaleObject, scene);
        FinaleSequence sequence = finaleObject.AddComponent<FinaleSequence>();
        FinaleCamera finaleCamera = finaleObject.AddComponent<FinaleCamera>();
        SerializedObject cameraData = new SerializedObject(finaleCamera);
        cameraData.FindProperty("lamp").objectReferenceValue = lampObject.transform;
        cameraData.FindProperty("flame").objectReferenceValue = flame;
        cameraData.FindProperty("seaY").floatValue = seaY;
        cameraData.ApplyModifiedPropertiesWithoutUndo();

        // The storm wall.
        GameObject stormObject = new GameObject("StormRing");
        stormObject.transform.SetParent(finaleObject.transform, false);
        stormObject.transform.position = new Vector3(0f, seaY, 0f);
        MeshFilter stormFilter = stormObject.AddComponent<MeshFilter>();
        stormFilter.sharedMesh = stormFull;
        MeshRenderer stormRenderer = stormObject.AddComponent<MeshRenderer>();
        ConfigureRenderer(stormRenderer, stormMaterial);
        StormRing storm = stormObject.AddComponent<StormRing>();
        SerializedObject stormData = new SerializedObject(storm);
        stormData.FindProperty("meshFilter").objectReferenceValue = stormFilter;
        stormData.FindProperty("ringRenderer").objectReferenceValue = stormRenderer;
        stormData.FindProperty("meshFull").objectReferenceValue = stormFull;
        stormData.FindProperty("meshLow").objectReferenceValue = stormLow;
        stormData.FindProperty("cardsFull").intValue = StormLayersFull * StormSegments;
        stormData.FindProperty("cardsLow").intValue = StormLayersLow * StormSegments;
        stormData.ApplyModifiedPropertiesWithoutUndo();

        // The dramatic clouds (three banks and the god rays); finale only.
        FinaleClouds clouds = BuildClouds(finaleObject.transform, seaY, cloud);
        if (clouds == null)
        {
            return false;
        }

        // The distant islands.
        GameObject islandsObject = new GameObject("Islands");
        islandsObject.transform.SetParent(finaleObject.transform, false);
        islandsObject.transform.position = new Vector3(0f, seaY, 0f);
        FinaleIslands islands = islandsObject.AddComponent<FinaleIslands>();
        SerializedObject islandData = new SerializedObject(islands);
        SerializedProperty entries = islandData.FindProperty("entries");
        entries.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            IslandBake bake = bakes[i];
            GameObject island = new GameObject("Island" + (i + 1));
            island.transform.SetParent(islandsObject.transform, false);
            Vector2 xz = FinaleLayout.SilhouetteXZ(i);
            island.transform.localPosition = new Vector3(xz.x, 0f, xz.y);
            island.transform.localScale = Vector3.one * FinaleLayout.SilhouetteScale(i);

            MeshRenderer terrainRenderer = AddPart(island.transform, "MiniTerrain", bake.terrain, islandMaterial);
            MeshRenderer propsRenderer = AddPart(island.transform, "MiniProps", bake.props, islandMaterial);
            MeshRenderer baseRenderer = AddPart(island.transform, "MiniBeaconBases", bake.beaconBase, islandMaterial);
            MeshRenderer glassRenderer = AddPart(island.transform, "MiniBeaconGlass", bake.glass, glassMaterial);
            MeshRenderer beamsRenderer = AddPart(island.transform, "MiniBeaconBeams", bake.beams, beamMaterial);
            MeshRenderer flameRenderer = AddPart(island.transform, "MiniBeaconFlames", bake.flame, glowMaterial);

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("root").objectReferenceValue = island.transform;
            entry.FindPropertyRelative("silhouette").objectReferenceValue = terrainRenderer;
            entry.FindPropertyRelative("terrainFull").objectReferenceValue = bake.terrain;
            entry.FindPropertyRelative("terrainLow").objectReferenceValue = bake.terrainLow;
            entry.FindPropertyRelative("props").objectReferenceValue = propsRenderer;
            entry.FindPropertyRelative("beaconBase").objectReferenceValue = baseRenderer;
            entry.FindPropertyRelative("glass").objectReferenceValue = glassRenderer;
            entry.FindPropertyRelative("flame").objectReferenceValue = flameRenderer;
            entry.FindPropertyRelative("flareLocal").vector3Value = bake.flareLocal;
            SerializedProperty beams = entry.FindPropertyRelative("beams");
            beams.arraySize = 1;
            beams.GetArrayElementAtIndex(0).objectReferenceValue = beamsRenderer;

            // The glow sprite and the tall column stand on the highest beacon.
            Vector3 top = bake.flareLocal;
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(island.transform, false);
            glow.transform.localPosition = top + Vector3.up * 3.5f;
            glow.AddComponent<MeshFilter>().sharedMesh = glowQuad;
            MeshRenderer glowRenderer = glow.AddComponent<MeshRenderer>();
            ConfigureRenderer(glowRenderer, glowMaterial);
            GameObject column = new GameObject("Column");
            column.transform.SetParent(island.transform, false);
            column.transform.localPosition = top;
            column.transform.localScale = new Vector3(1f, 0.01f, 1f);
            column.AddComponent<MeshFilter>().sharedMesh = columnMesh;
            MeshRenderer columnRenderer = column.AddComponent<MeshRenderer>();
            ConfigureRenderer(columnRenderer, columnMaterial);
            entry.FindPropertyRelative("column").objectReferenceValue = column.transform;
            entry.FindPropertyRelative("columnRenderer").objectReferenceValue = columnRenderer;
            entry.FindPropertyRelative("glow").objectReferenceValue = glow.transform;
            entry.FindPropertyRelative("glowRenderer").objectReferenceValue = glowRenderer;
        }

        // The sunrise: a glow where the sun comes up and a path of light on the sea towards it.
        float sunBearing = FinaleCamera.SunBearing * Mathf.Deg2Rad;
        Vector3 sunDirection = new Vector3(Mathf.Sin(sunBearing), 0f, Mathf.Cos(sunBearing));
        GameObject sunGlow = new GameObject("SunGlow");
        sunGlow.transform.SetParent(islandsObject.transform, false);
        sunGlow.transform.localPosition = sunDirection * 560f + Vector3.up * 28f;
        sunGlow.transform.localScale = Vector3.one * 480f;
        sunGlow.AddComponent<MeshFilter>().sharedMesh = glowQuad;
        MeshRenderer sunGlowRenderer = sunGlow.AddComponent<MeshRenderer>();
        ConfigureRenderer(sunGlowRenderer, sunMaterial);
        GameObject sunPath = new GameObject("SunPath");
        sunPath.transform.SetParent(islandsObject.transform, false);
        sunPath.transform.localPosition = sunDirection * 330f + Vector3.up * 0.4f;
        sunPath.transform.localRotation = Quaternion.LookRotation(sunDirection, Vector3.up);
        sunPath.transform.localScale = new Vector3(70f, 1f, 520f);
        sunPath.AddComponent<MeshFilter>().sharedMesh = sunPathMesh;
        MeshRenderer sunPathRenderer = sunPath.AddComponent<MeshRenderer>();
        ConfigureRenderer(sunPathRenderer, sunMaterial);
        islandData.FindProperty("sunGlow").objectReferenceValue = sunGlow.transform;
        islandData.FindProperty("sunGlowRenderer").objectReferenceValue = sunGlowRenderer;
        islandData.FindProperty("sunPathRenderer").objectReferenceValue = sunPathRenderer;
        islandData.ApplyModifiedPropertiesWithoutUndo();

        // A soft amber halo at the lantern room (switched on by the sequence), so the lit lamp reads at the finale camera distances.
        GameObject haloObject = new GameObject("LampHalo");
        haloObject.transform.SetParent(finaleObject.transform, false);
        haloObject.transform.position = flame.position;
        haloObject.AddComponent<MeshFilter>().sharedMesh = glowQuad;
        ConfigureRenderer(haloObject.AddComponent<MeshRenderer>(), glowMaterial);
        haloObject.SetActive(false);

        // The ember burst at the flame.
        ParticleSystem embers = CreateEmbers(finaleObject.transform, emberMaterial, flame.position);

        SerializedObject sequenceData = new SerializedObject(sequence);
        sequenceData.FindProperty("lamp").objectReferenceValue = lamp;
        sequenceData.FindProperty("beam").objectReferenceValue = beam;
        sequenceData.FindProperty("finaleCamera").objectReferenceValue = finaleCamera;
        sequenceData.FindProperty("storm").objectReferenceValue = storm;
        sequenceData.FindProperty("clouds").objectReferenceValue = clouds;
        sequenceData.FindProperty("halo").objectReferenceValue = haloObject.transform;
        sequenceData.FindProperty("islands").objectReferenceValue = islands;
        sequenceData.FindProperty("dawn").objectReferenceValue = dawn;
        sequenceData.FindProperty("embers").objectReferenceValue = embers;
        sequenceData.FindProperty("finaleGrade").objectReferenceValue = BuildGradeVolume(finaleObject.transform);
        sequenceData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject managerData = new SerializedObject(manager);
        managerData.FindProperty("finale").objectReferenceValue = sequence;
        managerData.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // A child of an island with a mesh and a material (no shadows, probes or motion vectors).
    static MeshRenderer AddPart(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        ConfigureRenderer(renderer, material);
        return renderer;
    }

    static void ConfigureRenderer(MeshRenderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
    }

    // 40 additive sparks from the flame, simulated on unscaled time (the finale runs at timeScale 0 too).
    static ParticleSystem CreateEmbers(Transform parent, Material material, Vector3 position)
    {
        GameObject go = new GameObject("FinaleEmbers");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(LookPalette.FromHex(LookPalette.LanternAmber), LookPalette.FromHex(LookPalette.GlowCore));
        main.gravityModifier = -0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.8f;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 1.2f;
        noise.frequency = 0.6f;

        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    // ---- storm ring meshes ----

    // `layers` continuous ribbons round the island at about StormRing.Radius (each StormSegments quads, no gaps), from StormRing.MinHeight up to
    // StormRing.MaxHeight above the object (the sea). Layer 0 is the lowest and densest; the higher layers reach further up and are wispier
    // (the shader shapes that from uv.y). uv0.x runs round the circle 12 times (the noise tiles), uv0.y is 0 at the base and 1 at the top;
    // uv1.x is a per-layer random.
    static Mesh BuildStormMesh(int layers, int seed)
    {
        System.Random rng = new System.Random(seed);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int layer = 0; layer < layers; layer++)
        {
            float k = layers > 1 ? (float)layer / (layers - 1) : 0f;
            float radius = StormRing.Radius + 12f - k * 36f;
            float y0 = StormRing.MinHeight + k * 14f;
            float y1 = Mathf.Lerp(StormRing.MinHeight + 70f, StormRing.MaxHeight, k);
            float random = (float)rng.NextDouble();
            int first = vertices.Count;
            for (int s = 0; s <= StormSegments; s++)
            {
                float u = (float)s / StormSegments;
                float a = u * 2f * Mathf.PI;
                float wobble = 1f + 0.03f * Mathf.Sin(a * 7f + random * 6f);
                Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius * wobble;
                vertices.Add(new Vector3(p.x, y0, p.z));
                vertices.Add(new Vector3(p.x, y1, p.z));
                uv0.Add(new Vector2(u * 12f, 0f));
                uv0.Add(new Vector2(u * 12f, 1f));
                uv1.Add(new Vector2(random, k));
                uv1.Add(new Vector2(random, k));
            }

            for (int s = 0; s < StormSegments; s++)
            {
                int b = first + s * 2;
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                triangles.Add(b + 2); triangles.Add(b + 1); triangles.Add(b + 3);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Two crossed unit quads, the summit column's look: bright at the foot, fading to nothing at the top (the soft texture's upper half).
    static Mesh BuildColumn()
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f),
            new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), new Vector3(0f, 1f, -0.5f), new Vector3(0f, 1f, 0.5f)
        });
        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(1f, 1f)
        });
        mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 }, 0);
        mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
        return mesh;
    }

    // A flat unit quad on the XZ plane (x and z from -0.5 to 0.5), uv across: the sun's path on the sea.
    static Mesh BuildSunPath()
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) });
        mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
        mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---- cloud texture ----

    // A 256x128 tileable fractal value noise in R (deterministic): the storm ring's cloud body.
    static Texture2D EnsureCloudTexture()
    {
        const int w = 256, h = 128;
        System.Random rng = new System.Random(1905);
        const int octaves = 5;
        float[][] lattice = new float[octaves][];
        int[] cells = { 4, 8, 16, 32, 64 };
        for (int o = 0; o < octaves; o++)
        {
            int cw = cells[o], ch = Mathf.Max(2, cells[o] / 2);
            lattice[o] = new float[cw * ch];
            for (int i = 0; i < lattice[o].Length; i++) lattice[o][i] = (float)rng.NextDouble();
        }

        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float sum = 0f, amp = 0.5f, norm = 0f;
                for (int o = 0; o < octaves; o++)
                {
                    int cw = cells[o], ch = Mathf.Max(2, cells[o] / 2);
                    float gx = (float)x / w * cw, gy = (float)y / h * ch;
                    int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
                    float tx = Smooth(gx - x0), ty = Smooth(gy - y0);
                    float a = lattice[o][(y0 % ch) * cw + (x0 % cw)];
                    float b = lattice[o][(y0 % ch) * cw + ((x0 + 1) % cw)];
                    float c = lattice[o][((y0 + 1) % ch) * cw + (x0 % cw)];
                    float d = lattice[o][((y0 + 1) % ch) * cw + ((x0 + 1) % cw)];
                    sum += Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty) * amp;
                    norm += amp;
                    amp *= 0.5f;
                }

                float value = Mathf.Clamp01((sum / norm - 0.2f) * 1.6f);
                byte g = (byte)Mathf.RoundToInt(value * 255f);
                pixels[y * w + x] = new Color32(g, g, g, 255);
            }
        }

        Texture2D bake = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        bake.SetPixels32(pixels);
        byte[] png = bake.EncodeToPNG();
        Object.DestroyImmediate(bake);
        string full = System.IO.Path.GetFullPath(CloudTexturePath);
        if (!System.IO.File.Exists(full) || !BytesEqual(System.IO.File.ReadAllBytes(full), png))
        {
            System.IO.File.WriteAllBytes(full, png);
            AssetDatabase.ImportAsset(CloudTexturePath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(CloudTexturePath) as TextureImporter;
        if (importer != null && (importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Repeat || importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(CloudTexturePath);
    }

    static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // ---- asset helpers ----

    static Material EnsureMaterial(string path, Shader shader, string name)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.name = name;
        return material;
    }

    // Saves `built` at `path`. An existing asset is rewritten only if its geometry differs, so a rerun leaves the file byte-identical.
    static Mesh EnsureMesh(string path, string name, Mesh built)
    {
        built.name = name;
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }

        if (SameGeometry(existing, built))
        {
            Object.DestroyImmediate(built);
            return existing;
        }

        existing.Clear();
        existing.indexFormat = built.indexFormat;
        existing.SetVertices(built.vertices);
        existing.SetNormals(built.normals);
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        built.GetUVs(0, uv0);
        built.GetUVs(1, uv1);
        if (uv0.Count > 0) existing.SetUVs(0, uv0);
        if (uv1.Count > 0) existing.SetUVs(1, uv1);
        Color32[] builtColors = built.colors32;
        if (builtColors.Length > 0) existing.SetColors(builtColors);
        existing.SetTriangles(built.triangles, 0);
        existing.RecalculateBounds();
        existing.name = name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(built);
        return existing;
    }

    static bool SameGeometry(Mesh a, Mesh b)
    {
        if (a.vertexCount != b.vertexCount || a.triangles.Length != b.triangles.Length)
        {
            return false;
        }

        Vector3[] va = a.vertices, vb = b.vertices;
        for (int i = 0; i < va.Length; i++) if (va[i] != vb[i]) return false;
        int[] ta = a.triangles, tb = b.triangles;
        for (int i = 0; i < ta.Length; i++) if (ta[i] != tb[i]) return false;
        List<Vector2> ua = new List<Vector2>(), ub = new List<Vector2>();
        a.GetUVs(0, ua);
        b.GetUVs(0, ub);
        if (ua.Count != ub.Count) return false;
        for (int i = 0; i < ua.Count; i++) if (ua[i] != ub[i]) return false;
        ua.Clear();
        ub.Clear();
        a.GetUVs(1, ua);
        b.GetUVs(1, ub);
        if (ua.Count != ub.Count) return false;
        for (int i = 0; i < ua.Count; i++) if (ua[i] != ub[i]) return false;
        Color32[] ca = a.colors32, cb = b.colors32;
        if (ca.Length != cb.Length) return false;
        for (int i = 0; i < ca.Length; i++) if (!ca[i].Equals(cb[i])) return false;
        return true;
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

    static Scene OpenScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == BridgeInstaller.ScenePath)
            {
                SceneManager.SetActiveScene(loaded);
                return loaded;
            }
        }

        return EditorSceneManager.OpenScene(BridgeInstaller.ScenePath, OpenSceneMode.Single);
    }

    public const string GradeVolumeName = "LookVolume_finale";

    // The finale colour grade: a global volume (priority 2, above the dawn grade, below the effects volume) at weight 0, which FinaleSequence blends in
    // over the sunrise. The profile is its own asset (LookVolume_finale); the shared look and dawn profiles are never touched.
    static Volume BuildGradeVolume(Transform parent)
    {
        VolumeProfile profile = LookVolumeBuilder.BuildFinale();
        GameObject volumeObject = new GameObject(GradeVolumeName);
        volumeObject.transform.SetParent(parent, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 2f;
        volume.weight = 0f;
        volume.enabled = false;
        volume.sharedProfile = profile;
        volumeObject.AddComponent<LookVolumeQuality>();
        return volume;
    }
}
}
