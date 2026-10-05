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
    public void MoonlessIslandGetsCoolNonBlackFill()
    {
        LookProfile p = Load(4);
        Assert.IsFalse(p.moonOn);
        Color c = LookMapping.MoonColour(p.moonOn, p.moon, p.moonRim);
        Assert.Greater(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), 0.2f, "moon fill is black");
        Assert.IsTrue(LookMapping.IsCool(c), "moon fill is warm");
        Assert.AreEqual(1f, c.a, 1e-6f);
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

    static float Value(Color c)
    {
        Color.RGBToHSV(c, out _, out _, out float v);
        return v;
    }

    static float Dist(Color a, Color b)
    {
        return new Vector3(a.r - b.r, a.g - b.g, a.b - b.b).magnitude;
    }

    [Test]
    public void WaterAndRidgeTonesStayCool()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            Assert.IsTrue(LookMapping.IsCool(LookMapping.WaterShallow(p.sea, p.moonRim)), "island" + i + " shallow");
            Assert.IsTrue(LookMapping.IsCool(LookMapping.WaterDeep(p.sea)), "island" + i + " deep");
            Assert.IsTrue(LookMapping.IsCool(LookMapping.Foam(p.moonRim)), "island" + i + " foam");
            Color[] ridges = LookMapping.RidgeLayers(p.land, p.skyHorizon, 3);
            Assert.AreEqual(3, ridges.Length);
            for (int r = 0; r < ridges.Length; r++)
            {
                Assert.IsTrue(LookMapping.IsCool(ridges[r]), "island" + i + " ridge" + r);
            }
        }
    }

    [Test]
    public void GrassGradientStaysInIslandHue()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            Assert.Greater(p.grassRoot.a, 0f, "island" + i + " authors grassRoot");
            Assert.Greater(p.grassTip.a, 0f, "island" + i + " authors grassTip");
            Color root = GroundPalette.GrassRootFor(p);
            Color tip = GroundPalette.GrassTipFor(p);
            Assert.Less(Value(root), Value(tip), "island" + i + " tip lighter than root");
            Assert.Less(Value(tip), 0.97f, "island" + i + " tip is not blown out");
            Color.RGBToHSV(root, out float rh, out _, out _);
            Color.RGBToHSV(tip, out float th, out _, out _);
            Assert.Less(HueDistance(rh, th), 0.12f, "island" + i + " gradient stays in one hue");
        }
    }

    [Test]
    public void IslandGrassHuesAreDistinct()
    {
        float[] hues = new float[4];
        for (int i = 1; i <= 4; i++)
        {
            Color.RGBToHSV(Load(i).grassTip, out hues[i - 1], out _, out _);
        }

        // Island 1 green, 2 golden, 3 olive, 4 heath purple: every pair apart, and a wide overall spread.
        float widest = 0f;
        for (int a = 0; a < 4; a++)
        {
            for (int b = a + 1; b < 4; b++)
            {
                float d = HueDistance(hues[a], hues[b]);
                widest = Mathf.Max(widest, d);
                Assert.Greater(d, 0.03f, "islands " + (a + 1) + " and " + (b + 1) + " grass hues collapse");
            }
        }

        Assert.Greater(widest, 0.25f, "grass hue spread across the islands is too narrow");
    }

    static float HueDistance(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        return Mathf.Min(d, 1f - d);
    }

    [Test]
    public void RidgeLayersApproachHorizon()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            Color[] ridges = LookMapping.RidgeLayers(p.land, p.skyHorizon, 3);
            for (int r = 1; r < ridges.Length; r++)
            {
                Assert.Less(Dist(ridges[r], p.skyHorizon), Dist(ridges[r - 1], p.skyHorizon), "island" + i + " layer" + r);
            }
        }
    }

    [Test]
    public void GroundTonesStayNearBase()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            Color[] tones = LookMapping.GroundTones(p.land);
            Assert.AreEqual(3, tones.Length);
            Color.RGBToHSV(p.land, out float h0, out _, out float v0);
            for (int t = 0; t < tones.Length; t++)
            {
                Color.RGBToHSV(tones[t], out float h, out _, out float v);
                float dh = Mathf.Abs(h - h0);
                dh = Mathf.Min(dh, 1f - dh);
                Assert.LessOrEqual(Mathf.Abs(v - v0), 0.15f, "island" + i + " tone" + t + " value");
                Assert.LessOrEqual(dh, 0.08f, "island" + i + " tone" + t + " hue");
            }
        }
    }

    [Test]
    public void OrDerivedFallsBackOnlyWhenUnauthored()
    {
        Color derived = new Color(0.2f, 0.3f, 0.4f, 1f);
        Color authored = new Color(0.5f, 0.6f, 0.7f, 1f);
        Assert.AreEqual(derived, LookMapping.OrDerived(Color.clear, derived));
        Assert.AreEqual(authored, LookMapping.OrDerived(authored, derived));
    }

    [Test]
    public void DeepWaterIsDarkerThanShallow()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile p = Load(i);
            Assert.Less(Value(LookMapping.WaterDeep(p.sea)), Value(LookMapping.WaterShallow(p.sea, p.moonRim)), "island" + i);
        }
    }

    [Test]
    public void NewPaletteFieldsDoNotBreakValidation()
    {
        for (int i = 1; i <= 4; i++)
        {
            Assert.IsTrue(Load(i).Validate(out string problem), "island" + i + ": " + problem);
        }
    }

    [Test]
    public void Island3HasPaleTideBand()
    {
        Assert.AreEqual("4E6E66", LookPalette.ToHex(Load(3).tideBand));
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

    [Test]
    public void MoonRimQualityLowIsHalfResSimple()
    {
        LookMapping.MoonRimQuality q = LookMapping.MoonRimQualityFor(0);
        Assert.AreEqual(0.5f, q.resolutionScale, 1e-6f);
        Assert.IsTrue(q.simpleEdge);
    }

    [Test]
    public void MoonRimQualityFullForMediumAndAbove()
    {
        int[] values = { 1, 99 };
        for (int i = 0; i < values.Length; i++)
        {
            LookMapping.MoonRimQuality q = LookMapping.MoonRimQualityFor(values[i]);
            Assert.AreEqual(1f, q.resolutionScale, 1e-6f, "quality " + values[i]);
            Assert.IsFalse(q.simpleEdge, "quality " + values[i]);
        }
    }
}
}
