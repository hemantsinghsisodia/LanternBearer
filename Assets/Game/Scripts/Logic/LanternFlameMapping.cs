using UnityEngine;

namespace LanternKeeper
{
// Pure mapping from normalised fuel to the lantern flame's look.
public static class LanternFlameMapping
{
    public const float MinHeight = 0.45f;
    const float MinHalo = 0.55f;
    static readonly Color EmptyColour = new Color(0xE8 / 255f, 0x50 / 255f, 0x2A / 255f, 1f);
    static readonly Color FullColour = new Color(0xFF / 255f, 0xD3 / 255f, 0x8A / 255f, 1f);

    public static float Height(float fuel01)
    {
        return Mathf.Lerp(MinHeight, 1f, Mathf.Clamp01(fuel01));
    }

    public static Color FlameColour(float fuel01)
    {
        return Color.Lerp(EmptyColour, FullColour, Mathf.Clamp01(fuel01));
    }

    public static float HaloScale(float fuel01)
    {
        return Mathf.Lerp(MinHalo, 1f, Mathf.Clamp01(fuel01));
    }
}
}
