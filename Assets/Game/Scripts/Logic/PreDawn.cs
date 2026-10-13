using UnityEngine;

namespace LanternKeeper
{
// The Island 1-4 wins end in a pre-dawn that grows island by island; only the Island 5 finale reaches the full sunrise.
public static class PreDawn
{
    public static float ReachFor(string levelId)
    {
        switch (levelId)
        {
            case "island1": return 0.25f;
            case "island2": return 0.45f;
            case "island3": return 0.65f;
            case "island4": return 0.85f;
            default: return 1f;
        }
    }

    // The dawn blend at progress t01 (0..1): smooth-stepped, and it stops at reach.
    public static float Blend(float t01, float reach)
    {
        return Mathf.Clamp01(reach) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t01));
    }
}
}
