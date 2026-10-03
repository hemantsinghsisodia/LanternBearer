using TMPro;
using UnityEngine;

namespace LanternKeeper
{
// Shared UI look: fonts, colours and frame sprites. Plain data asset, so fields are public.
[CreateAssetMenu(menuName = "Lantern Keeper/UI Theme")]
public class UITheme : ScriptableObject
{
    public const int MinReadableHudFontPx = 18;

    [Header("Fonts")]
    public TMP_FontAsset titleFont;     // Cormorant Garamond Bold
    public TMP_FontAsset flavourFont;   // Spectral Italic
    public TMP_FontAsset uiFont;        // Inter Regular
    public TMP_FontAsset uiFontStrong;  // Inter SemiBold

    [Header("Signal colours")]
    public Color amber = LookPalette.FromHex(LookPalette.LanternAmber);
    public Color glowCore = LookPalette.FromHex(LookPalette.GlowCore);
    public Color drainViolet = LookPalette.FromHex(LookPalette.DrainViolet);
    public Color lightningBlue = LookPalette.FromHex(LookPalette.LightningBlue);
    public Color fireflyGreen = LookPalette.FromHex(LookPalette.FireflyGreen);

    [Header("Panels and text")]
    public Color inkPanel = new Color(0x0D / 255f, 0x10 / 255f, 0x20 / 255f, 0.88f);
    public Color brassLine = LookPalette.FromHex("6B5A44");
    public Color textPrimary = LookPalette.FromHex("F3E6CF");
    public Color textMuted = LookPalette.FromHex("C8B9A0");

    [Header("HUD")]
    public int minHudFontPx = MinReadableHudFontPx;

    [Header("Sprites (filled in later phases)")]
    public Sprite panelFrame;
    public Sprite buttonFrame;
    public Sprite iconLantern;
    public Sprite iconBeacon;
    public Sprite iconMoth;
    public Sprite iconShield;
    public Sprite iconEmber;

    // Sprites are not checked: Phase A leaves them empty.
    public bool Validate(out string problem)
    {
        if (titleFont == null)
        {
            problem = "titleFont is missing";
            return false;
        }
        if (flavourFont == null)
        {
            problem = "flavourFont is missing";
            return false;
        }
        if (uiFont == null)
        {
            problem = "uiFont is missing";
            return false;
        }
        if (uiFontStrong == null)
        {
            problem = "uiFontStrong is missing";
            return false;
        }
        if (minHudFontPx < MinReadableHudFontPx)
        {
            problem = "minHudFontPx " + minHudFontPx + " is below " + MinReadableHudFontPx;
            return false;
        }
        problem = null;
        return true;
    }
}
}
