using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
public static class GraphicsProfileSetup
{
    const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
    const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
    const string UrpFolder = "Assets/Settings/Graphics";
    const string ProfileFolder = "Assets/Game/Settings/Graphics";
    const string ProfileSetPath = "Assets/Game/Resources/GraphicsProfileSet.asset";

    [MenuItem("Lantern Keeper/Create Graphics Profiles")]
    public static void CreateGraphicsProfiles()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit play mode before creating graphics profiles.");
            return;
        }

        EnsureFolder(UrpFolder);
        EnsureFolder(ProfileFolder);
        EnsureFolder("Assets/Game/Resources");

        UniversalRenderPipelineAsset[] pipelines = new UniversalRenderPipelineAsset[4];
        pipelines[0] = EnsurePipeline("URP_Low");
        pipelines[1] = EnsurePipeline("URP_Medium");
        pipelines[2] = EnsurePipeline("URP_High");
        pipelines[3] = EnsurePipeline("URP_Ultra");

        ApplyPipeline(pipelines[0], 0.75f, "Disabled", false, 20f, 1, 1024, false, "Low", 2, false);
        ApplyPipeline(pipelines[1], 1f, "Disabled", true, 50f, 4, 2048, true, "High", 4, true);
        ApplyPipeline(pipelines[2], 1f, "_4x", true, 70f, 4, 2048, true, "High", 6, true);
        ApplyPipeline(pipelines[3], 1f, "_4x", true, 100f, 4, 4096, true, "High", 8, true);

        GraphicsProfile[] profiles = new GraphicsProfile[4];
        for (int i = 0; i < 4; i++)
        {
            GraphicsLevel level = (GraphicsLevel)i;
            string path = ProfileFolder + "/GraphicsProfile_" + level + ".asset";
            profiles[i] = LoadOrCreate<GraphicsProfile>(path);
            WriteProfile(profiles[i], level);
            EditorUtility.SetDirty(profiles[i]);
        }

        GraphicsProfileSet set = LoadProfileSet();
        set.low = profiles[0];
        set.medium = profiles[1];
        set.high = profiles[2];
        set.ultra = profiles[3];
        EditorUtility.SetDirty(set);

        WriteQualityLevels(pipelines);
        GraphicsSettings.defaultRenderPipeline = pipelines[1];
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Graphics profiles ready. Active level " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
    }

    static void WriteProfile(GraphicsProfile profile, GraphicsLevel level)
    {
        profile.level = level;
        bool low = level == GraphicsLevel.Low;
        bool high = level == GraphicsLevel.High || level == GraphicsLevel.Ultra;
        bool ultra = level == GraphicsLevel.Ultra;

        profile.renderScale = low ? 0.75f : 1f;
        profile.msaaSampleCount = high ? 4 : 1;
        profile.supportsHdr = !low;
        profile.cameraAntialiasing = ultra
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
            : AntialiasingMode.None;
        profile.postProcessing = low
            ? GraphicsPostProcessing.Off
            : (level == GraphicsLevel.Medium ? GraphicsPostProcessing.Lighter : GraphicsPostProcessing.Full);

        profile.shadowDistance = level == GraphicsLevel.Low ? 20f
            : level == GraphicsLevel.High ? 70f
            : level == GraphicsLevel.Ultra ? 100f
            : 50f;
        profile.shadowCascadeCount = low ? 1 : 4;
        profile.mainLightShadowmapResolution = low ? 1024 : (ultra ? 4096 : 2048);
        profile.supportsSoftShadows = !low;
        profile.softShadowQuality = low ? 1 : 3;
        profile.additionalLightsPerObject = low ? 2 : level == GraphicsLevel.Medium ? 4 : level == GraphicsLevel.High ? 6 : 8;
        profile.additionalLightShadowsSupported = !low;
        profile.pointLightShadows = high;
        profile.glowLightCap = low ? 4 : 0;

        profile.grassDrawDistance = low ? 12f : level == GraphicsLevel.Medium ? 20f : level == GraphicsLevel.High ? 30f : 45f;
        profile.grassDensity = low ? 0.45f : 1f;
        profile.terrainPixelError = low ? 8f : level == GraphicsLevel.Medium ? 4f : level == GraphicsLevel.High ? 3f : 2f;
        profile.terrainBasemapDistance = low ? 150f : level == GraphicsLevel.Medium ? 280f : level == GraphicsLevel.High ? 400f : 600f;
        profile.treeLodBias = low ? 0.7f : level == GraphicsLevel.Medium ? 1f : level == GraphicsLevel.High ? 1.5f : 2f;
        profile.grassBending = !low;

        // PC quality level lodBias is 2 today. Medium stays 2. Low 1, High 3, Ultra 4.
        profile.lodBias = low ? 1f : level == GraphicsLevel.Medium ? 2f : level == GraphicsLevel.High ? 3f : 4f;

        profile.waterRipples = !low;
        profile.waterShoreFoam = !low;
        profile.waterDepthTexture = !low;
        profile.waterGlint = low ? 0.22f : ultra ? 0.85f : 0.55f;
        profile.waterDetailLayer = high;
        profile.waterRippleRange = low ? 0f : high ? 1.35f : 1f;
        profile.waterFoamReach = low ? 0f : ultra ? 12f : 8f;
        profile.waterDetailStrength = low ? 0f : 1f;
        profile.waterReflectionProbe = ultra;

        profile.keeperRim = !low;
        profile.chestFillLight = !low;
        profile.keeperSelfShadow = !low;
        profile.keeperAlwaysAnimate = high;
        profile.lanternSoftShadowOnKeeper = ultra;

        profile.distantMountains = !low;
        profile.hazeLayers = 1;
        profile.extraRidges = high;
        profile.finerHaze = ultra;
        profile.cameraFarPlane = low ? 600f : ultra ? 1400f : 900f;

        profile.particleCountMultiplier = low ? 0.5f : level == GraphicsLevel.High ? 1.25f : ultra ? 1.5f : 1f;
    }

    static UniversalRenderPipelineAsset EnsurePipeline(string assetName)
    {
        string path = UrpFolder + "/" + assetName + ".asset";
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (pipeline == null)
        {
            if (!AssetDatabase.CopyAsset(PcPipelinePath, path))
            {
                throw new IOException("Could not copy " + PcPipelinePath + " to " + path);
            }

            pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        }

        pipeline.name = assetName;
        ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
        SerializedObject so = new SerializedObject(pipeline);
        SerializedProperty list = so.FindProperty("m_RendererDataList");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        so.FindProperty("m_DefaultRendererIndex").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return pipeline;
    }

    static void ApplyPipeline(
        UniversalRenderPipelineAsset pipeline,
        float renderScale,
        string msaaName,
        bool hdr,
        float shadowDistance,
        int cascades,
        int shadowMap,
        bool softShadows,
        string softQualityName,
        int additionalLights,
        bool additionalShadows)
    {
        SerializedObject so = new SerializedObject(pipeline);
        so.FindProperty("m_RenderScale").floatValue = renderScale;
        SetEnumByName(so.FindProperty("m_MSAA"), msaaName);
        so.FindProperty("m_SupportsHDR").boolValue = hdr;
        so.FindProperty("m_ShadowDistance").floatValue = shadowDistance;
        so.FindProperty("m_ShadowCascadeCount").intValue = cascades;
        so.FindProperty("m_MainLightShadowmapResolution").intValue = shadowMap;
        so.FindProperty("m_SoftShadowsSupported").boolValue = softShadows;
        SetEnumByName(so.FindProperty("m_SoftShadowQuality"), softQualityName);
        so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = additionalLights;
        so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = additionalShadows;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
    }

    static void WriteQualityLevels(UniversalRenderPipelineAsset[] pipelines)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
        SerializedObject so = new SerializedObject(assets[0]);
        SerializedProperty levels = so.FindProperty("m_QualitySettings");
        int template = FindLevel(levels, "PC");
        if (template < 0)
        {
            template = FindLevel(levels, "Medium");
        }

        if (template < 0)
        {
            throw new IOException("QualitySettings has no PC or Medium level to copy.");
        }

        if (levels.arraySize != 4)
        {
            levels.arraySize = 4;
        }

        if (template >= levels.arraySize)
        {
            template = levels.arraySize - 1;
        }

        SerializedProperty source = levels.GetArrayElementAtIndex(template);
        for (int i = 0; i < levels.arraySize; i++)
        {
            if (i == template)
            {
                continue;
            }

            Assign(source, levels.GetArrayElementAtIndex(i));
        }

        float[] lodBias = { 1f, 2f, 3f, 4f };
        string[] names = { "Low", "Medium", "High", "Ultra" };
        for (int i = 0; i < 4; i++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(i);
            level.FindPropertyRelative("name").stringValue = names[i];
            level.FindPropertyRelative("lodBias").floatValue = lodBias[i];
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipelines[i];
        }

        so.FindProperty("m_CurrentQuality").intValue = (int)GraphicsLevel.Medium;
        SerializedProperty platforms = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < platforms.arraySize; i++)
        {
            SerializedProperty pair = platforms.GetArrayElementAtIndex(i);
            if (pair.FindPropertyRelative("first").stringValue == "Standalone")
            {
                pair.FindPropertyRelative("second").intValue = (int)GraphicsLevel.Medium;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(assets[0]);
        QualitySettings.SetQualityLevel((int)GraphicsLevel.Medium, true);
    }

    static int FindLevel(SerializedProperty levels, string levelName)
    {
        for (int i = 0; i < levels.arraySize; i++)
        {
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == levelName)
            {
                return i;
            }
        }

        return -1;
    }

    static void Assign(SerializedProperty src, SerializedProperty dst)
    {
        if (src.isArray && src.propertyType != SerializedPropertyType.String)
        {
            dst.arraySize = src.arraySize;
            for (int i = 0; i < src.arraySize; i++)
            {
                Assign(src.GetArrayElementAtIndex(i), dst.GetArrayElementAtIndex(i));
            }

            return;
        }

        switch (src.propertyType)
        {
            case SerializedPropertyType.Integer:
                dst.intValue = src.intValue;
                break;
            case SerializedPropertyType.Boolean:
                dst.boolValue = src.boolValue;
                break;
            case SerializedPropertyType.Float:
                dst.floatValue = src.floatValue;
                break;
            case SerializedPropertyType.String:
                dst.stringValue = src.stringValue;
                break;
            case SerializedPropertyType.Color:
                dst.colorValue = src.colorValue;
                break;
            case SerializedPropertyType.ObjectReference:
                dst.objectReferenceValue = src.objectReferenceValue;
                break;
            case SerializedPropertyType.Enum:
                dst.enumValueIndex = src.enumValueIndex;
                break;
            case SerializedPropertyType.Vector2:
                dst.vector2Value = src.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                dst.vector3Value = src.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                dst.vector4Value = src.vector4Value;
                break;
            case SerializedPropertyType.Quaternion:
                dst.quaternionValue = src.quaternionValue;
                break;
            default:
                if (src.hasChildren)
                {
                    SerializedProperty child = src.Copy();
                    SerializedProperty end = src.GetEndProperty();
                    if (!child.Next(true))
                    {
                        break;
                    }

                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        if (child.depth != src.depth + 1)
                        {
                            break;
                        }

                        SerializedProperty destChild = dst.FindPropertyRelative(child.name);
                        if (destChild != null)
                        {
                            Assign(child, destChild);
                        }

                        if (!child.Next(false))
                        {
                            break;
                        }
                    }
                }

                break;
        }
    }

    static void SetEnumByName(SerializedProperty property, string name)
    {
        string[] names = property.enumNames;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == name)
            {
                property.enumValueIndex = i;
                return;
            }
        }

        throw new IOException("Missing enum value " + name + " on " + property.propertyPath);
    }

    static GraphicsProfileSet LoadProfileSet()
    {
        GraphicsProfileSet set = AssetDatabase.LoadAssetAtPath<GraphicsProfileSet>(ProfileSetPath);
        if (set != null && ScriptAssigned(set))
        {
            return set;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(ProfileSetPath) != null)
        {
            AssetDatabase.DeleteAsset(ProfileSetPath);
        }

        set = LoadOrCreate<GraphicsProfileSet>(ProfileSetPath);
        if (!ScriptAssigned(set))
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Scripts/Graphics/GraphicsProfileSet.cs");
            SerializedObject serialized = new SerializedObject(set);
            serialized.FindProperty("m_Script").objectReferenceValue = script;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        return set;
    }

    static bool ScriptAssigned(Object asset)
    {
        SerializedObject serialized = new SerializedObject(asset);
        return serialized.FindProperty("m_Script").objectReferenceValue != null;
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
}
