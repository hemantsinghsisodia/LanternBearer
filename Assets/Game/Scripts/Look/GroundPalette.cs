using UnityEngine;

namespace LanternKeeper
{
// Base colour of each painted terrain layer. Natural material hues (green or golden grass ground, warm brown paths, pale tan sand, grey rock, green moss);
// the night comes from the moonlight, fog and grading, not from tinting the albedo. Runtime-safe so tests and the editor builder share it.
public static class GroundPalette
{
    public static readonly string[] Layers = { "sand", "grass", "dirt", "rock", "moss" };

    static readonly Color Sand = new Color(0.92f, 0.72f, 0.34f, 1f);
    static readonly Color Dirt = new Color(0.80f, 0.40f, 0.10f, 1f);
    static readonly Color Rock = new Color(0.64f, 0.55f, 0.45f, 1f);
    static readonly Color SlateRock = new Color(0.48f, 0.48f, 0.56f, 1f);
    static readonly Color Moss = new Color(0.20f, 0.46f, 0.10f, 1f);

    public static Color LayerBase(LookProfile p, string layer)
    {
        Color grass = GrassGround(p);
        Color c;
        switch (layer)
        {
            case "sand":
                c = Sand;
                break;
            case "dirt":
                c = Dirt;
                // Paths must read lighter than the ground around them, whatever the island's grass is.
                Color.RGBToHSV(c, out float dh, out float ds, out float dv);
                Color.RGBToHSV(grass, out _, out _, out float gv);
                c = ScaleValue(c, Mathf.Max(1f, gv * 1.35f / Mathf.Max(0.01f, dv)), 1f);
                break;
            case "rock":
                c = p.levelId == "island2" ? SlateRock : Rock;
                break;
            case "moss":
                c = Moss;
                break;
            default:
                c = grass;
                break;
        }

        c.a = 1f;
        return c;
    }

    // The grass ground sits between the island's grass root and tip, so it matches the blades on top of it.
    public static Color GrassGround(LookProfile p)
    {
        if (p.grassRoot.a > 0f && p.grassTip.a > 0f)
        {
            // Ground sits darker than the blades above it, so tufts read against it.
            return ScaleValue(Color.Lerp(p.grassRoot, p.grassTip, 0.4f), 0.8f, 1.25f);
        }

        return p.land;
    }

    // Authored per-island grass hue when set, otherwise the land-derived tones. The tip carries the moonlit highlight.
    public static Color GrassRootFor(LookProfile p)
    {
        Color c = LookMapping.OrDerived(p.grassRoot, LookMapping.GrassRoot(p.land));
        c.a = 1f;
        return c;
    }

    public static Color GrassTipFor(LookProfile p)
    {
        return p.grassTip.a > 0f ? LookMapping.GrassTipLit(p.grassTip, p.moonRim) : LookMapping.GrassTip(p.land, p.moonRim);
    }

    static Color ScaleValue(Color c, float value, float saturation)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return Color.HSVToRGB(h, Mathf.Clamp01(s * saturation), Mathf.Clamp01(v * value));
    }
}
}
