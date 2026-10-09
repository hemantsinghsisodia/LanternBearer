using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The Render scale setting multiplies the preset's scale on the active URP asset, and putting it back restores the preset.
public class RenderScaleTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    const string ScaleKey = "LanternKeeperRenderScale";

    int savedGraphics;
    bool hadGraphics;
    bool hadScale;
    float savedScale;

    static Type Quality()
    {
        return Type.GetType("LanternKeeper.GraphicsQuality, Assembly-CSharp");
    }

    static void SetLevel(int level)
    {
        Type levelType = Type.GetType("LanternKeeper.GraphicsLevel, Assembly-CSharp");
        Quality().GetMethod("Set").Invoke(null, new object[] { Enum.ToObject(levelType, level) });
    }

    static float PresetScale()
    {
        object profile = Quality().GetProperty("Profile").GetValue(null);
        return (float)profile.GetType().GetField("renderScale").GetValue(profile);
    }

    static UniversalRenderPipelineAsset Asset()
    {
        return (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
    }

    [SetUp]
    public void SetUp()
    {
        hadGraphics = PlayerPrefs.HasKey(GraphicsKey);
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 1);
        hadScale = PlayerPrefs.HasKey(ScaleKey);
        savedScale = PlayerPrefs.GetFloat(ScaleKey, 1f);
    }

    [TearDown]
    public void TearDown()
    {
        UserSettings.RenderScale = savedScale;
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        SetLevel(savedGraphics);
        if (!hadScale) { PlayerPrefs.DeleteKey(ScaleKey); }
        if (!hadGraphics) { PlayerPrefs.DeleteKey(GraphicsKey); }
        PlayerPrefs.Save();
        Quality().GetMethod("RestorePresetScales").Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator SettingMultipliesPresetAndRestores()
    {
        UserSettings.RenderScale = 1f;
        SetLevel(1);
        yield return null;
        Assert.IsNotNull(Asset(), "an URP asset is active");
        float preset = PresetScale();
        Assert.AreEqual(preset, Asset().renderScale, 0.0001f, "default setting leaves the preset scale");

        UserSettings.RenderScale = 1.5f;
        Assert.AreEqual(preset * 1.5f, Asset().renderScale, 0.0001f, "preset x setting");

        UserSettings.RenderScale = 2f;
        Assert.AreEqual(Mathf.Min(preset * 2f, 2f), Asset().renderScale, 0.0001f);

        // A preset change keeps the setting.
        SetLevel(0);
        yield return null;
        Assert.AreEqual(SettingsMath.EffectiveRenderScale(PresetScale(), 2f), Asset().renderScale, 0.0001f, "Low x 200%");
        Assert.AreEqual(1f, Asset().renderScale, 0.0001f, "Low is 0.5 x 2");

        UserSettings.RenderScale = 1f;
        Assert.AreEqual(PresetScale(), Asset().renderScale, 0.0001f, "restoring the default restores the preset value");
        Assert.AreEqual(0.5f, Asset().renderScale, 0.0001f);

        SetLevel(1);
        yield return null;
        Assert.AreEqual(PresetScale(), Asset().renderScale, 0.0001f);
    }
}
}
