using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class LookProfileTests
{
    const string ProfilePath = "Assets/Game/Art/Look/LookProfile_island";
    const string LevelPath = "Assets/Game/Levels/Island";

    static readonly string[] SignalColours =
    {
        LookPalette.LanternAmber, LookPalette.GlowCore, LookPalette.DrainViolet,
        LookPalette.LightningBlue, LookPalette.FireflyGreen
    };

    [Test]
    public void FromHexRoundTrips()
    {
        for (int i = 0; i < SignalColours.Length; i++)
        {
            Assert.AreEqual(SignalColours[i], LookPalette.ToHex(LookPalette.FromHex(SignalColours[i])));
        }
    }

    [Test]
    public void FromHexRejectsMalformed()
    {
        Assert.Throws<ArgumentException>(() => LookPalette.FromHex("FFB15"));
        Assert.Throws<ArgumentException>(() => LookPalette.FromHex("GGGGGG"));
        Assert.Throws<ArgumentException>(() => LookPalette.FromHex(""));
    }

    static LookProfile Load(int island)
    {
        LookProfile profile = AssetDatabase.LoadAssetAtPath<LookProfile>(ProfilePath + island + ".asset");
        Assert.IsNotNull(profile, "missing LookProfile_island" + island);
        return profile;
    }

    static void CheckPalette(int island, string sky, string sea, string land, string rim, string extra, string moon, bool moonOn, bool mist, bool rain)
    {
        LookProfile p = Load(island);
        string tag = "island" + island + " ";
        Assert.AreEqual("island" + island, p.levelId, tag + "levelId");
        Assert.AreEqual(sky, LookPalette.ToHex(p.sky), tag + "sky");
        Assert.AreEqual(sea, LookPalette.ToHex(p.sea), tag + "sea");
        Assert.AreEqual(land, LookPalette.ToHex(p.land), tag + "land");
        Assert.AreEqual(rim, LookPalette.ToHex(p.moonRim), tag + "moonRim");
        Assert.AreEqual(extra, LookPalette.ToHex(p.extra), tag + "extra");
        Assert.AreEqual(moon, LookPalette.ToHex(p.moon), tag + "moon");
        Assert.AreEqual(moonOn, p.moonOn, tag + "moonOn");
        Assert.AreEqual(mist, p.mist, tag + "mist");
        Assert.AreEqual(rain, p.rain, tag + "rain");
    }

    [Test]
    public void IslandPalettesMatchSpec()
    {
        // "none" entries (island 1 extra, island 4 moon) are stored as transparent black.
        CheckPalette(1, "0C1630", "17304D", "163027", "6FB3C8", "000000", "EEF6FF", true, false, false);
        CheckPalette(2, "0D1124", "151D33", "1B2A2A", "8A9CC4", "1D2233", "E6ECFF", true, true, false);
        CheckPalette(3, "0A1A1C", "132A28", "1B2E24", "7FB8A2", "7FB8A2", "D8F0D0", true, true, false);
        CheckPalette(4, "120F22", "1C1A30", "2A2638", "B8B2E0", "D6DCFF", "000000", false, false, true);
    }

    [Test]
    public void ProfilesValidate()
    {
        for (int i = 1; i <= 4; i++)
        {
            string problem;
            Assert.IsTrue(Load(i).Validate(out problem), "island" + i + ": " + problem);
        }

        LookProfile bad = ScriptableObject.CreateInstance<LookProfile>();
        bad.levelId = "x";
        bad.fogDensity = 0.5f;
        string reason;
        Assert.IsFalse(bad.Validate(out reason));
        Assert.IsNotEmpty(reason);
        UnityEngine.Object.DestroyImmediate(bad);
    }

    [Test]
    public void LevelConfigsReferenceTheirProfile()
    {
        for (int i = 1; i <= 4; i++)
        {
            ScriptableObject config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LevelPath + i + ".asset");
            Assert.IsNotNull(config, "missing Island" + i + ".asset");
            SerializedObject so = new SerializedObject(config);
            LookProfile profile = so.FindProperty("lookProfile").objectReferenceValue as LookProfile;
            Assert.IsNotNull(profile, "Island" + i + " has no lookProfile");
            Assert.AreEqual(so.FindProperty("levelId").stringValue, profile.levelId);
        }
    }
}
}
