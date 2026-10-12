using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Installs the Island 5 finale cinematic on the built scene (BuildIsland5 runs it after the performance pass, before the audio).
//  - Bakes the silhouettes of Islands 1-4 from their TerrainData (read only; the scenes are not opened and the terrain is not modified):
//    a 48x48 grid mesh at the terrain's true size, heights relative to that island's sea, with the sea skirt cut away.
//    Assets/Game/Art/Finale/Silhouette_Island{1..4}.asset. The meshes are rewritten only if they differ, so a second run is byte-identical.
//  - Bakes the storm ring meshes (full and Low) and the cloud noise texture, and the materials for the ring, the silhouettes and the glow.
//  - Builds the "Finale" object in Island5: FinaleSequence + FinaleCamera, StormRing, FinaleIslands (the four silhouettes, each with five
//    BeaconBeam clusters and a glow sprite) and the ember burst; wires GameManager.finale.
// Idempotent: the Finale object is rebuilt each run. The scene is edited through EditorSceneManager, never as text.
public static class FinaleInstaller
{
    public const string Folder = "Assets/Game/Art/Finale";
    public const string RootName = "Finale";
    public const int GridCells = 48;
    public const int FlareClusters = 5;
    // The islands are low (4 to 9 m above their sea); at 520 m they would be a few pixels. The silhouette is stretched vertically by this much.
    public const float VerticalExaggeration = 4f;
    const string CloudTexturePath = Folder + "/StormCloud.png";
    const string StormMaterialPath = Folder + "/StormRing.mat";
    const string SilhouetteMaterialPath = Folder + "/FinaleSilhouette.mat";
    const string GlowMaterialPath = Folder + "/FinaleGlow.mat";
    const string StormFullPath = Folder + "/StormRing_Full.asset";
    const string StormLowPath = Folder + "/StormRing_Low.asset";
    const string GlowQuadPath = Folder + "/FinaleGlowQuad.asset";
    const string SoftTexturePath = "Assets/Game/Art/Look/Mist/MistSoft.png";
    // IslandBuilder.WaterFraction: the sea stands at 20% of the terrain height.
    const float WaterFraction = 0.2f;
    const float SkirtFloor = -2f;
    const float ClusterSpacing = 8f;
    const int StormCardsPerLayerFull = 24;
    const int StormCardsPerLayerLow = 12;
    const int StormSegments = 4;

    [MenuItem("Lantern Keeper/Install Finale (Island 5)")]
    public static void InstallMenu()
    {
        Install();
    }

