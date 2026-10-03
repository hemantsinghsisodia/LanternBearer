using UnityEngine;

namespace LanternKeeper
{
// Pure mapping from a look profile's colours to lighting values. Starting constants, tuned against captures.
public static class LookMapping
{
    public const float SkyAmbientScale = 1.6f;
    public const float EquatorAmbientScale = 1.4f;
    public const float GroundAmbientScale = 0.6f;
    public const float HorizonBlend = 0.35f;
    public const float MoonOffIntensityCap = 0.12f;
    public const float CoolBlackThreshold = 0.04f;

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
    }

    // The rim is never off: Low only halves its resolution and drops the facing term's extra taps.
    public static MoonRimQuality MoonRimQualityFor(int moonRimQuality)
    {
        MoonRimQuality q;
        q.resolutionScale = moonRimQuality <= 0 ? 0.5f : 1f;
        q.simpleEdge = moonRimQuality <= 0;
        return q;
    }

    public static AmbientValues Ambient(Color sky, Color sea, Color land, float ambientIntensity)
    {
        AmbientValues values;
        values.sky = Scale(sky, SkyAmbientScale);
        values.equator = Scale(sea, EquatorAmbientScale);
        values.ground = Scale(land, GroundAmbientScale);
        values.intensity = ambientIntensity;
        return values;
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

    static Color Scale(Color c, float k)
    {
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
    }
}
}
