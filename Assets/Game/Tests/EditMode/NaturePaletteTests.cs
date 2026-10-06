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
            Assert.AreNotEqual(Color.white, profile.bark, "bark is still the white default on island" + i);
            Assert.AreNotEqual(Color.white, profile.rockTint, "rockTint is still the white default on island" + i);
            Assert.Less(profile.bark.r + profile.bark.g + profile.bark.b, 2.4f, "bark too bright island" + i);
            Assert.Less(profile.rockTint.r + profile.rockTint.g + profile.rockTint.b, 2.4f, "rockTint too bright island" + i);
            Assert.AreEqual(1f, profile.foliage.a, 0.001f, "foliage alpha island" + i);
        }
    }

    [Test]
    public void TreeFoliageDiffersOnlyOnIsland4()
    {
        for (int i = 1; i <= 4; i++)
        {
            LookProfile profile = Load(i);
            Color tree = LookMapping.OrDerived(profile.treeFoliage, profile.foliage);
            if (i == 4)
            {
                Assert.Greater(profile.treeFoliage.a, 0f, "island4 authors treeFoliage");
                Assert.GreaterOrEqual(HueDistance(tree, profile.foliage), 0.15f, "island4 trees must differ in hue from the purple bushes");
                Assert.GreaterOrEqual(Hue(tree), 0.20f, "island4 tree hue is olive, not purple");
                Assert.LessOrEqual(Hue(tree), 0.31f, "island4 tree hue is olive, not purple");
            }
            else
            {
                Assert.AreEqual(profile.foliage, tree, "treeFoliage defaults to foliage on island" + i);
            }
        }
    }

    const string MaterialPath = "Assets/Game/Art/Environment/Nature/Materials/Nature_";

    static Material LoadMaterial(string kind, int island)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + kind + "_island" + island + ".mat");
        Assert.IsNotNull(material, "missing material " + kind + " island" + island);
        return material;
    }

    [Test]
    public void GeneratedTreeLeavesDifferFromBushesOnIsland4()
    {
        Material tree = LoadMaterial("LeavesNormal", 4);
        Material bush = LoadMaterial("LeavesBush", 4);
        Color treeTint = tree.GetColor("_BaseColor");
        Color bushTint = bush.GetColor("_BaseColor");
        Assert.GreaterOrEqual(HueDistance(treeTint, bushTint), 0.15f, "island4 tree leaf material must differ in hue from the bush material");
        Assert.GreaterOrEqual(Hue(treeTint), 0.20f, "island4 tree leaf tint is olive");
        Assert.LessOrEqual(Hue(treeTint), 0.31f, "island4 tree leaf tint is olive");
        Assert.GreaterOrEqual(HueDistance(treeTint, LoadMaterial("LeavesNormal", 1).GetColor("_BaseColor")), 0.02f, "island4 tree leaves must not share island1 tree hue");
        Color.RGBToHSV(treeTint, out float h, out float saturation, out float value);
        Assert.Less(saturation, 0.5f, "island4 tree leaf tint is muted");
        Assert.Less(value, 0.35f, "island4 tree leaf tint is dark");
        // The leaf texture is ochre; trees drop most of it so the muted tint decides the colour, bushes keep the purple look.
        Assert.GreaterOrEqual(tree.GetFloat("_Desaturate"), 0.5f, "island4 tree leaves desaturate the leaf texture");
        Assert.AreEqual(0f, bush.GetFloat("_Desaturate"), 0.001f, "island4 bushes keep the leaf texture");
        string[] other = { "LeavesPine", "LeavesTwisted" };
        for (int i = 0; i < other.Length; i++)
        {
            Assert.GreaterOrEqual(LoadMaterial(other[i], 4).GetFloat("_Desaturate"), 0.5f, other[i] + " island4");
        }

        for (int island = 1; island <= 3; island++)
        {
            Material leaves = LoadMaterial("LeavesNormal", island);
            Assert.AreEqual(0f, leaves.GetFloat("_Desaturate"), 0.001f, "island" + island + " tree leaves are untouched");
            // Islands without their own treeFoliage keep the profile-derived tint (the island foliage colour).
            Color expected = Load(island).foliage;
            Color actual = leaves.GetColor("_BaseColor");
            Assert.AreEqual(expected.r, actual.r, 0.002f, "island" + island + " tree leaf tint r");
            Assert.AreEqual(expected.g, actual.g, 0.002f, "island" + island + " tree leaf tint g");
            Assert.AreEqual(expected.b, actual.b, 0.002f, "island" + island + " tree leaf tint b");
        }
    }
}
}