    public static string SilhouettePath(int island)
    {
        return Folder + "/Silhouette_Island" + island + ".asset";
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
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FinaleInstaller: installed the finale in " + scene.name + ".");
        }
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
        Shader silhouetteShader = Shader.Find("LanternKeeper/Silhouette");
        Shader additive = Shader.Find("LanternKeeper/AdditiveUnlit");
        Material beamMaterial = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconBeamMaterialPath);
        Material emberMaterial = AssetDatabase.LoadAssetAtPath<Material>(CreatureInstaller.BeaconEmberMaterialPath);
        Mesh beamMesh = AssetDatabase.LoadAssetAtPath<Mesh>(LighthouseInstaller.BeamMeshPath);
        Texture2D soft = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftTexturePath);
        if (stormShader == null || silhouetteShader == null || additive == null || beamMaterial == null || emberMaterial == null || beamMesh == null || soft == null)
        {
            Debug.LogError("FinaleInstaller: a shader, material, the lamp beam mesh or MistSoft is missing.");
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

        Material silhouetteMaterial = EnsureMaterial(SilhouetteMaterialPath, silhouetteShader, "FinaleSilhouette");
        silhouetteMaterial.SetColor("_NearColor", new Color(0.30f, 0.36f, 0.50f, 1f));
        silhouetteMaterial.SetColor("_FarColor", new Color(0.36f, 0.42f, 0.56f, 1f));
        silhouetteMaterial.SetFloat("_Band", 0.3f);
        silhouetteMaterial.SetFloat("_RimStrength", 0.35f);
        silhouetteMaterial.SetFloat("_Fade", 1f);
        EditorUtility.SetDirty(silhouetteMaterial);

        Material glowMaterial = EnsureMaterial(GlowMaterialPath, additive, "FinaleGlow");
        glowMaterial.SetTexture("_BaseMap", soft);
        glowMaterial.SetColor("_BaseColor", new Color(1f, 0.694f, 0.361f, 0.8f));
        EditorUtility.SetDirty(glowMaterial);

        Mesh stormFull = EnsureMesh(StormFullPath, "StormRing_Full", BuildStormMesh(StormCardsPerLayerFull, 5505));
        Mesh stormLow = EnsureMesh(StormLowPath, "StormRing_Low", BuildStormMesh(StormCardsPerLayerLow, 5505));
        Mesh glowQuad = EnsureMesh(GlowQuadPath, "FinaleGlowQuad", BuildQuad());

        Mesh[] silhouettes = new Mesh[4];
        List<Vector3>[] clusters = new List<Vector3>[4];
        for (int i = 0; i < 4; i++)
        {
            if (!BakeSilhouette(i + 1, out silhouettes[i], out clusters[i]))
            {
                return false;
            }
        }

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
        stormData.FindProperty("cardsFull").intValue = StormCardsPerLayerFull * 2;
        stormData.FindProperty("cardsLow").intValue = StormCardsPerLayerLow * 2;
        stormData.ApplyModifiedPropertiesWithoutUndo();

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
            GameObject island = new GameObject("Island" + (i + 1));
            island.transform.SetParent(islandsObject.transform, false);
            Vector2 xz = FinaleLayout.SilhouetteXZ(i);
            island.transform.localPosition = new Vector3(xz.x, 0f, xz.y);
            island.transform.localScale = Vector3.one * FinaleLayout.SilhouetteScale(i);

            GameObject shape = new GameObject("Silhouette");
            shape.transform.SetParent(island.transform, false);
            shape.transform.localScale = new Vector3(1f, VerticalExaggeration, 1f);
            shape.AddComponent<MeshFilter>().sharedMesh = silhouettes[i];
            MeshRenderer shapeRenderer = shape.AddComponent<MeshRenderer>();
            ConfigureRenderer(shapeRenderer, silhouetteMaterial);

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("root").objectReferenceValue = island.transform;
            entry.FindPropertyRelative("silhouette").objectReferenceValue = shapeRenderer;
            SerializedProperty beams = entry.FindPropertyRelative("beams");
            beams.arraySize = FlareClusters;
            for (int b = 0; b < FlareClusters; b++)
            {
                Vector3 at = clusters[i][Mathf.Min(b, clusters[i].Count - 1)];
                GameObject cluster = new GameObject("Beam" + b);
                cluster.transform.SetParent(island.transform, false);
                cluster.transform.localPosition = new Vector3(at.x, at.y * VerticalExaggeration + 1.5f, at.z);
                cluster.transform.localRotation = Quaternion.Euler(0f, b * 180f / FlareClusters, 0f);
                cluster.transform.localScale = new Vector3(1f, 5f, 1f);
                cluster.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                MeshRenderer beamRenderer = cluster.AddComponent<MeshRenderer>();
                ConfigureRenderer(beamRenderer, beamMaterial);
                beams.GetArrayElementAtIndex(b).objectReferenceValue = beamRenderer;
            }

            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(island.transform, false);
            glow.transform.localPosition = new Vector3(clusters[i][0].x, clusters[i][0].y * VerticalExaggeration + 6f, clusters[i][0].z);
            glow.AddComponent<MeshFilter>().sharedMesh = glowQuad;
            MeshRenderer glowRenderer = glow.AddComponent<MeshRenderer>();
            ConfigureRenderer(glowRenderer, glowMaterial);
            entry.FindPropertyRelative("glow").objectReferenceValue = glow.transform;
            entry.FindPropertyRelative("glowRenderer").objectReferenceValue = glowRenderer;
        }

        islandData.ApplyModifiedPropertiesWithoutUndo();

        // The ember burst at the flame.
        ParticleSystem embers = CreateEmbers(finaleObject.transform, emberMaterial, flame.position);

        SerializedObject sequenceData = new SerializedObject(sequence);
        sequenceData.FindProperty("lamp").objectReferenceValue = lamp;
        sequenceData.FindProperty("beam").objectReferenceValue = beam;
        sequenceData.FindProperty("finaleCamera").objectReferenceValue = finaleCamera;
        sequenceData.FindProperty("storm").objectReferenceValue = storm;
        sequenceData.FindProperty("islands").objectReferenceValue = islands;
        sequenceData.FindProperty("dawn").objectReferenceValue = dawn;
        sequenceData.FindProperty("embers").objectReferenceValue = embers;
        sequenceData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject managerData = new SerializedObject(manager);
        managerData.FindProperty("finale").objectReferenceValue = sequence;
        managerData.ApplyModifiedPropertiesWithoutUndo();
        return true;
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

    // ---- silhouettes ----

    // Bakes island `island` (1..4). `clusters` are up to five beacon cluster spots (island local, y relative to the sea), highest first.
    static bool BakeSilhouette(int island, out Mesh mesh, out List<Vector3> clusters)
    {
        mesh = null;
        clusters = null;
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath(island));
        if (data == null)
        {
            Debug.LogError("FinaleInstaller: missing " + TerrainPath(island));
            return false;
        }

        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 size = data.size;
        float waterY = WaterFraction * size.y;
        int n = GridCells + 1;
        Vector3[] grid = new Vector3[n * n];
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / GridCells;
                float v = (float)j / GridCells;
                float y = Bilinear(heights, res, u, v) * size.y - waterY;
                grid[j * n + i] = new Vector3((u - 0.5f) * size.x, y, (v - 0.5f) * size.z);
            }
        }

        // Triangles with at least one corner at or above the sea; the rest of the skirt is cut away.
        List<int> kept = new List<int>();
        for (int j = 0; j < GridCells; j++)
        {
            for (int i = 0; i < GridCells; i++)
            {
                int v00 = j * n + i, v10 = v00 + 1, v01 = v00 + n, v11 = v01 + 1;
                AddTriangle(kept, grid, v00, v01, v10);
                AddTriangle(kept, grid, v10, v01, v11);
            }
        }

        int[] remap = new int[grid.Length];
        for (int i = 0; i < remap.Length; i++) remap[i] = -1;
        List<Vector3> vertices = new List<Vector3>();
        int[] triangles = new int[kept.Count];
        for (int t = 0; t < kept.Count; t++)
        {
            int old = kept[t];
            if (remap[old] < 0)
            {
                remap[old] = vertices.Count;
                Vector3 p = grid[old];
                p.y = Mathf.Max(p.y, SkirtFloor);
                vertices.Add(p);
            }

            triangles[t] = remap[old];
        }

        Mesh built = new Mesh();
        built.name = "Silhouette_Island" + island;
        built.SetVertices(vertices);
        built.SetTriangles(triangles, 0);
        built.RecalculateNormals();
        built.RecalculateBounds();
        mesh = EnsureMesh(SilhouettePath(island), built.name, built);

        // Cluster spots: the highest grid points, kept ClusterSpacing apart.
        List<int> order = new List<int>();
        for (int i = 0; i < grid.Length; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            int c = grid[b].y.CompareTo(grid[a].y);
            return c != 0 ? c : a.CompareTo(b);
        });
        clusters = new List<Vector3>();
        for (int k = 0; k < order.Count && clusters.Count < FlareClusters; k++)
        {
            Vector3 p = grid[order[k]];
            bool apart = true;
            for (int c = 0; c < clusters.Count; c++)
            {
                Vector2 d = new Vector2(clusters[c].x - p.x, clusters[c].z - p.z);
                if (d.magnitude < ClusterSpacing)
                {
                    apart = false;
                    break;
                }
            }

            if (apart && p.y > 0f)
            {
                clusters.Add(p);
            }
        }

        if (clusters.Count == 0)
        {
            Debug.LogError("FinaleInstaller: island " + island + " has no land above the sea.");
            return false;
        }

        return true;
    }

    static void AddTriangle(List<int> kept, Vector3[] grid, int a, int b, int c)
    {
        if (Mathf.Max(grid[a].y, Mathf.Max(grid[b].y, grid[c].y)) >= 0f)
        {
            kept.Add(a);
            kept.Add(b);
            kept.Add(c);
        }
    }

    // Heights are [z, x]; u and v run 0..1 across the terrain.
    static float Bilinear(float[,] heights, int res, float u, float v)
    {
        float fx = u * (res - 1), fz = v * (res - 1);
        int x0 = Mathf.Min((int)fx, res - 2), z0 = Mathf.Min((int)fz, res - 2);
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(heights[z0, x0], heights[z0, x0 + 1], tx);
        float b = Mathf.Lerp(heights[z0 + 1, x0], heights[z0 + 1, x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // ---- storm ring meshes ----

    // Two layers of cloud cards round the island at StormRing.Radius, StormRing.MinHeight..MaxHeight above the object (the sea). Each card is an
    // arc of StormSegments segments, so wide cards still follow the circle. uv0 runs across the card; uv1.x is a per-card random.
    static Mesh BuildStormMesh(int cardsPerLayer, int seed)
    {
        System.Random rng = new System.Random(seed);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        List<int> triangles = new List<int>();
        float spacing = 2f * Mathf.PI / cardsPerLayer;
        for (int layer = 0; layer < 2; layer++)
        {
            for (int c = 0; c < cardsPerLayer; c++)
            {
                float centre = (c + 0.5f * layer) * spacing + ((float)rng.NextDouble() - 0.5f) * spacing * 0.2f;
                float half = spacing * 0.95f;
                float radius = StormRing.Radius - layer * 12f + ((float)rng.NextDouble() - 0.5f) * 16f;
                float y0 = StormRing.MinHeight + (float)rng.NextDouble() * 14f + layer * 18f;
                float y1 = Mathf.Min(StormRing.MaxHeight, y0 + 65f + (float)rng.NextDouble() * 50f);
                float random = (float)rng.NextDouble();
                int first = vertices.Count;
                for (int s = 0; s <= StormSegments; s++)
                {
                    float k = (float)s / StormSegments;
                    float a = centre + Mathf.Lerp(-half, half, k);
                    Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                    vertices.Add(new Vector3(p.x, y0, p.z));
                    vertices.Add(new Vector3(p.x, y1, p.z));
                    uv0.Add(new Vector2(k, 0f));
                    uv0.Add(new Vector2(k, 1f));
                    uv1.Add(new Vector2(random, 0f));
                    uv1.Add(new Vector2(random, 0f));
                }

                for (int s = 0; s < StormSegments; s++)
                {
                    int b = first + s * 2;
                    triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                    triangles.Add(b + 2); triangles.Add(b + 1); triangles.Add(b + 3);
                }
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
        existing.SetVertices(built.vertices);
        existing.SetNormals(built.normals);
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        built.GetUVs(0, uv0);
        built.GetUVs(1, uv1);
        if (uv0.Count > 0) existing.SetUVs(0, uv0);
        if (uv1.Count > 0) existing.SetUVs(1, uv1);
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
}
}
