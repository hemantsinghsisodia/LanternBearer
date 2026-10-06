using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
public class SettingsEffectTest
{
    const string Prefix = "LanternKeeper";
    const string Intro1 = "LanternKeeperIntro_island1";
    const string Intro4 = "LanternKeeperIntro_island4";
    const string GraphicsKey = "LanternKeeperGraphics";

    int savedGraphics;
    bool hadIntro1;
    bool hadIntro4;
    int savedIntro1;
    int savedIntro4;
    bool hadMusic;
    bool hadBrightness;
    bool hadReduce;
    float savedMusic;
    float savedBrightness;
    bool savedReduce;

    [SetUp]
    public void SetUp()
    {
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        hadIntro1 = PlayerPrefs.HasKey(Intro1);
        hadIntro4 = PlayerPrefs.HasKey(Intro4);
        savedIntro1 = PlayerPrefs.GetInt(Intro1, 0);
        savedIntro4 = PlayerPrefs.GetInt(Intro4, 0);
        hadMusic = PlayerPrefs.HasKey(Prefix + "MusicVolume");
        hadBrightness = PlayerPrefs.HasKey(Prefix + "Brightness");
        hadReduce = PlayerPrefs.HasKey(Prefix + "ReduceFlashing");
        savedMusic = (float)GetSetting("MusicVolume");
        savedBrightness = (float)GetSetting("Brightness");
        savedReduce = (bool)GetSetting("ReduceFlashing");
        // The exposure pulse only runs where post-processing is on, so default to Ultra (3).
        SetGraphics(3);
        // Intro cards pause the game, so mark them seen.
        PlayerPrefs.SetInt(Intro1, 1);
        PlayerPrefs.SetInt(Intro4, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        Restore(Intro1, hadIntro1, savedIntro1);
        Restore(Intro4, hadIntro4, savedIntro4);
        // Through UserSettings so every listener sees the restored values; absent keys are deleted afterwards.
        SetSetting("MusicVolume", savedMusic);
        SetSetting("Brightness", savedBrightness);
        SetSetting("ReduceFlashing", savedReduce);
        if (!hadMusic) { PlayerPrefs.DeleteKey(Prefix + "MusicVolume"); }
        if (!hadBrightness) { PlayerPrefs.DeleteKey(Prefix + "Brightness"); }
        if (!hadReduce) { PlayerPrefs.DeleteKey(Prefix + "ReduceFlashing"); }
        PlayerPrefs.Save();
    }

    static void Restore(string key, bool had, int value)
    {
        if (had)
        {
            PlayerPrefs.SetInt(key, value);
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    static void SetGraphics(int level)
    {
        PlayerPrefs.SetInt(GraphicsKey, level);
    }

    static System.Type Settings()
    {
        System.Type t = System.Type.GetType("LanternKeeper.UserSettings, LanternKeeper.UI");
        Assert.IsNotNull(t, "UserSettings");
        return t;
    }

    static object GetSetting(string name)
    {
        return Settings().GetProperty(name).GetValue(null);
    }

    static void SetSetting(string name, object value)
    {
        Settings().GetProperty(name).SetValue(null, value);
    }

    static System.Type Game(string name)
    {
        System.Type t = System.Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(t, name);
        return t;
    }

    static AudioMixer Mixer()
    {
        return (AudioMixer)Game("UserSettingsApplier").GetProperty("Mixer").GetValue(null);
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

    static void Mix(bool ducked)
    {
        System.Type am = Game("AudioManager");
        Object manager = Object.FindAnyObjectByType(am);
        Assert.IsNotNull(manager);
        am.GetMethod("TransitionMix").Invoke(manager, new object[] { ducked, "test" });
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
        Mix(true);
        yield return new WaitForSecondsRealtime(0.8f);
        mixer.GetFloat("MusicVol", out db);
        Assert.AreEqual(-6.02f - 8f, db, 0.1f);
        Mix(false);
        yield return new WaitForSecondsRealtime(0.8f);
        mixer.GetFloat("MusicVol", out db);
        Assert.AreEqual(-6.02f, db, 0.1f);
    }

    [UnityTest]
    public IEnumerator DuckResetsOnSceneLoad()
    {
        yield return Load("Island1");
        SetSetting("MusicVolume", 0.5f);
        AudioMixer mixer = Mixer();
        Mix(true);
        yield return new WaitForSecondsRealtime(0.8f);
        float db;
        mixer.GetFloat("MusicVol", out db);
        Assert.AreEqual(-6.02f - 8f, db, 0.1f, "ducked before the load");
        yield return Load("Island1");
        yield return new WaitForSecondsRealtime(0.3f);
        mixer.GetFloat("MusicVol", out db);
        Assert.AreEqual(-6.02f, db, 0.1f, "a scene load clears the duck");
    }

    [UnityTest]
    public IEnumerator BrightnessShiftsExposure()
    {
        yield return Load("Island1");
        SetSetting("Brightness", 0f);
        yield return null;
        float baseline = Exposure("LookVolume").postExposure.value;
        float moon0 = RenderSettings.sun.intensity;
        SetSetting("Brightness", 1f);
        yield return null;
        yield return null;
        float raised = Exposure("LookVolume").postExposure.value;
        Assert.AreEqual(1f, raised - baseline, 0.01f);
        Assert.AreEqual(moon0, RenderSettings.sun.intensity, 0.0001f, "with post-processing on the lights are not also scaled");
    }

    [UnityTest]
    public IEnumerator BrightnessWorksOnLow()
    {
        SetGraphics(0);
        yield return Load("Island1");
        SetSetting("Brightness", 0f);
        yield return null;
        yield return null;
        Light moon = RenderSettings.sun;
        Assert.IsNotNull(moon);
        float moon0 = moon.intensity;
        float ambient0 = RenderSettings.ambientIntensity;
        Assert.Greater(moon0, 0f);

        SetSetting("Brightness", 1f);
        yield return null;
        yield return null;
        Assert.AreEqual(moon0 * 2f, moon.intensity, moon0 * 0.02f, "moonlight is about 2x authored");
        Assert.AreEqual(ambient0 * 2f, RenderSettings.ambientIntensity, ambient0 * 0.02f, "ambient is about 2x authored");

        SetSetting("Brightness", 0f);
        yield return null;
        yield return null;
        Assert.AreEqual(moon0, moon.intensity, 0.00001f, "brightness 0 restores the authored moonlight");
        Assert.AreEqual(ambient0, RenderSettings.ambientIntensity, 0.00001f);
    }

    // Peak exposure written by the effects volume over a flash, sampled every frame.
    static IEnumerator PeakOver(ColorAdjustments effects, float seconds, float[] result)
    {
        float end = Time.time + seconds;
        float peak = 0f;
        while (Time.time < end)
        {
            if (effects.postExposure.overrideState)
            {
                peak = Mathf.Max(peak, effects.postExposure.value);
            }
            yield return null;
        }
        result[0] = peak;
    }

    [UnityTest]
    public IEnumerator ReduceFlashingCapsLightning()
    {
        yield return Load("Island4");
        System.Type lt = Game("Lightning");
        Object lightning = Object.FindAnyObjectByType(lt);
        Assert.IsNotNull(lightning, "Lightning on Island 4");
        ColorAdjustments effects = Exposure("EffectsVolume");
        SetSetting("Brightness", 0f);

        float[] full = new float[1];
        float[] capped = new float[1];
        SetSetting("ReduceFlashing", false);
        Flash(lightning);
        yield return PeakOver(effects, 0.4f, full);

        yield return new WaitForSeconds(1f);
        SetSetting("ReduceFlashing", true);
        Flash(lightning);
        yield return PeakOver(effects, 0.4f, capped);

        Assert.Greater(full[0], 0.5f, "an uncapped flash pulses the exposure");
        Assert.LessOrEqual(capped[0], full[0] * 0.36f, "reduced flashing peaks at 35% or less");
        Assert.Greater(capped[0], 0f);
    }

    [UnityTest]
    public IEnumerator LightningReturnsToUserBrightness()
    {
        yield return Load("Island4");
        System.Type lt = Game("Lightning");
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

    static GameObject Named(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t.gameObject;
            }
        }
        Assert.Fail("Not found: " + name);
        return null;
    }

    [UnityTest]
    public IEnumerator BackWalksOutOfSettingsOneLevelAtATime()
    {
        yield return Load("MainMenu");
        System.Type st = Game("SettingsScreen");
        Component screen = (Component)Object.FindAnyObjectByType(st, FindObjectsInactive.Include);
        Assert.IsNotNull(screen, "SettingsScreen in MainMenu");
        Button opener = Named(Object.FindAnyObjectByType<Canvas>().transform, "GraphicsButton").GetComponent<Button>();
        bool closedCalled = false;
        st.GetMethod("Open").Invoke(screen, new object[] { opener, (System.Action)(() => closedCalled = true) });
        Assert.IsTrue((bool)st.GetProperty("IsOpen").GetValue(screen));
        EventSystem system = EventSystem.current;

        GameObject audioTab = Named(screen.transform, "Section_Audio");
        system.SetSelectedGameObject(audioTab);
        Assert.IsTrue(Named(screen.transform, "Panel_Audio").activeSelf, "selecting a tab shows its section");
        GameObject slider = Named(Named(screen.transform, "Panel_Audio").transform, "Slider");
        system.SetSelectedGameObject(slider);

        MethodInfo back = st.GetMethod("ConsumeBack");
        Assert.IsTrue((bool)back.Invoke(screen, null));
        Assert.AreSame(audioTab, system.currentSelectedGameObject, "Back from a slider returns to the Audio tab");
        Assert.IsTrue((bool)st.GetProperty("IsOpen").GetValue(screen));

        Assert.IsTrue((bool)back.Invoke(screen, null));
        Assert.IsFalse((bool)st.GetProperty("IsOpen").GetValue(screen), "Back from the tab list closes Settings");
        Assert.AreSame(opener.gameObject, system.currentSelectedGameObject, "the opener is reselected");
        Assert.IsTrue(closedCalled);
        yield return null;
    }
}
}
