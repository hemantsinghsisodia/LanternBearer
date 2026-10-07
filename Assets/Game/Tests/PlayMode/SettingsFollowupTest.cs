using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// Phase E follow-up: the legacy music mute no longer silences the game for good, and the brightness pass
// writes RenderSettings and lights only when something changed.
public class SettingsFollowupTest
{
    const string MusicKey = "LanternKeeperMusicVolume";
    const string MutedKey = "LanternKeeperMusicMuted";
    const string BrightnessKey = "LanternKeeperBrightness";
    const string GraphicsKey = "LanternKeeperGraphics";
    const string Intro1 = "LanternKeeperIntro_island1";

    bool hadMusic, hadMuted, hadBrightness, hadIntro1;
    float savedMusic, savedBrightness;
    int savedMuted, savedGraphics, savedIntro1;

    [SetUp]
    public void SetUp()
    {
        hadMusic = PlayerPrefs.HasKey(MusicKey);
        hadMuted = PlayerPrefs.HasKey(MutedKey);
        hadBrightness = PlayerPrefs.HasKey(BrightnessKey);
        hadIntro1 = PlayerPrefs.HasKey(Intro1);
        savedMusic = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        savedBrightness = PlayerPrefs.GetFloat(BrightnessKey, 0f);
        savedMuted = PlayerPrefs.GetInt(MutedKey, 0);
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        savedIntro1 = PlayerPrefs.GetInt(Intro1, 0);
        PlayerPrefs.SetInt(Intro1, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        if (hadIntro1) { PlayerPrefs.SetInt(Intro1, savedIntro1); } else { PlayerPrefs.DeleteKey(Intro1); }
        if (hadMuted) { PlayerPrefs.SetInt(MutedKey, savedMuted); } else { PlayerPrefs.DeleteKey(MutedKey); }
        UserSettings.MusicVolume = savedMusic;
        UserSettings.Brightness = savedBrightness;
        if (!hadMusic) { PlayerPrefs.DeleteKey(MusicKey); }
        if (!hadBrightness) { PlayerPrefs.DeleteKey(BrightnessKey); }
        PlayerPrefs.Save();
    }

    static System.Type Game(string name)
    {
        System.Type t = System.Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(t, name);
        return t;
    }

    static IEnumerator Load(string scene)
    {
        yield return SceneManager.LoadSceneAsync(scene);
        yield return null;
        yield return null;
    }

    static bool IsMuted()
    {
        return (bool)Game("MusicPlayer").GetProperty("IsMuted").GetValue(null);
    }

    static int LightWrites()
    {
        return (int)Game("UserSettingsApplier").GetProperty("LightWriteCount").GetValue(null);
    }

    [UnityTest]
    public IEnumerator LegacyMutedSaveRecoversThroughTheSlider()
    {
        yield return Load("Island1");
        PlayerPrefs.DeleteKey(MusicKey);
        PlayerPrefs.SetInt(MutedKey, 1);
        UserSettings.MigrateLegacy();
        Game("MusicPlayer").GetMethod("ReloadMute").Invoke(null, null);
        Assert.IsFalse(PlayerPrefs.HasKey(MutedKey), "legacy flag cleared");
        Assert.IsTrue(IsMuted(), "migrated to volume 0");

        UserSettings.MusicVolume = 0.5f;
        yield return null;
        Assert.IsFalse(IsMuted(), "raising the slider unmutes");
        AudioMixer mixer = (AudioMixer)Game("UserSettingsApplier").GetProperty("Mixer").GetValue(null);
        float db;
        Assert.IsTrue(mixer.GetFloat("MusicVol", out db));
        Assert.AreEqual(-6.02f, db, 0.1f, "mixer MusicVol follows the slider");
    }

    [UnityTest]
    public IEnumerator MuteKeyPathTogglesThroughMusicVolume()
    {
        yield return Load("Island1");
        UserSettings.MusicVolume = 0.6f;
        Game("MusicPlayer").GetMethod("ToggleMute").Invoke(null, null);
        yield return null;
        Assert.AreEqual(0f, UserSettings.MusicVolume, 0.0001f);
        Assert.IsTrue(IsMuted());
        AudioMixer mixer = (AudioMixer)Game("UserSettingsApplier").GetProperty("Mixer").GetValue(null);
        float db;
        mixer.GetFloat("MusicVol", out db);
        Assert.Less(db, -60f, "silent at the mixer");
        Game("MusicPlayer").GetMethod("ToggleMute").Invoke(null, null);
        yield return null;
        Assert.AreEqual(0.6f, UserSettings.MusicVolume, 0.0001f);
        Assert.IsFalse(IsMuted());
    }

    [UnityTest]
    public IEnumerator SteadyStateLightingPerformsNoWrites()
    {
        foreach (int graphics in new[] { 0, 3 })
        {
            foreach (float brightness in new[] { 0f, 0.5f })
            {
                PlayerPrefs.SetInt(GraphicsKey, graphics);
                yield return Load("Island1");
                UserSettings.Brightness = brightness;
                for (int i = 0; i < 5; i++)
                {
                    yield return null;
                }
                int before = LightWrites();
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                }
                Assert.AreEqual(before, LightWrites(), "graphics " + graphics + ", brightness " + brightness + ": no writes in steady state");
            }
        }
    }
}
}
