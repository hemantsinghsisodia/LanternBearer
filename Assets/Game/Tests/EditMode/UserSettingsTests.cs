using System;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class UserSettingsTests
{
    static readonly string[] Keys =
    {
        "LanternKeeperMasterVolume", "LanternKeeperMusicVolume", "LanternKeeperEffectsVolume",
        "LanternKeeperAmbienceVolume", "LanternKeeperBrightness", "LanternKeeperTextScale",
        "LanternKeeperReduceFlashing", "LanternKeeperWindowMode", "LanternKeeperResWidth",
        "LanternKeeperResHeight", "LanternKeeperRefreshHz", "LanternKeeperMusicMuted"
    };

    static void Clear()
    {
        foreach (string k in Keys)
        {
            PlayerPrefs.DeleteKey(k);
        }
    }

    [SetUp]
    public void SetUp()
    {
        Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Clear();
    }

    [Test]
    public void DefaultsAreSpecValues()
    {
        Assert.AreEqual(0.8f, UserSettings.MasterVolume, 0.0001f);
        Assert.AreEqual(0.8f, UserSettings.MusicVolume, 0.0001f);
        Assert.AreEqual(0.8f, UserSettings.EffectsVolume, 0.0001f);
        Assert.AreEqual(0.8f, UserSettings.AmbienceVolume, 0.0001f);
        Assert.AreEqual(0f, UserSettings.Brightness, 0.0001f);
        Assert.AreEqual(1f, UserSettings.TextScale, 0.0001f);
        Assert.IsFalse(UserSettings.ReduceFlashing);
    }

    [Test]
    public void RoundTripsEveryKey()
    {
        UserSettings.MasterVolume = 0.1f;
        UserSettings.MusicVolume = 0.2f;
        UserSettings.EffectsVolume = 0.3f;
        UserSettings.AmbienceVolume = 0.4f;
        UserSettings.Brightness = -0.5f;
        UserSettings.TextScale = 1.3f;
        UserSettings.ReduceFlashing = true;
        UserSettings.WindowMode = FullScreenMode.Windowed;
        UserSettings.ResolutionWidth = 1280;
        UserSettings.ResolutionHeight = 720;
        UserSettings.RefreshRateHz = 75;
        Assert.AreEqual(0.1f, UserSettings.MasterVolume, 0.0001f);
        Assert.AreEqual(0.2f, UserSettings.MusicVolume, 0.0001f);
        Assert.AreEqual(0.3f, UserSettings.EffectsVolume, 0.0001f);
        Assert.AreEqual(0.4f, UserSettings.AmbienceVolume, 0.0001f);
        Assert.AreEqual(-0.5f, UserSettings.Brightness, 0.0001f);
        Assert.AreEqual(1.3f, UserSettings.TextScale, 0.0001f);
        Assert.IsTrue(UserSettings.ReduceFlashing);
        Assert.AreEqual(FullScreenMode.Windowed, UserSettings.WindowMode);
        Assert.AreEqual(1280, UserSettings.ResolutionWidth);
        Assert.AreEqual(720, UserSettings.ResolutionHeight);
        Assert.AreEqual(75, UserSettings.RefreshRateHz);
    }

    [Test]
    public void TextScaleSnaps()
    {
        UserSettings.TextScale = 1.2f;
        Assert.AreEqual(1.15f, UserSettings.TextScale, 0.0001f);
    }

    [Test]
    public void MutedMusicMigratesToZero()
    {
        PlayerPrefs.SetInt("LanternKeeperMusicMuted", 1);
        UserSettings.MigrateLegacy();
        Assert.AreEqual(0f, UserSettings.MusicVolume, 0.0001f);
    }

    [Test]
    public void MigrationKeepsExplicitMusicVolume()
    {
        PlayerPrefs.SetInt("LanternKeeperMusicMuted", 1);
        UserSettings.MusicVolume = 0.6f;
        UserSettings.MigrateLegacy();
        Assert.AreEqual(0.6f, UserSettings.MusicVolume, 0.0001f);
    }

    static Resolution Res(int w, int h)
    {
        Resolution r = new Resolution();
        r.width = w;
        r.height = h;
        r.refreshRateRatio = new RefreshRate { numerator = 60, denominator = 1 };
        return r;
    }

    [Test]
    public void InvalidSavedResolutionFallsBack()
    {
        UserSettings.ResolutionWidth = 9999;
        UserSettings.ResolutionHeight = 9999;
        Resolution[] supported = { Res(1920, 1080), Res(1280, 720) };
        Resolution result = UserSettings.ResolveResolution(supported, Res(1920, 1080));
        Assert.AreEqual(1920, result.width);
        Assert.AreEqual(1080, result.height);
    }

    [Test]
    public void SupportedSavedResolutionIsKept()
    {
        UserSettings.ResolutionWidth = 1280;
        UserSettings.ResolutionHeight = 720;
        Resolution[] supported = { Res(1920, 1080), Res(1280, 720) };
        Resolution result = UserSettings.ResolveResolution(supported, Res(1920, 1080));
        Assert.AreEqual(1280, result.width);
    }

    [Test]
    public void ChangedFires()
    {
        int count = 0;
        Action handler = () => count++;
        UserSettings.Changed += handler;
        try
        {
            UserSettings.Brightness = 0.25f;
        }
        finally
        {
            UserSettings.Changed -= handler;
        }
        Assert.AreEqual(1, count);
    }
}
}
