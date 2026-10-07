using UnityEditor;
using UnityEngine;

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

    [MenuItem("Lantern Keeper/Install Creature Visuals")]
    public static void InstallAll()
    {
        InstallMoth();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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
}
}
