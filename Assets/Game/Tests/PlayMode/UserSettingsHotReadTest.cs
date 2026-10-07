using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// Pins the Island 4 perf fix: Lightning.CurrentFlash and the brightness and text scale readers run dozens of times per frame,
// and each PlayerPrefs read costs ~15 microseconds on Windows. In play mode the three hot settings must touch PlayerPrefs
// at most once per frame, and setters must still show up immediately.
public class UserSettingsHotReadTest
{
    const string BrightnessKey = "LanternKeeperBrightness";
    const string ReduceKey = "LanternKeeperReduceFlashing";
    const string ScaleKey = "LanternKeeperTextScale";

    bool hadBrightness, hadReduce, hadScale;
    float savedBrightness, savedScale;
    int savedReduce;

    [SetUp]
    public void SetUp()
    {
        hadBrightness = PlayerPrefs.HasKey(BrightnessKey);
        hadReduce = PlayerPrefs.HasKey(ReduceKey);
        hadScale = PlayerPrefs.HasKey(ScaleKey);
        savedBrightness = PlayerPrefs.GetFloat(BrightnessKey, 0f);
        savedReduce = PlayerPrefs.GetInt(ReduceKey, 0);
        savedScale = PlayerPrefs.GetFloat(ScaleKey, 1f);
    }

    [TearDown]
    public void TearDown()
    {
        Restore(BrightnessKey, hadBrightness, savedBrightness);
        Restore(ScaleKey, hadScale, savedScale);
        if (hadReduce) { PlayerPrefs.SetInt(ReduceKey, savedReduce); } else { PlayerPrefs.DeleteKey(ReduceKey); }
        UserSettings.InvalidateCache();
    }

    static void Restore(string key, bool had, float value)
    {
        if (had) { PlayerPrefs.SetFloat(key, value); } else { PlayerPrefs.DeleteKey(key); }
    }

    [UnityTest]
    public IEnumerator HotSettingsReadPlayerPrefsAtMostOncePerFrame()
    {
        yield return null;
        int before = UserSettings.HotReadCount;
        for (int i = 0; i < 1000; i++)
        {
            float b = UserSettings.Brightness;
            float s = UserSettings.TextScale;
            bool r = UserSettings.ReduceFlashing;
            Assert.IsTrue(b >= -1f && s > 0f && (r || !r));
        }
        Assert.LessOrEqual(UserSettings.HotReadCount - before, 1, "1000 reads in one frame refreshed PlayerPrefs more than once");

        yield return null;
        before = UserSettings.HotReadCount;
        for (int i = 0; i < 1000; i++)
        {
            float b = UserSettings.Brightness;
            Assert.IsTrue(b >= -1f);
        }
        Assert.LessOrEqual(UserSettings.HotReadCount - before, 1, "second frame refreshed more than once");
    }

    [UnityTest]
    public IEnumerator SettersAreVisibleImmediatelyAndOutsideChangesAfterOneFrame()
    {
        yield return null;
        UserSettings.Brightness = 0.5f;
        Assert.AreEqual(0.5f, UserSettings.Brightness, 0.0001f);
        UserSettings.ReduceFlashing = true;
        Assert.IsTrue(UserSettings.ReduceFlashing);
        UserSettings.ReduceFlashing = false;
        Assert.IsFalse(UserSettings.ReduceFlashing);

        PlayerPrefs.SetFloat(BrightnessKey, -0.25f);
        yield return null;
        Assert.AreEqual(-0.25f, UserSettings.Brightness, 0.0001f);
    }
}
}
