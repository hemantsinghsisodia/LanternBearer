using System;
using System.Globalization;
using UnityEngine;

namespace LanternKeeper
{
// The signal colours that mean the same thing on every island, plus hex helpers shared by profiles and tests.
public static class LookPalette
{
    public const string LanternAmber = "FFB15C";
    public const string GlowCore = "FFD38A";
    public const string DrainViolet = "B48CFF";
    public const string LightningBlue = "BCD2FF";
    public const string FireflyGreen = "B8FFB0";

    // Six hex digits, sRGB, alpha 1.
    public static Color FromHex(string hex)
    {
        if (hex == null || hex.Length != 6)
        {
            throw new ArgumentException("Expected 6 hex digits, got '" + hex + "'.", "hex");
        }

        int value;
        if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
        {
            throw new ArgumentException("Not a hex colour: '" + hex + "'.", "hex");
        }

        return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, 1f);
    }

    // Uppercase RGB, no '#'.
    public static string ToHex(Color c)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }
}
}
