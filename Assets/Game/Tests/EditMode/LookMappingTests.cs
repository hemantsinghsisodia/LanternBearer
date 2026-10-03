using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class LookMappingTests
{
    const string ProfilePath = "Assets/Game/Art/Look/LookProfile_island";
    const string DawnPath = "Assets/Game/Art/Look/DawnLook.asset";
    const string GraphicsPath = "Assets/Game/Settings/Graphics/GraphicsProfile_";

    static LookProfile Load(int island)
    {
        LookProfile profile = AssetDatabase.LoadAssetAtPath<LookProfile>(ProfilePath + island + ".asset");
        Assert.IsNotNull(profile, "missing LookProfile_island" + island);
        return profile;
    }

    static void AssertColour(Color expected, Color actual, float tolerance, string message)
    {
        Assert.AreEqual(expected.r, actual.r, tolerance, message + " r");
        Assert.AreEqual(expected.g, actual.g, tolerance, message + " g");
        Assert.AreEqual(expected.b, actual.b, tolerance, message + " b");
    }

    [Test]
    public void AmbientStaysCoolForEveryIsland()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            LookMapping.AmbientValues a = LookMapping.Ambient(p.sky, p.sea, p.land, p.ambientIntensity);
            Assert.IsTrue(LookMapping.IsCool(a.sky), "island" + i + " sky");
            Assert.IsTrue(LookMapping.IsCool(a.equator), "island" + i + " equator");
            Assert.IsTrue(LookMapping.IsCool(a.ground), "island" + i + " ground");
        }
    }

    [Test]
    public void HorizonIsLerp()
    {
        Color h = LookMapping.Horizon(Color.black, Color.white, 0.35f);
        AssertColour(new Color(0.35f, 0.35f, 0.35f), h, 1e-4f, "horizon");
    }

    [Test]
    public void MoonOffCapsIntensity()
    {
        Assert.AreEqual(0.12f, LookMapping.MoonIntensity(false, 0.35f), 1e-6f);
        Assert.AreEqual(0.35f, LookMapping.MoonIntensity(true, 0.35f), 1e-6f);
    }

    [Test]
    public void DawnLerpEndpoints()
    {
        Color a = new Color(0.1f, 0.2f, 0.3f);
        Color b = new Color(0.9f, 0.8f, 0.7f);
        AssertColour(a, LookMapping.LerpDawn(a, b, 0f), 1e-6f, "u0");
        AssertColour(b, LookMapping.LerpDawn(a, b, 1f), 1e-6f, "u1");
        AssertColour(b, LookMapping.LerpDawn(a, b, 2f), 1e-6f, "u2");
        AssertColour(new Color(0.5f, 0.5f, 0.5f), LookMapping.LerpDawn(a, b, 0.5f), 1e-6f, "u0.5");
    }

    [Test]
    public void DawnLookMatchesSpec()
    {
        DawnLook d = AssetDatabase.LoadAssetAtPath<DawnLook>(DawnPath);
        Assert.IsNotNull(d, "missing DawnLook.asset");
        Assert.AreEqual("7E9CCB", LookPalette.ToHex(d.dawnTop));
        Assert.AreEqual("F4B38A", LookPalette.ToHex(d.dawnHorizon));
        Assert.AreEqual("C98A7A", LookPalette.ToHex(d.dawnGround));
        Assert.AreEqual("FFD9B0", LookPalette.ToHex(d.dawnLight));
        Assert.AreEqual(0.28f, d.dawnAmbientBoost, 1e-6f);
    }

    [Test]
    public void IslandProfilesHaveDawnAndHorizon()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            Assert.IsNotNull(p.dawn, "island" + i + " dawn");
            Color toward = i == 4 ? p.extra : p.moonRim;
            Color expected = LookMapping.Horizon(p.sky, toward);
            AssertColour(expected, p.skyHorizon, 1f / 255f, "island" + i + " skyHorizon");
            AssertColour(p.sea * 0.6f, p.skyGround, 1f / 255f, "island" + i + " skyGround");
            Assert.AreEqual(i == 4 ? 0.12f : 0.35f, p.moonIntensity, 1e-6f, "island" + i + " moonIntensity");
        }
    }

    [Test]
    public void MoonRimQualityPerPreset()
    {
        string[] names = { "Low", "Medium", "High", "Ultra" };
        for (int i = 0; i < names.Length; i++)
        {
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GraphicsPath + names[i] + ".asset");
            Assert.IsNotNull(profile, "missing GraphicsProfile_" + names[i]);
            SerializedProperty prop = new SerializedObject(profile).FindProperty("moonRimQuality");
            Assert.IsNotNull(prop, names[i] + " has no moonRimQuality");
            Assert.AreEqual(i == 0 ? 0 : 1, prop.intValue, names[i]);
        }
    }
}
}
