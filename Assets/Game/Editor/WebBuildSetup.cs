using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
// One-shot, idempotent setup for the WebGL (browser) player. Everything here is WebGL-only: the Windows quality levels,
// pipeline assets and player settings are never changed.
//   Lantern Keeper/Web/Setup Renderer and Quality   : Web_Renderer, Web_URP, Web_URP_Low and four web-only quality levels
//   Lantern Keeper/Web/Setup Player Settings        : WebGL2, Brotli, memory, exceptions, stripping, template
// Quality levels: the four desktop levels (Low..Ultra) are excluded from WebGL, and four "Web" levels (same order) are
// excluded from every other platform. QualitySettings.names therefore has the same four entries on every platform, so
// GraphicsQuality's level index keeps working unchanged.
public static class WebBuildSetup
{
    const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
    const string WebRendererPath = "Assets/Settings/Web_Renderer.asset";
    const string WebUrpPath = "Assets/Settings/Web_URP.asset";
    const string WebUrpLowPath = "Assets/Settings/Web_URP_Low.asset";
    const string HighUrpPath = "Assets/Settings/Graphics/URP_High.asset";
    const string LowUrpPath = "Assets/Settings/Graphics/URP_Low.asset";
    const string QualityAssetPath = "ProjectSettings/QualitySettings.asset";
    const string WebTemplate = "PROJECT:LanternKeeper";

    static readonly string[] WebLevelNames = { "Web Low", "Web Medium", "Web High", "Web Ultra" };

    // Every platform name Unity's quality settings know, except WebGL.
    static readonly string[] NonWebPlatforms =
    {
        "Standalone", "Android", "AndroidXR", "iPhone", "tvOS", "GameCoreScarlett", "GameCoreXboxOne", "Lumin", "MetaQuest",
        "Nintendo Switch", "Nintendo Switch 2", "PS4", "PS5", "Server", "Stadia", "Windows Store Apps", "XboxOne"
    };

    [MenuItem("Lantern Keeper/Web/Setup Renderer and Quality")]
    public static void SetupRendererAndQuality()
    {
        ScriptableRendererData renderer = EnsureWebRenderer();
        UniversalRenderPipelineAsset web = EnsureWebPipeline(WebUrpPath, HighUrpPath, renderer, false);
        UniversalRenderPipelineAsset low = EnsureWebPipeline(WebUrpLowPath, LowUrpPath, renderer, true);
        EnsureQualityLevels(new[] { low, web, web, web });
        AssetDatabase.SaveAssets();
        Debug.Log("Web renderer and quality levels ready.");
    }

