using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class NaturePaletteTests
{
    const string ProfilePath = "Assets/Game/Art/Look/LookProfile_island";

    static LookProfile Load(int island)
    {
        LookProfile profile = AssetDatabase.LoadAssetAtPath<LookProfile>(ProfilePath + island + ".asset");
        Assert.IsNotNull(profile, "missing LookProfile_island" + island);
        return profile;
    }

    static float Hue(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return h;
    }

    static float HueDistance(Color a, Color b)
    {
        float d = Mathf.Abs(Hue(a) - Hue(b));
        return Mathf.Min(d, 1f - d);
    }

    [Test]
    public void FoliageHuesDistinct()
    {
        Color[] foliage = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            foliage[i] = Load(i + 1).foliage;
        }

        Assert.GreaterOrEqual(LookMapping.HueSpread(foliage), 0.25f, "foliage hue spread");
        for (int a = 0; a < 4; a++)
        {
            for (int b = a + 1; b < 4; b++)
            {
                float min = (a == 0 && b == 2) ? 0.03f : 0.05f;
                Assert.GreaterOrEqual(HueDistance(foliage[a], foliage[b]), min, "islands " + (a + 1) + " and " + (b + 1));
            }
        }
    }

    [Test]
    public void HueSpreadIsMaxPairwiseCircularDistance()
    {
        Color red = Color.HSVToRGB(0.95f, 1f, 1f);
        Color orange = Color.HSVToRGB(0.05f, 1f, 1f);
        Assert.AreEqual(0.1f, LookMapping.HueSpread(new[] { red, orange }), 0.001f);
        Assert.AreEqual(0.5f, LookMapping.HueSpread(new[] { Color.HSVToRGB(0f, 1f, 1f), Color.HSVToRGB(0.5f, 1f, 1f) }), 0.001f);
        Assert.AreEqual(0f, LookMapping.HueSpread(new Color[0]), 0.001f);
    }

    [Test]
    public void BarkAndRockTintAreSet()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile profile = Load(i);
            Assert.AreEqual(1f, profile.bark.a, 0.001f, "bark alpha island" + i);
            Assert.AreEqual(1f, profile.rockTint.a, 0.001f, "rockTint alpha island" + i);
            Assert.AreEqual(1f, profile.foliage.a, 0.001f, "foliage alpha island" + i);
        }
    }
}
}
