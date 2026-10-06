using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
public class SettingsEffectTest
{
    const string Prefix = "LanternKeeper";
    const string Intro1 = "LanternKeeperIntro_island1";
    const string Intro4 = "LanternKeeperIntro_island4";

    const string GraphicsKey = "LanternKeeperGraphics";
    int savedGraphics;

    [SetUp]
    public void SetUp()
    {
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        // The exposure pulse only runs where post-processing is on, so use Ultra (3).
        PlayerPrefs.SetInt(GraphicsKey, 3);
        // Intro cards pause the game, so mark them seen.
        PlayerPrefs.SetInt(Intro1, 1);
        PlayerPrefs.SetInt(Intro4, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        PlayerPrefs.DeleteKey(Prefix + "MusicVolume");
        PlayerPrefs.DeleteKey(Prefix + "Brightness");
        PlayerPrefs.DeleteKey(Prefix + "ReduceFlashing");
        PlayerPrefs.Save();
    }

    static System.Type Settings()
    {
        System.Type t = System.Type.GetType("LanternKeeper.UserSettings, LanternKeeper.UI");
        Assert.IsNotNull(t, "UserSettings");
        return t;
    }

    static void SetSetting(string name, object value)
    {
        Settings().GetProperty(name).SetValue(null, value);
    }

    static AudioMixer Mixer()
    {
        System.Type t = System.Type.GetType("LanternKeeper.UserSettingsApplier, Assembly-CSharp");
        Assert.IsNotNull(t, "UserSettingsApplier");
        return (AudioMixer)t.GetProperty("Mixer").GetValue(null);
    }

    static ColorAdjustments Exposure(string volumeName)
    {
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            if (v.name == volumeName)
            {
                ColorAdjustments c;
                Assert.IsTrue(v.profile.TryGet(out c), volumeName + " has ColorAdjustments");
                return c;
            }
        }
        Assert.Fail("Volume not found: " + volumeName);
        return null;
    }

    static IEnumerator Load(string scene)
    {
        yield return SceneManager.LoadSceneAsync(scene);
        yield return null;
        yield return null;
    }

    static void Flash(Object lightning)
    {
        lightning.GetType().GetMethod("DebugFlash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lightning, null);
    }

    [UnityTest]
    public IEnumerator MusicSliderSetsMixer()
    {
        yield return Load("Island1");
        SetSetting("MusicVolume", 0.5f);
        AudioMixer mixer = Mixer();
        Assert.IsNotNull(mixer);
        foreach (string name in new[] { "MasterVol", "MusicVol", "SfxVol", "AmbienceVol" })
        {
            float unused;
            Assert.IsTrue(mixer.GetFloat(name, out unused), name + " is exposed");
        }
        float db;
        Assert.IsTrue(mixer.GetFloat("MusicVol", out db));
        Assert.AreEqual(-6.02f, db, 0.05f);

        // The Ducked snapshot lowers music by 8 dB on top of the user's volume, and un-ducking returns to it.
        System.Type am = System.Type.GetType("LanternKeeper.AudioManager, Assembly-CSharp");
        Object manager = Object.FindAnyObjectByType(am);
        Assert.IsNotNull(manager);
        am.GetMethod("TransitionMix").Invoke(manager, new object[] { true, "test" });
        yield return new WaitForSecondsRealtime(0.8f);
        mixer.GetFloat("MusicVol", out db);
        Assert.AreEqual(-6.02f - 8f, db, 0.1f);
        am.GetMethod("TransitionMix").Invoke(manager, new object[] { false, "test" });
        yield return new WaitForSecondsRealtime(0.8f);
        mixer.GetFloat("MusicVol", out db);
        Assert.AreEqual(-6.02f, db, 0.1f);
    }

    [UnityTest]
    public IEnumerator BrightnessShiftsExposure()
    {
        yield return Load("Island1");
        SetSetting("Brightness", 0f);
        float baseline = Exposure("LookVolume").postExposure.value;
        SetSetting("Brightness", 1f);
        float raised = Exposure("LookVolume").postExposure.value;
        Assert.AreEqual(1f, raised - baseline, 0.01f);
    }

    [UnityTest]
    public IEnumerator ReduceFlashingCapsLightning()
    {
        yield return Load("Island4");
        System.Type lt = System.Type.GetType("LanternKeeper.Lightning, Assembly-CSharp");
        Object lightning = Object.FindAnyObjectByType(lt);
        Assert.IsNotNull(lightning, "Lightning on Island 4");
        ColorAdjustments effects = Exposure("EffectsVolume");
        SetSetting("Brightness", 0f);

        SetSetting("ReduceFlashing", false);
        Flash(lightning);
        yield return null;
        float full = effects.postExposure.overrideState ? effects.postExposure.value : 0f;

        yield return new WaitForSeconds(1.2f);
        SetSetting("ReduceFlashing", true);
        Flash(lightning);
        yield return null;
        float capped = effects.postExposure.overrideState ? effects.postExposure.value : 0f;

        Assert.Greater(full, 0.5f, "an uncapped flash pulses the exposure");
        Assert.LessOrEqual(capped, full * 0.36f, "reduced flashing peaks at 35% or less");
        Assert.Greater(capped, 0f);
    }

    [UnityTest]
    public IEnumerator LightningReturnsToUserBrightness()
    {
        yield return Load("Island4");
        System.Type lt = System.Type.GetType("LanternKeeper.Lightning, Assembly-CSharp");
        Object lightning = Object.FindAnyObjectByType(lt);
        SetSetting("ReduceFlashing", false);
        SetSetting("Brightness", 0f);
        float baseline = Exposure("LookVolume").postExposure.value;
        SetSetting("Brightness", 0.5f);
        Flash(lightning);
        yield return null;
        yield return new WaitForSeconds(1.2f);
        ColorAdjustments effects = Exposure("EffectsVolume");
        Assert.IsFalse(effects.postExposure.overrideState, "the flash is over and the override is released");
        Assert.AreEqual(baseline + 0.5f, Exposure("LookVolume").postExposure.value, 0.01f);
    }
}
}
