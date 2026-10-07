using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper
{
// Swaps the visual child of the creature prefabs for the modelled versions. Edits prefab assets only: it never opens
// an island scene and never touches world content. Every install is idempotent.
public static class CreatureInstaller
{
    public const string MothFbxPath = "Assets/Game/Art/Creatures/Moth/Moth.fbx";
    public const string MothTexturePath = "Assets/Game/Art/Creatures/Moth/MothWing.png";
    public const string MothWingMaterialPath = "Assets/Game/Art/Creatures/Moth/MothWing.mat";
    public const string MothBodyMaterialPath = "Assets/Game/Art/Creatures/Moth/MothBody.mat";
    public const string MothPrefabPath = "Assets/Game/Prefabs/Gameplay/Moth.prefab";
    public const string VisualName = "Visual";
    const string WingShaderName = "LanternKeeper/MothWing";

    public const string BeaconFbxPath = "Assets/Game/Art/Beacons/Beacon.fbx";
    public const string BeaconGlassMaterialPath = "Assets/Game/Art/Beacons/BeaconGlass.mat";
    public const string BeaconBeamMaterialPath = "Assets/Game/Art/Beacons/BeaconBeam.mat";
    public const string BeaconIronMaterialPath = "Assets/Game/Art/Beacons/BeaconIron.mat";
    public const string BeaconCairnMaterialPath = "Assets/Game/Art/Beacons/BeaconCairn.mat";
    public const string BeaconEmberMaterialPath = "Assets/Game/Art/Beacons/BeaconEmber.mat";
    public const string BeaconPrefabPath = "Assets/Game/Prefabs/Gameplay/Beacon.prefab";
    const string RockTexturePath = "Assets/Game/Art/Environment/Nature/Textures/Rocks_Diffuse_Grey.png";
    const string Island1LookPath = "Assets/Game/Art/Look/LookProfile_island1.asset";
    const string EmberSoftTexturePath = "Assets/Game/Art/Look/Mist/MistSoft.png";

    [MenuItem("Lantern Keeper/Install Creature Visuals")]
    public static void InstallAll()
    {
        InstallMoth();
        InstallBeacon();
        InstallShade();
        InstallFirefly();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // Island scenes spawn moths and Shades from the prefabs and hold Beacon prefab instances, so they pick up the new visuals
    // through the prefabs. This ensures the prefabs are installed and reports any island scene that does not depend on them.
    // It never opens or saves a scene, and never rebuilds a level.
    public static readonly string[] IslandScenePaths =
    {
        "Assets/Game/Scenes/Island1.unity",
        "Assets/Game/Scenes/Island2.unity",
        "Assets/Game/Scenes/Island3.unity",
        "Assets/Game/Scenes/Island4.unity"
    };

    [MenuItem("Lantern Keeper/Install Creature Visuals On Every Island")]
    public static void InstallAllIslands()
    {
        InstallAll();
        for (int i = 0; i < IslandScenePaths.Length; i++)
        {
            string scene = IslandScenePaths[i];
            string[] dependencies = AssetDatabase.GetDependencies(scene, true);
            bool beacon = System.Array.IndexOf(dependencies, BeaconPrefabPath) >= 0;
            bool moth = System.Array.IndexOf(dependencies, MothPrefabPath) >= 0;
            bool shade = System.Array.IndexOf(dependencies, ShadePrefabPath) >= 0;
            Debug.Log("InstallAllIslands: " + scene + " references Beacon=" + beacon + " Moth=" + moth + " Shade=" + shade + ".");
            if (!beacon)
            {
                Debug.LogWarning("InstallAllIslands: " + scene + " does not reference Beacon.prefab; rebuild that island.");
            }
        }
    }

    public static void InstallMoth()
    {
        ConfigureTexture();
        ConfigureModel();
        Material wing = EnsureWingMaterial();
        Material body = EnsureBodyMaterial();
        SwapMothVisual(wing, body);
    }

    static void ConfigureTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(MothTexturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("CreatureInstaller: missing " + MothTexturePath + ". Run ArtSource/Blender/moth_build.py first.");
            return;
        }

        bool dirty = false;
        if (!importer.alphaIsTransparency || !importer.mipmapEnabled
            || !importer.mipMapsPreserveCoverage || !Mathf.Approximately(importer.alphaTestReferenceValue, 0.4f)
            || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.4f;
            importer.wrapMode = TextureWrapMode.Clamp;
            dirty = true;
        }

        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }

    static void ConfigureModel()
    {
        ModelImporter importer = AssetImporter.GetAtPath(MothFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("CreatureInstaller: missing " + MothFbxPath + ". Run ArtSource/Blender/moth_build.py first.");
            return;
        }

        bool dirty = importer.materialImportMode != ModelImporterMaterialImportMode.None
            || importer.bakeAxisConversion || importer.importCameras || importer.importLights
            || importer.importAnimation || importer.isReadable;
        if (!dirty)
        {
            return;
        }

        // moth_build.py bakes the axis conversion into the FBX itself, so the mesh is in Unity space: the shader flaps about the mesh's Z axis.
        importer.globalScale = 1f;
        importer.bakeAxisConversion = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    static Material EnsureWingMaterial()
    {
        Shader shader = Shader.Find(WingShaderName);
        if (shader == null)
        {
            Debug.LogError("CreatureInstaller: shader " + WingShaderName + " not found.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MothWingMaterialPath);
        bool created = material == null;
        if (created)
        {
            material = new Material(shader);
        }

        material.shader = shader;
        material.name = "MothWing";
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MothTexturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.4f);
        material.SetFloat("_LightCap", 0.6f);
        material.enableInstancing = true;
        if (created)
        {
            AssetDatabase.CreateAsset(material, MothWingMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static Material EnsureBodyMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MothBodyMaterialPath);
        bool created = material == null;
        if (created)
        {
            material = new Material(shader);
        }

        material.name = "MothBody";
        Color color = new Color(0.16f, 0.14f, 0.15f, 1f);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Metallic", 0f);
        material.enableInstancing = true;
        if (created)
        {
            AssetDatabase.CreateAsset(material, MothBodyMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static void SwapMothVisual(Material wing, Material body)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MothFbxPath);
        if (model == null || wing == null || body == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MothPrefabPath);
        try
        {
            if (IsInstalled(root.transform, wing, body))
            {
                return;
            }

            // Legacy placeholder children and any earlier install.
            string[] stale = { "Body", "Wings", VisualName };
            for (int i = 0; i < stale.Length; i++)
            {
                Transform child = root.transform.Find(stale[i]);
                while (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    child = root.transform.Find(stale[i]);
                }
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = VisualName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Renderer wingRenderer = null;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                bool isWings = renderer.name == "MothWings";
                renderer.sharedMaterial = isWings ? wing : body;
                renderer.shadowCastingMode = isWings ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                if (isWings)
                {
                    wingRenderer = renderer;
                }
            }

            MothVisual visualComponent = visual.AddComponent<MothVisual>();
            SerializedObject serialized = new SerializedObject(visualComponent);
            serialized.FindProperty("wingRenderer").objectReferenceValue = wingRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, MothPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- Beacon ----------------------------------------------------------------------------------------------------

    // Old primitive children of the generated beacon (IslandBuilder.BuildBeacon): the whole visual is replaced by "Visual".
    public static readonly string[] LegacyBeaconChildren =
    {
        "Footing", "Pole", "BandLow", "BandMid", "BandHigh", "Cloth", "CageA", "CageB", "CageC", "CageD", "CageRing",
        "Wisp", "Flare", VisualName
    };

    // Flame root and its children, resized for the inside of the lantern: (name, local position, local scale).
    static readonly (string name, Vector3 position, Vector3 scale)[] FlamePose =
    {
        ("FlameA", new Vector3(0f, 0.126f, 0f), new Vector3(0.126f, 0.285f, 1f)),
        ("FlameB", new Vector3(0f, 0.108f, 0f), new Vector3(0.09f, 0.234f, 1f)),
        ("FlameC", new Vector3(0f, 0.15f, 0f), new Vector3(0.15f, 0.324f, 1f)),
        ("Heat", new Vector3(0f, 0.084f, 0f), new Vector3(0.12f, 0.165f, 1f)),
        ("Sparks", new Vector3(0f, 0.85f, 0f), Vector3.one),
        ("Smoke", new Vector3(0f, 0.8f, 0f), Vector3.one),
        ("BeaconLight", new Vector3(0f, 0.25f, 0f), Vector3.one)
    };

    const float LanternCentreY = 2.26f;
    const float EmberConeAngle = 42f;

    public static void InstallBeacon()
    {
        ConfigureBeaconModel();
        Material glass = EnsureBeaconGlassMaterial();
        Material beam = EnsureBeaconBeamMaterial();
        Material iron = EnsureBeaconIronMaterial();
        Material cairn = EnsureBeaconCairnMaterial();
        Material ember = EnsureBeaconEmberMaterial();
        SwapBeaconVisual(glass, beam, iron, cairn, ember);
    }

    static void ConfigureBeaconModel()
    {
        ModelImporter importer = AssetImporter.GetAtPath(BeaconFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("CreatureInstaller: missing " + BeaconFbxPath + ". Run ArtSource/Blender/beacon_build.py first.");
            return;
        }

        bool dirty = importer.materialImportMode != ModelImporterMaterialImportMode.None
            || importer.bakeAxisConversion || importer.importCameras || importer.importLights
            || importer.importAnimation || importer.isReadable;
        if (!dirty)
        {
            return;
        }

        // beacon_build.py bakes the axis conversion into the FBX, like the moth: the mesh is already in Unity space (Y up).
        importer.globalScale = 1f;
        importer.bakeAxisConversion = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    static Material LoadOrCreate(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.enableInstancing = true;
        return material;
    }

    static Material EnsureBeaconGlassMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/BeaconGlass");
        if (shader == null)
        {
            Debug.LogError("CreatureInstaller: shader LanternKeeper/BeaconGlass not found.");
            return null;
        }

        Material material = LoadOrCreate(BeaconGlassMaterialPath, shader);
        material.name = "BeaconGlass";
        material.SetColor("_AmberColor", LookPalette.FromHex(LookPalette.LanternAmber));
        material.SetColor("_CoreColor", LookPalette.FromHex(LookPalette.GlowCore));
        material.SetColor("_GlassColor", new Color(0.008f, 0.01f, 0.016f, 1f));
        material.SetColor("_ColdColor", new Color(0.58f, 0.72f, 0.95f, 1f));
        material.SetFloat("_EmissionGain", 4f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material EnsureBeaconBeamMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/BeaconBeam");
        if (shader == null)
        {
            Debug.LogError("CreatureInstaller: shader LanternKeeper/BeaconBeam not found.");
            return null;
        }

        Material material = LoadOrCreate(BeaconBeamMaterialPath, shader);
        material.name = "BeaconBeam";
        material.SetColor("_BaseColor", LookPalette.FromHex(LookPalette.LanternAmber));
        material.SetColor("_CoreColor", LookPalette.FromHex(LookPalette.GlowCore));
        material.SetFloat("_Strength", 0.5f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material EnsureBeaconIronMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/BeaconIron");
        if (shader == null)
        {
            Debug.LogError("CreatureInstaller: shader LanternKeeper/BeaconIron not found.");
            return null;
        }

        Material material = LoadOrCreate(BeaconIronMaterialPath, shader);
        material.name = "BeaconIron";
        material.SetColor("_BaseColor", new Color(0.045f, 0.047f, 0.055f, 1f));
        material.SetFloat("_Smoothness", 0.4f);
        material.SetColor("_RimColor", new Color(0.58f, 0.72f, 0.95f, 1f));
        material.SetFloat("_RimStrength", 1.6f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // The same recipe as the per-island rock materials (NatureKitImporter "Rock", IslandBuilder cliff skin): kit Rocks_Diffuse_Grey times rockTint.
    // The prefab is shared by all islands, so this asset carries island 1's tint as its default and BeaconVisual
    // multiplies in the active island's LookProfile.rockTint at runtime through a MaterialPropertyBlock.
    static Material EnsureBeaconCairnMaterial()
    {
        Material material = LoadOrCreate(BeaconCairnMaterialPath, Shader.Find("Universal Render Pipeline/Lit"));
        material.name = "BeaconCairn";
        LookProfile look = AssetDatabase.LoadAssetAtPath<LookProfile>(Island1LookPath);
        Color tint = look != null ? look.rockTint : Color.white;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RockTexturePath));
        material.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material EnsureBeaconEmberMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/AdditiveUnlit");
        Material material = LoadOrCreate(BeaconEmberMaterialPath, shader);
        material.name = "BeaconEmber";
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(EmberSoftTexturePath));
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SwapBeaconVisual(Material glass, Material beam, Material iron, Material cairn, Material ember)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BeaconFbxPath);
        if (model == null || glass == null || beam == null || iron == null || cairn == null || ember == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BeaconPrefabPath);
        try
        {
            if (BeaconInstalled(root.transform, glass, beam))
            {
                return;
            }

            for (int i = 0; i < LegacyBeaconChildren.Length; i++)
            {
                Transform child = root.transform.Find(LegacyBeaconChildren[i]);
                while (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    child = root.transform.Find(LegacyBeaconChildren[i]);
                }
            }

            // The legacy Flare particle system was Beacon.embers; BeaconVisual owns the ember burst now.
            Beacon beacon = root.GetComponent<Beacon>();
            if (beacon != null)
            {
                SerializedObject serializedBeacon = new SerializedObject(beacon);
                serializedBeacon.FindProperty("embers").objectReferenceValue = null;
                serializedBeacon.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform flameRoot = root.transform.Find("FlameRoot");
            if (flameRoot != null)
            {
                flameRoot.localPosition = new Vector3(0f, LanternCentreY, 0f);
                for (int i = 0; i < FlamePose.Length; i++)
                {
                    Transform part = flameRoot.Find(FlamePose[i].name);
                    if (part != null)
                    {
                        part.localPosition = FlamePose[i].position;
                        part.localScale = FlamePose[i].scale;
                    }
                }
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = VisualName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Renderer glassRenderer = null;
            Renderer beamRenderer = null;
            System.Collections.Generic.List<Renderer> lod0 = new System.Collections.Generic.List<Renderer>();
            System.Collections.Generic.List<Renderer> lod1 = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                bool low = name.EndsWith("_L1");
                bool shadows = true;
                if (name.StartsWith("BeaconCairn"))
                {
                    renderer.sharedMaterial = cairn;
                }
                else if (name.StartsWith("BeaconIron"))
                {
                    renderer.sharedMaterial = iron;
                }
                else if (name == "BeaconGlass")
                {
                    renderer.sharedMaterial = glass;
                    glassRenderer = renderer;
                    shadows = false;
                }
                else if (name == "BeaconBeams")
                {
                    renderer.sharedMaterial = beam;
                    beamRenderer = renderer;
                    shadows = false;
                    renderer.receiveShadows = false;
                    renderer.enabled = false;
                }

                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (name == "BeaconGlass" || name == "BeaconBeams")
                {
                    lod0.Add(renderer);
                    lod1.Add(renderer);
                }
                else if (low)
                {
                    lod1.Add(renderer);
                }
                else
                {
                    lod0.Add(renderer);
                }
            }

            // The beams are about 6 m across, so the group size is set by hand (the lamp-post is 3.3 m tall).
            LODGroup group = visual.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.2f, lod0.ToArray()), new LOD(0f, lod1.ToArray()) });
            group.size = 3.3f;
            group.fadeMode = LODFadeMode.None;

            GameObject embers = CreateEmbers(visual.transform, ember);

            BeaconVisual component = visual.AddComponent<BeaconVisual>();
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty("glassRenderer").objectReferenceValue = glassRenderer;
            serialized.FindProperty("beamRenderer").objectReferenceValue = beamRenderer;
            serialized.FindProperty("embers").objectReferenceValue = embers.GetComponent<ParticleSystem>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BeaconPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The ember burst: BeaconVisual emits by script (45, or 15 on Low), so the system itself has no rate and no looping.
    static GameObject CreateEmbers(Transform parent, Material material)
    {
        GameObject go = new GameObject("MomentEmbers");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 1.6f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(LookPalette.FromHex(LookPalette.LanternAmber), LookPalette.FromHex(LookPalette.GlowCore));
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = EmberConeAngle;
        shape.radius = 0.2f;

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.8f;

        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    static bool BeaconInstalled(Transform root, Material glass, Material beam)
    {
        Transform visual = root.Find(VisualName);
        if (visual == null)
        {
            return false;
        }

        for (int i = 0; i < LegacyBeaconChildren.Length; i++)
        {
            if (LegacyBeaconChildren[i] != VisualName && root.Find(LegacyBeaconChildren[i]) != null)
            {
                return false;
            }
        }

        BeaconVisual component = visual.GetComponent<BeaconVisual>();
        if (component == null || component.GlassRenderer == null || component.GlassRenderer.sharedMaterial != glass
            || component.BeamRenderer == null || component.BeamRenderer.sharedMaterial != beam || component.Embers == null
            || !Mathf.Approximately(component.Embers.shape.angle, EmberConeAngle))
        {
            return false;
        }

        Transform flameRoot = root.Find("FlameRoot");
        return flameRoot == null || Mathf.Approximately(flameRoot.localPosition.y, LanternCentreY);
    }

    static bool IsInstalled(Transform root, Material wing, Material body)
    {
        Transform visual = root.Find(VisualName);
        if (visual == null || root.Find("Body") != null || root.Find("Wings") != null)
        {
            return false;
        }

        MothVisual component = visual.GetComponent<MothVisual>();
        if (component == null || component.WingRenderer == null || component.WingRenderer.sharedMaterial != wing)
        {
            return false;
        }

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material expected = renderer.name == "MothWings" ? wing : body;
            if (renderer.sharedMaterial != expected)
            {
                return false;
            }
        }

        return true;
    }

    // ---- Shade -----------------------------------------------------------------------------------------------------

    public const string ShadePrefabPath = "Assets/Game/Prefabs/Gameplay/Shade.prefab";
    public const string ShadeBodyMaterialPath = "Assets/Game/Art/Creatures/Shade/ShadeWraith.mat";
    public const string ShadeEyeMaterialPath = "Assets/Game/Art/Creatures/Shade/ShadeEyePale.mat";
    public const string ShadeSmokeMaterialPath = "Assets/Game/Art/Creatures/Shade/ShadeTrail.mat";
    const string ShadeFolder = "Assets/Game/Art/Creatures/Shade";
    const string SmokeSoftTexturePath = "Assets/Game/Art/Look/Mist/MistSoft.png";

    // Re-materials the existing hollow wraith (the mesh, the Body/Eyes/Smoke children, the colliders and the Shade logic are kept),
    // adds the ShadeVisual controller on the root and turns the existing Smoke child into the speed driven trail.
    // Shade.cs finds Body, Eyes and Smoke by name, so no child is renamed or added.
    public static void InstallShade()
    {
        Shader body = Shader.Find("LanternKeeper/Shade");
        Shader eye = Shader.Find("LanternKeeper/ShadeEye");
        Shader smoke = Shader.Find("LanternKeeper/ShadeSmoke");
        if (body == null || eye == null || smoke == null)
        {
            Debug.LogError("CreatureInstaller: Shade shaders not found (LanternKeeper/Shade, ShadeEye, ShadeSmoke).");
            return;
        }

        if (!AssetDatabase.IsValidFolder(ShadeFolder))
        {
            AssetDatabase.CreateFolder("Assets/Game/Art/Creatures", "Shade");
        }

        Material bodyMaterial = LoadOrCreate(ShadeBodyMaterialPath, body);
        bodyMaterial.name = "ShadeWraith";
        bodyMaterial.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.04f, 0.92f));
        bodyMaterial.SetColor("_InkColor", new Color(0.02f, 0.02f, 0.04f, 1f));
        bodyMaterial.SetColor("_StunInkColor", new Color(0.04f, 0.055f, 0.11f, 1f));
        bodyMaterial.SetFloat("_BodyAlphaRef", 0.92f);
        bodyMaterial.SetColor("_OutlineColor", LookPalette.FromHex(LookPalette.LightningBlue));
        bodyMaterial.SetColor("_BurnColor", LookPalette.FromHex(ShadeVisual.BurnEdgeHex));
        bodyMaterial.SetColor("_RimColor", new Color(0.58f, 0.72f, 0.95f, 1f));
        bodyMaterial.SetFloat("_RimStrength", 0.3f);
        bodyMaterial.SetFloat("_SkyTint", 0.08f);
        bodyMaterial.SetFloat("_BurnGain", 1f);
        EditorUtility.SetDirty(bodyMaterial);

        Material eyeMaterial = LoadOrCreate(ShadeEyeMaterialPath, eye);
        eyeMaterial.name = "ShadeEyePale";
        eyeMaterial.SetColor("_Color", new Color(0.78f, 0.88f, 1f, 1f));
        eyeMaterial.SetFloat("_Gain", 2.4f);
        EditorUtility.SetDirty(eyeMaterial);

        Material smokeMaterial = LoadOrCreate(ShadeSmokeMaterialPath, smoke);
        smokeMaterial.name = "ShadeTrail";
        smokeMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeSoftTexturePath));
        smokeMaterial.SetColor("_BaseColor", new Color(0.14f, 0.17f, 0.3f, 1f));
        EditorUtility.SetDirty(smokeMaterial);
        AssetDatabase.SaveAssets();

        GameObject root = PrefabUtility.LoadPrefabContents(ShadePrefabPath);
        try
        {
            Transform bodyTransform = root.transform.Find("Body");
            Transform eyes = root.transform.Find("Eyes");
            Transform smokeTransform = root.transform.Find("Smoke");
            if (bodyTransform == null || eyes == null || smokeTransform == null)
            {
                Debug.LogError("CreatureInstaller: Shade.prefab needs Body, Eyes and Smoke children.");
                return;
            }

            Renderer bodyRenderer = bodyTransform.GetComponent<Renderer>();
            if (IsShadeInstalled(root, bodyRenderer, eyes, smokeTransform, bodyMaterial, eyeMaterial, smokeMaterial))
            {
                return;
            }

            bodyRenderer.sharedMaterial = bodyMaterial;
            bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (Renderer eyeRenderer in eyes.GetComponentsInChildren<Renderer>(true))
            {
                eyeRenderer.sharedMaterial = eyeMaterial;
            }

            ParticleSystem trail = smokeTransform.GetComponent<ParticleSystem>();
            ConfigureShadeTrail(trail, smokeMaterial);

            ShadeVisual visual = root.GetComponent<ShadeVisual>();
            if (visual == null)
            {
                visual = root.AddComponent<ShadeVisual>();
            }

            SerializedObject serialized = new SerializedObject(visual);
            serialized.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
            serialized.FindProperty("trail").objectReferenceValue = trail;
            serialized.FindProperty("trailBaseRate").floatValue = 10f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ShadePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // True when the Shade prefab already carries these materials and a fully wired ShadeVisual, so a re-run changes nothing.
    static bool IsShadeInstalled(GameObject root, Renderer bodyRenderer, Transform eyes, Transform smokeTransform,
        Material bodyMaterial, Material eyeMaterial, Material smokeMaterial)
    {
        if (bodyRenderer == null || bodyRenderer.sharedMaterial != bodyMaterial
            || bodyRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
        {
            return false;
        }

        foreach (Renderer eyeRenderer in eyes.GetComponentsInChildren<Renderer>(true))
        {
            if (eyeRenderer.sharedMaterial != eyeMaterial)
            {
                return false;
            }
        }

        ParticleSystem trail = smokeTransform.GetComponent<ParticleSystem>();
        ParticleSystemRenderer trailRenderer = smokeTransform.GetComponent<ParticleSystemRenderer>();
        if (trail == null || trailRenderer == null || trailRenderer.sharedMaterial != smokeMaterial)
        {
            return false;
        }

        ShadeVisual visual = root.GetComponent<ShadeVisual>();
        if (visual == null)
        {
            return false;
        }

        SerializedObject serialized = new SerializedObject(visual);
        return serialized.FindProperty("bodyRenderer").objectReferenceValue == bodyRenderer
            && serialized.FindProperty("trail").objectReferenceValue == trail
            && Mathf.Approximately(serialized.FindProperty("trailBaseRate").floatValue, 10f);
    }

    // A low, slow smoke that lingers behind the Shade in world space: thin when idle, thick at speed (ShadeVisual drives the rate).
    static void ConfigureShadeTrail(ParticleSystem system, Material material)
    {
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.5f;
        main.startSpeed = 0.12f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.85f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.2831853f);
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 10f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        ParticleSystem.VelocityOverLifetimeModule rise = system.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.Local;
        rise.y = 0.2f;

        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colour.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // ---- Firefly ---------------------------------------------------------------------------------------------------

    public const string FireflyPrefabPath = "Assets/Game/Prefabs/Gameplay/Firefly.prefab";
    public const string FireflyMoteMaterialPath = "Assets/Game/Materials/Generated/FireflyMote.mat";
    public const string SwarmName = "Swarm";

    // Replaces the placeholder firefly (the Body sphere and the PickupBurst particles) with a Swarm child: the FireflySwarm
    // controller and six camera-facing mote quads. The Glow light, the collider and the Firefly logic are kept.
    public static void InstallFirefly()
    {
        Shader shader = Shader.Find("LanternKeeper/FireflyMote");
        if (shader == null)
        {
            Debug.LogError("CreatureInstaller: shader LanternKeeper/FireflyMote not found.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(FireflyMoteMaterialPath);
        Color halo = LookPalette.FromHex(LookPalette.FireflyGreen);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, FireflyMoteMaterialPath);
        }

        if (material.shader != shader || !material.enableInstancing || material.GetColor("_Color") != halo
            || !Mathf.Approximately(material.GetFloat("_Gain"), FireflyMoteGain))
        {
            material.shader = shader;
            material.name = "FireflyMote";
            material.SetColor("_Color", halo);
            material.SetFloat("_Gain", FireflyMoteGain);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        GameObject root = PrefabUtility.LoadPrefabContents(FireflyPrefabPath);
        try
        {
            if (IsFireflyInstalled(root.transform, material))
            {
                return;
            }

            string[] stale = { "Body", "PickupBurst", SwarmName };
            for (int i = 0; i < stale.Length; i++)
            {
                Transform child = root.transform.Find(stale[i]);
                while (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    child = root.transform.Find(stale[i]);
                }
            }

            GameObject swarm = new GameObject(SwarmName);
            swarm.transform.SetParent(root.transform, false);
            swarm.transform.localPosition = Vector3.zero;
            swarm.AddComponent<FireflySwarm>();

            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            for (int i = 0; i < FireflyCurve.Motes; i++)
            {
                GameObject mote = new GameObject("Mote" + i);
                mote.transform.SetParent(swarm.transform, false);
                mote.transform.localPosition = Vector3.zero;
                mote.transform.localScale = Vector3.one * FireflyMoteSize;
                mote.AddComponent<MeshFilter>().sharedMesh = quad;
                MeshRenderer renderer = mote.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 3f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, FireflyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    const float FireflyMoteSize = 0.15f;
    const float FireflyMoteGain = 1f;

    static bool IsFireflyInstalled(Transform root, Material material)
    {
        if (root.Find("Body") != null || root.Find("PickupBurst") != null)
        {
            return false;
        }

        Transform swarm = root.Find(SwarmName);
        if (swarm == null || swarm.GetComponent<FireflySwarm>() == null)
        {
            return false;
        }

        for (int i = 0; i < FireflyCurve.Motes; i++)
        {
            Transform mote = swarm.Find("Mote" + i);
            MeshRenderer renderer = mote != null ? mote.GetComponent<MeshRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial != material || renderer.shadowCastingMode != ShadowCastingMode.Off
                || renderer.receiveShadows || !material.enableInstancing)
            {
                return false;
            }
        }

        return swarm.childCount == FireflyCurve.Motes;
    }
}
}
