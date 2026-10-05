using UnityEngine;

namespace LanternKeeper
{
// Base colour of each painted terrain layer, derived from an island's look profile. Runtime-safe so tests and the editor builder share it.
public static class GroundPalette
{
    public static readonly string[] Layers = { "sand", "grass", "dirt", "rock", "moss" };

    // Warmer-neutral grey that paths drift toward. The result stays cool because land dominates the lerp.
    static readonly Color PathGrey = new Color(0x5A / 255f, 0x56 / 255f, 0x50 / 255f, 1f);

    public static Color LayerBase(LookProfile p, string layer)
    {
        Color c;
        switch (layer)
        {
            case "sand":
                c = ScaleValue(Color.Lerp(p.land, p.sea, 0.5f), 1.15f, 1f);
                break;
            case "dirt":
                c = ScaleValue(Color.Lerp(p.land, PathGrey, 0.35f), 1.25f, 1f);
                break;
            case "rock":
                // Island 2 has its own slate.
                c = p.levelId == "island2" && p.extra.a > 0f
                    ? p.extra
                    : ScaleValue(Color.Lerp(p.land, p.moonRim, 0.5f), 0.9f, 1f);
                break;
            case "moss":
                c = ScaleValue(p.land, 0.9f, 1.1f);
                break;
            default:
                c = p.land;
                break;
        }

        c.a = 1f;
        return c;
    }

    static Color ScaleValue(Color c, float value, float saturation)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return Color.HSVToRGB(h, Mathf.Clamp01(s * saturation), Mathf.Clamp01(v * value));
    }
}
}
