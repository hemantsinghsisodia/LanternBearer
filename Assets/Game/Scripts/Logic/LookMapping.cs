using UnityEngine;

namespace LanternKeeper
{
// Pure mapping from a look profile's colours to lighting values. Starting constants, tuned against captures.
public static class LookMapping
{
    public const float SkyAmbientScale = 1.6f;
    public const float EquatorAmbientScale = 1.4f;
    public const float GroundAmbientScale = 0.6f;
    // Equator and ground ambient are pulled this far toward a neutral grey of the same luminance, so material colour reads at night.
    public const float AmbientNeutralise = 0.65f;
    public const float AmbientIntensityGain = 1.15f;
    // The scene's directional sky fill was authored teal (0.35, 0.6, 0.62); it is pulled to a near-neutral cool so it does not tint the ground.
    public static readonly Color SkyFillAuthored = new Color(0.35f, 0.6f, 0.62f);
    public const float HorizonBlend = 0.35f;
    public const float MoonOffIntensityCap = 0.12f;
    public const float CoolBlackThreshold = 0.04f;
    // The look volume's vignette. LowFuelFX adds its fuel and Shade pulses on top of it, so the effect never weakens the look.
    public const float LookVignette = 0.22f;
    // LowFuelFX's vignette at full fuel; anything above it is the extra the effect adds.
    public const float FullFuelVignette = 0.18f;

    public struct AmbientValues
    {
        public Color sky;
        public Color equator;
        public Color ground;
        public float intensity;
    }

    public struct MoonRimQuality
    {
        public float resolutionScale;
        public bool simpleEdge;
        public float gain;
    }

    // The rim is never off: Low only halves its resolution and drops the facing term's extra taps.
    public static MoonRimQuality MoonRimQualityFor(int moonRimQuality)
    {
        MoonRimQuality q;
        q.resolutionScale = moonRimQuality <= 0 ? 0.5f : 1f;
        q.simpleEdge = moonRimQuality <= 0;
        // Overall rim amplitude per tier. 1 on both for now; tuned in the look review.
        q.gain = 1f;
        return q;
    }

    public static AmbientValues Ambient(Color sky, Color sea, Color land, float ambientIntensity)
    {
        AmbientValues values;
        values.sky = Scale(sky, SkyAmbientScale);
        values.equator = Neutralise(Scale(sea, EquatorAmbientScale), AmbientNeutralise);
        values.ground = Neutralise(Scale(land, GroundAmbientScale), AmbientNeutralise);
        values.intensity = ambientIntensity * AmbientIntensityGain;
        return values;
    }

    public static Color SkyFill()
    {
        return Neutralise(SkyFillAuthored, AmbientNeutralise);
    }

    public static Color Horizon(Color sky, Color toward, float t = HorizonBlend)
    {
        return Color.Lerp(sky, toward, t);
    }

    public static float MoonIntensity(bool moonOn, float profileIntensity)
    {
        if (moonOn)
        {
            return profileIntensity;
        }

        return Mathf.Min(profileIntensity, MoonOffIntensityCap);
    }

    // The moon's light colour. A moonless island has a black "off" marker for moon, so it uses the cool fallback as a dim fill.
    public static Color MoonColour(bool moonOn, Color moon, Color fallback)
    {
        Color c = moonOn ? moon : fallback;
        return new Color(c.r, c.g, c.b, 1f);
    }

    public static Color LerpDawn(Color night, Color dawn, float u)
    {
        return Color.Lerp(night, dawn, Mathf.Clamp01(u));
    }

    // The warm/cold rule: ambient, fog and moonlight must never read warm.
    public static bool IsCool(Color c)
    {
        if (c.b >= c.r)
        {
            return true;
        }

        return Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < CoolBlackThreshold;
    }

    // Shallows: the sea pulled a quarter of the way to the moon rim, then brightened by 10% value.
    public static Color WaterShallow(Color sea, Color moonRim)
    {
        Color c = Color.Lerp(sea, moonRim, 0.25f);
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color result = Color.HSVToRGB(h, s, Mathf.Clamp01(v + 0.10f));
        result.a = 1f;
        return result;
    }

