using NUnit.Framework;
using TMPro;
using UnityEditor;

namespace LanternKeeper.Tests
{
public class UIThemeTests
{
    const string ThemePath = "Assets/Game/Art/UI/UITheme.asset";

    // Everything the font assets are created with: ASCII 32-126 plus the punctuation the game prints.
    static readonly string RequiredGlyphs = BuildRequiredGlyphs();

    static string BuildRequiredGlyphs()
    {
        string glyphs = "—·’“”…•";
        for (int c = 32; c <= 126; c++)
        {
            glyphs += (char)c;
        }
        return glyphs;
    }

    static UITheme Load()
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Assert.IsNotNull(theme, "missing UITheme asset");
        return theme;
    }

    static TMP_FontAsset[] Fonts(UITheme t)
    {
        return new[] { t.titleFont, t.flavourFont, t.uiFont, t.uiFontStrong };
    }

    [Test]
    public void ThemeValidates()
    {
        string problem;
        Assert.IsTrue(Load().Validate(out problem), problem);
    }

    [Test]
    public void SignalColoursMatchSpec()
    {
        UITheme t = Load();
        Assert.AreEqual(LookPalette.LanternAmber, LookPalette.ToHex(t.amber), "amber");
        Assert.AreEqual(LookPalette.GlowCore, LookPalette.ToHex(t.glowCore), "glowCore");
        Assert.AreEqual(LookPalette.DrainViolet, LookPalette.ToHex(t.drainViolet), "drainViolet");
        Assert.AreEqual(LookPalette.LightningBlue, LookPalette.ToHex(t.lightningBlue), "lightningBlue");
        Assert.AreEqual(LookPalette.FireflyGreen, LookPalette.ToHex(t.fireflyGreen), "fireflyGreen");
    }

    [Test]
    public void PanelColoursMatchSpec()
    {
        UITheme t = Load();
        Assert.AreEqual("0D1020", LookPalette.ToHex(t.inkPanel), "inkPanel");
        Assert.AreEqual(0.88f, t.inkPanel.a, 0.005f, "inkPanel alpha");
        Assert.AreEqual("6B5A44", LookPalette.ToHex(t.brassLine), "brassLine");
        Assert.AreEqual("F3E6CF", LookPalette.ToHex(t.textPrimary), "textPrimary");
        Assert.AreEqual("C8B9A0", LookPalette.ToHex(t.textMuted), "textMuted");
        Assert.AreEqual(18, t.minHudFontPx);
    }

    [Test]
    public void FontsHaveRequiredGlyphs()
    {
        // tryAddCharacter is false: the saved assets must already contain the glyphs, and the test must not modify them.
        foreach (TMP_FontAsset font in Fonts(Load()))
        {
            Assert.IsNotNull(font);
            uint[] missing;
            bool ok = font.HasCharacters(RequiredGlyphs, out missing, false, false);
            string list = "";
            if (missing != null)
            {
                for (int i = 0; i < missing.Length; i++)
                {
                    list += char.ConvertFromUtf32((int)missing[i]);
                }
            }
            Assert.IsTrue(ok, font.name + " missing glyphs: " + list);
        }
    }

    [Test]
    public void FontsAreStaticNotVariable()
    {
        foreach (TMP_FontAsset font in Fonts(Load()))
        {
            Assert.IsNotNull(font.sourceFontFile, font.name + " has no source font");
            Assert.IsFalse(font.sourceFontFile.name.Contains("["), font.name + " is variable: " + font.sourceFontFile.name);
        }
    }

    [Test]
    public void ValidateRejectsMissingFontAndSmallHudText()
    {
        UITheme t = UnityEngine.ScriptableObject.CreateInstance<UITheme>();
        string problem;
        Assert.IsFalse(t.Validate(out problem), "null fonts must fail");
        UITheme good = UnityEngine.Object.Instantiate(Load());
        Assert.IsTrue(good.Validate(out problem), problem);
        good.minHudFontPx = 17;
        Assert.IsFalse(good.Validate(out problem), "minHudFontPx 17 must fail");
        UnityEngine.Object.DestroyImmediate(t);
        UnityEngine.Object.DestroyImmediate(good);
    }

    [Test]
    public void AllSlotsAssigned()
    {
        string problem;
        Assert.IsTrue(Load().Validate(out problem), problem);
        UITheme t = Load();
        Assert.AreEqual("E89A48", LookPalette.ToHex(t.amberDeep), "amberDeep");
        Assert.AreEqual("E3D3B0", LookPalette.ToHex(t.parchment), "parchment");
        Assert.AreEqual("B89F72", LookPalette.ToHex(t.parchmentEdge), "parchmentEdge");
        Assert.AreEqual("3A2C1C", LookPalette.ToHex(t.inkText), "inkText");
        Assert.AreEqual("7A2E22", LookPalette.ToHex(t.ribbon), "ribbon");
        Assert.AreEqual(0.35f, t.focusGlow.a, 0.005f, "focusGlow alpha");
        float v0, v1, h, s;
        UnityEngine.Color.RGBToHSV(t.brassLine, out h, out s, out v0);
        UnityEngine.Color.RGBToHSV(t.focusOutline, out h, out s, out v1);
        Assert.Greater(v1, v0, "focusOutline is brighter than brassLine");
    }

    [Test]
    public void SpecAmberIsExact()
    {
        Assert.AreEqual("FFB15C", LookPalette.ToHex(Load().amber));
    }

    [Test]
    public void BodyTextReadable()
    {
        UITheme t = Load();
        Assert.GreaterOrEqual(t.bodyPx, 18);
        Assert.GreaterOrEqual(t.labelPx, 18);
        Assert.AreEqual(56, t.titlePx);
    }
}
}
