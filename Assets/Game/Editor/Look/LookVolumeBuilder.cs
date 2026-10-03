using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
// Generates each island's look volume profile, the dawn profile and the effects profile from the LookProfile and DawnLook assets.
// Idempotent: existing assets are updated in place, so their GUIDs and scene references survive.
public static class LookVolumeBuilder
{
    public const string Folder = "Assets/Game/Art/Look/Volumes";
    public const string DawnPath = Folder + "/LookVolume_dawn.asset";
    public const string EffectsPath = Folder + "/EffectsVolume.asset";
    const string DawnLookPath = "Assets/Game/Art/Look/DawnLook.asset";

    const float BloomThreshold = 1.1f;
    const float BloomIntensity = 0.6f;
    const float BloomScatter = 0.65f;
    const float VignetteIntensity = LookMapping.LookVignette;
    const float VignetteSmoothness = 0.42f;
    const float GrainIntensity = 0.12f;
    const float ShadowTint = 0.35f;
    const float Lift = 0.02f;

    public static string LookPath(string levelId)
    {
        return Folder + "/LookVolume_" + levelId + ".asset";
    }

    [MenuItem("Lantern Keeper/Look/Build Look Volumes")]
    public static void BuildMenu()
    {
        EnsureAll();
        Debug.Log("Look volumes built in " + Folder + ".");
    }

    public static void EnsureAll()
    {
        EnsureFolder();
        string[] guids = AssetDatabase.FindAssets("t:LookProfile");
        DawnLook dawn = AssetDatabase.LoadAssetAtPath<DawnLook>(DawnLookPath);
        for (int i = 0; i < guids.Length; i++)
        {
            LookProfile look = AssetDatabase.LoadAssetAtPath<LookProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (look == null || string.IsNullOrEmpty(look.levelId))
            {
                continue;
            }

            BuildLook(look);
            if (dawn == null)
            {
                dawn = look.dawn;
            }
        }

        if (dawn != null)
        {
            BuildDawn(dawn);
        }

        BuildEffects();
        AssetDatabase.SaveAssets();
    }

    public static VolumeProfile BuildLook(LookProfile look)
    {
        VolumeProfile profile = LoadOrCreate(LookPath(look.levelId));
        Color shadows = TintToward(look.sea, ShadowTint);
        Fill(profile, look.gradeSaturation, look.gradeContrast, look.gradeExposure, shadows, Color.white);
        return profile;
    }

    public static VolumeProfile BuildDawn(DawnLook dawn)
    {
        VolumeProfile profile = LoadOrCreate(DawnPath);
        Color shadows = TintToward(dawn.dawnTop, ShadowTint);
        Color highlights = Color.Lerp(Color.white, Normalised(dawn.dawnLight), 0.3f);
        Fill(profile, 12f, 8f, 0.1f, shadows, highlights);
        return profile;
    }

    public static VolumeProfile BuildEffects()
    {
        EnsureFolder();
        VolumeProfile profile = LoadOrCreate(EffectsPath);
        Vignette vignette = Component<Vignette>(profile);
        // Every override is saved off: LowFuelFX and Lightning switch them on only while active, so the look and dawn volumes show through.
        vignette.intensity.overrideState = false;
        vignette.intensity.value = 0f;
        ColorAdjustments color = Component<ColorAdjustments>(profile);
        color.postExposure.overrideState = false;
        color.postExposure.value = 0f;
        color.saturation.overrideState = false;
        color.saturation.value = 0f;
        Save(profile);
        return profile;
    }

    static void Fill(VolumeProfile profile, float saturation, float contrast, float exposure, Color shadows, Color highlights)
    {
        Tonemapping tone = Component<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments color = Component<ColorAdjustments>(profile);
        color.saturation.Override(saturation);
        color.contrast.Override(contrast);
        color.postExposure.Override(exposure);

        ShadowsMidtonesHighlights smh = Component<ShadowsMidtonesHighlights>(profile);
        smh.shadows.Override(new Vector4(shadows.r, shadows.g, shadows.b, 0f));
        smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        smh.highlights.Override(new Vector4(highlights.r, highlights.g, highlights.b, 0f));

        LiftGammaGain lgg = Component<LiftGammaGain>(profile);
        lgg.lift.Override(new Vector4(0.94f, 0.97f, 1f, Lift));
        lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
        lgg.gain.Override(new Vector4(1f, 1f, 1f, 0f));

        Bloom bloom = Component<Bloom>(profile);
        bloom.threshold.Override(BloomThreshold);
        bloom.intensity.Override(BloomIntensity);
        bloom.scatter.Override(BloomScatter);

        Vignette vignette = Component<Vignette>(profile);
        vignette.intensity.Override(VignetteIntensity);
        vignette.smoothness.Override(VignetteSmoothness);
        vignette.color.Override(Color.black);

        // Ultra only. LookVolumeQuality switches it on at runtime.
        FilmGrain grain = Component<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(GrainIntensity);
        grain.response.Override(0.8f);
        grain.active = false;

        Save(profile);
    }

    // Shadows lean toward the island's sea colour: brightened to full value, then blended in lightly from white.
    static Color TintToward(Color target, float amount)
    {
        return Color.Lerp(Color.white, Normalised(target), amount);
    }

    static Color Normalised(Color c)
    {
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, Mathf.Max(c.b, 0.0001f)));
        return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
    }

    static T Component<T>(VolumeProfile profile) where T : VolumeComponent
    {
        for (int i = profile.components.Count - 1; i >= 0; i--)
        {
            if (profile.components[i] == null)
            {
                profile.components.RemoveAt(i);
            }
        }

        T component;
        if (profile.TryGet(out component))
        {
            return component;
        }

        component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static VolumeProfile LoadOrCreate(string path)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null)
        {
            return profile;
        }

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    static void Save(VolumeProfile profile)
    {
        for (int i = 0; i < profile.components.Count; i++)
        {
            EditorUtility.SetDirty(profile.components[i]);
        }

        EditorUtility.SetDirty(profile);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Art/Look"))
        {
            AssetDatabase.CreateFolder("Assets/Game/Art", "Look");
        }

        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Game/Art/Look", "Volumes");
        }
    }
}
}