    public static Color WaterDeep(Color sea)
    {
        Color.RGBToHSV(sea, out float h, out float s, out float v);
        Color result = Color.HSVToRGB(h, s, v * 0.45f);
        result.a = 1f;
        return result;
    }

    public static Color Foam(Color moonRim)
    {
        Color c = Color.Lerp(moonRim, Color.white, 0.4f);
        c.a = 0.55f;
        return c;
    }

    // Layer 0 is the nearest and darkest; each further layer fades toward the sky horizon.
    public static Color[] RidgeLayers(Color land, Color skyHorizon, int count)
    {
        Color start = new Color(land.r * 0.8f, land.g * 0.8f, land.b * 0.8f, 1f);
        Color[] layers = new Color[count];
        for (int i = 0; i < count; i++)
        {
            Color c = Color.Lerp(start, skyHorizon, (i + 1f) / (count + 1f));
            c.a = 1f;
            layers[i] = c;
        }

        return layers;
    }

    public static Color[] GroundTones(Color baseColour)
    {
        Color.RGBToHSV(baseColour, out float h, out float s, out float v);
        Color dark = Color.HSVToRGB(h, s, v * 0.8f);
        Color light = Color.HSVToRGB(h, s * 0.92f, Mathf.Clamp01(v * 1.16f));
        dark.a = 1f;
        light.a = 1f;
        return new Color[] { dark, new Color(baseColour.r, baseColour.g, baseColour.b, 1f), light };
    }

    // Grass root: the land colour at 70% value, so tufts sit dark against the ground.
    public static Color GrassRoot(Color land)
    {
        Color.RGBToHSV(land, out float h, out float s, out float v);
        Color result = Color.HSVToRGB(h, s, v * 0.7f);
        result.a = 1f;
        return result;
    }

    // Authored grass tip with a soft moonlit highlight: the island's tip hue nudged a little toward the moon rim, so the gradient keeps its own hue.
    public const float GrassMoonlight = 0.15f;

    public static Color GrassTipLit(Color tip, Color moonRim)
    {
        Color c = Color.Lerp(tip, moonRim, GrassMoonlight);
        c.a = 1f;
        return c;
    }

    // Grass tip (derived fallback): the land colour pulled 40% toward the moon rim, desaturated by 20%.
    public static Color GrassTip(Color land, Color moonRim)
    {
        Color c = Color.Lerp(land, moonRim, 0.4f);
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color result = Color.HSVToRGB(h, s * 0.8f, v);
        result.a = 1f;
        return result;
    }

    // The largest pairwise circular hue distance between the colours (0..0.5).
    public static float HueSpread(Color[] colours)
    {
        if (colours == null || colours.Length < 2)
        {
            return 0f;
        }

        float[] hues = new float[colours.Length];
        for (int i = 0; i < colours.Length; i++)
        {
            Color.RGBToHSV(colours[i], out float h, out float s, out float v);
            hues[i] = h;
        }

        float spread = 0f;
        for (int a = 0; a < hues.Length; a++)
        {
            for (int b = a + 1; b < hues.Length; b++)
            {
                float d = Mathf.Abs(hues[a] - hues[b]);
                spread = Mathf.Max(spread, Mathf.Min(d, 1f - d));
            }
        }

        return spread;
    }

    public static Color OrDerived(Color authored, Color derived)
    {
        return authored.a == 0f ? derived : authored;
    }

    // Pulls a colour toward the grey of equal luminance. Never adds warmth: the result lies between the colour and its grey.
    public static Color Neutralise(Color c, float amount)
    {
        float grey = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        return new Color(Mathf.Lerp(c.r, grey, amount), Mathf.Lerp(c.g, grey, amount), Mathf.Lerp(c.b, grey, amount), 1f);
    }

    static Color Scale(Color c, float k)
    {
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
    }
}
}
