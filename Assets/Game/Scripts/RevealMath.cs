using UnityEngine;

namespace LanternKeeper
{
public static class RevealMath
{
    public const float InnerFraction = 0.45f;
    public const float SolidReveal = 0.5f;
    public const float MinRadius = 0.2f;

    public static float Evaluate(Vector3 point, Vector3 lantern, float radius)
    {
        if (radius < MinRadius)
        {
            return 0f;
        }

        float inner = radius * InnerFraction;
        float span = radius - inner;
        if (span < 0.0001f)
        {
            return Vector3.Distance(point, lantern) < radius ? 1f : 0f;
        }

        float distance = Vector3.Distance(point, lantern);
        float t = (distance - inner) / span;
        if (t <= 0f)
        {
            return 1f;
        }

        if (t >= 1f)
        {
            return 0f;
        }

        float hermite = t * t * (3f - 2f * t);
        return 1f - hermite;
    }
}
}