    // PC_Renderer without the SSAO feature, switched to plain Forward. Forward+ must not be used on WebGL: its lit fragment
    // shaders (cluster loops over urp_ZBins/urp_Tiles, about 70 KB of GLSL) make the browser's ANGLE/D3D11 shader compile
    // kill the GPU process, so the WebGL context is lost and the canvas stays black. MoonRim stays: it needs only the depth texture.
    const int ForwardRenderingMode = 0;
    static ScriptableRendererData EnsureWebRenderer()
    {
        ScriptableRendererData existing = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(WebRendererPath);
        if (existing != null)
        {
            SetForwardRendering(existing);
            return existing;
        }
        if (!AssetDatabase.CopyAsset(PcRendererPath, WebRendererPath))
        {
            throw new System.InvalidOperationException("Could not copy " + PcRendererPath);
        }
        AssetDatabase.ImportAsset(WebRendererPath);
        ScriptableRendererData data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(WebRendererPath);
        data.name = "Web_Renderer";

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(WebRendererPath);
        foreach (Object sub in all)
        {
            ScriptableRendererFeature feature = sub as ScriptableRendererFeature;
            if (feature != null && feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
            {
                data.rendererFeatures.Remove(feature);
                AssetDatabase.RemoveObjectFromAsset(feature);
                Object.DestroyImmediate(feature, true);
            }
        }

        // Rebuild the feature id map that the editor keeps next to the list.
        SerializedObject so = new SerializedObject(data);
        SerializedProperty list = so.FindProperty("m_RendererFeatures");
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                list.DeleteArrayElementAtIndex(i);
            }
        }
        map.ClearArray();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object feature = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (feature == null)
            {
                continue;
            }
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            map.InsertArrayElementAtIndex(map.arraySize);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        SetForwardRendering(data);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    static void SetForwardRendering(ScriptableRendererData data)
    {
        SerializedObject so = new SerializedObject(data);
        SerializedProperty mode = so.FindProperty("m_RenderingMode");
        if (mode != null && mode.intValue != ForwardRenderingMode)
        {
            mode.intValue = ForwardRenderingMode;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
    }

    static UniversalRenderPipelineAsset EnsureWebPipeline(string path, string sourcePath, ScriptableRendererData renderer, bool low)
    {
        UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (asset == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                throw new System.InvalidOperationException("Could not copy " + sourcePath);
            }
            asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        }

        SerializedObject so = new SerializedObject(asset);
        SerializedProperty list = so.FindProperty("m_RendererDataList");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        so.FindProperty("m_DefaultRendererIndex").intValue = 0;
        // MSAA 2x (4x on desktop High): the enum stores the sample count.
        so.FindProperty("m_MSAA").intValue = low ? 1 : 2;
        // HDR stays on outside Low: the lantern glow and bloom lean on values above 1.
        so.FindProperty("m_SupportsHDR").boolValue = !low;
        so.FindProperty("m_ShadowDistance").floatValue = low ? 8f : 40f;
        so.FindProperty("m_ShadowCascadeCount").intValue = low ? 1 : 2;
        so.FindProperty("m_MainLightShadowmapResolution").intValue = low ? 1024 : 2048;
        so.FindProperty("m_SoftShadowQuality").intValue = 1;
        // Point-light shadows are the most expensive thing on the web and the beacons do not need them there.
        so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        so.FindProperty("m_SupportsLightCookies").boolValue = false;
        so.FindProperty("m_SupportsLightLayers").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void EnsureQualityLevels(UniversalRenderPipelineAsset[] webPipelines)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(QualityAssetPath);
        SerializedObject so = new SerializedObject(assets[0]);
        SerializedProperty levels = so.FindProperty("m_QualitySettings");

        // Desktop levels keep their data; they only gain WebGL in their exclusion list.
        int desktopCount = 0;
        for (int i = 0; i < levels.arraySize; i++)
        {
            string name = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            if (!name.StartsWith("Web "))
            {
                desktopCount++;
            }
        }
        if (desktopCount != 4)
        {
            throw new System.InvalidOperationException("Expected 4 desktop quality levels, found " + desktopCount);
        }

        for (int i = 0; i < 4; i++)
        {
            AddExclusion(levels.GetArrayElementAtIndex(i), "WebGL");
        }

        for (int i = 0; i < 4; i++)
        {
            int existing = FindLevel(levels, WebLevelNames[i]);
            if (existing < 0)
            {
                levels.InsertArrayElementAtIndex(levels.arraySize - 1);
                existing = levels.arraySize - 1;
            }
            // Always copy from the matching desktop level so a rerun picks up later desktop changes.
            CopyLevel(levels.GetArrayElementAtIndex(i), levels.GetArrayElementAtIndex(existing));
            SerializedProperty level = levels.GetArrayElementAtIndex(existing);
            level.FindPropertyRelative("name").stringValue = WebLevelNames[i];
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = webPipelines[i];
            level.FindPropertyRelative("shadowCascades").intValue = i == 0 ? 1 : 2;
            level.FindPropertyRelative("shadowDistance").floatValue = i == 0 ? 8f : 40f;
            SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            excluded.arraySize = NonWebPlatforms.Length;
            for (int p = 0; p < NonWebPlatforms.Length; p++)
            {
                excluded.GetArrayElementAtIndex(p).stringValue = NonWebPlatforms[p];
            }
        }

        // The default web level is High (index 2 of the four web levels). Desktop defaults are untouched.
        SerializedProperty platforms = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < platforms.arraySize; i++)
        {
            SerializedProperty pair = platforms.GetArrayElementAtIndex(i);
            if (pair.FindPropertyRelative("first").stringValue == "WebGL")
            {
                pair.FindPropertyRelative("second").intValue = GraphicsDefaults.HighLevel;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(assets[0]);
    }

    static void AddExclusion(SerializedProperty level, string platform)
    {
        SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");
        for (int i = 0; i < excluded.arraySize; i++)
        {
            if (excluded.GetArrayElementAtIndex(i).stringValue == platform)
            {
                return;
            }
        }
        excluded.InsertArrayElementAtIndex(excluded.arraySize);
        excluded.GetArrayElementAtIndex(excluded.arraySize - 1).stringValue = platform;
    }

    static int FindLevel(SerializedProperty levels, string name)
    {
        for (int i = 0; i < levels.arraySize; i++)
        {
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
            {
                return i;
            }
        }
        return -1;
    }

    static void CopyLevel(SerializedProperty source, SerializedProperty destination)
    {
        SerializedProperty src = source.Copy();
        SerializedProperty end = source.GetEndProperty();
        bool enter = true;
        while (src.Next(enter) && !SerializedProperty.EqualContents(src, end))
        {
            enter = false;
            if (src.name == "excludedTargetPlatforms" || src.name == "name" || src.name == "customRenderPipeline")
            {
                continue;
            }
            SerializedProperty dst = destination.FindPropertyRelative(src.name);
            if (dst == null)
            {
                continue;
            }
            switch (src.propertyType)
            {
                case SerializedPropertyType.Integer: dst.intValue = src.intValue; break;
                case SerializedPropertyType.Boolean: dst.boolValue = src.boolValue; break;
                case SerializedPropertyType.Float: dst.floatValue = src.floatValue; break;
                case SerializedPropertyType.Vector3: dst.vector3Value = src.vector3Value; break;
                case SerializedPropertyType.Enum: dst.enumValueIndex = src.enumValueIndex; break;
                case SerializedPropertyType.ObjectReference: dst.objectReferenceValue = src.objectReferenceValue; break;
            }
        }
    }

    [MenuItem("Lantern Keeper/Web/Setup Player Settings")]
    public static void SetupPlayerSettings()
    {
        // WebGL2 only. WebGPU stays off for the widest browser support.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
        PlayerSettings.WebGL.wasmArithmeticExceptions = WebGLWasmArithmeticExceptions.Ignore;
        PlayerSettings.WebGL.analyzeBuildSize = false;

        // 512 MB to start and growth up to 2 GB.
        PlayerSettings.WebGL.initialMemorySize = 512;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;

        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.WebGL.template = WebTemplate;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        // Desktop browsers: DXT (S3TC). ASTC is not available in most desktop browsers on Windows.
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
        AssetDatabase.SaveAssets();
        Debug.Log("WebGL player settings applied.");
    }

    // Reported by the execute_code diagnostics.
    public static string Describe()
    {
        List<string> lines = new List<string>();
        lines.Add("compression " + PlayerSettings.WebGL.compressionFormat + " fallback " + PlayerSettings.WebGL.decompressionFallback);
        lines.Add("memory " + PlayerSettings.WebGL.initialMemorySize + "/" + PlayerSettings.WebGL.maximumMemorySize + " " + PlayerSettings.WebGL.memoryGrowthMode);
        lines.Add("exceptions " + PlayerSettings.WebGL.exceptionSupport);
        lines.Add("stripping " + PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL));
        lines.Add("template " + PlayerSettings.WebGL.template);
        return string.Join("\n", lines);
    }
}
}
