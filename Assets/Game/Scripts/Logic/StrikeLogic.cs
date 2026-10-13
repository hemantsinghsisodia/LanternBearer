using UnityEngine;

namespace LanternKeeper
{
// Island 5's hunting lightning, as pure numbers (LightningHunter uses them; tests read them).
// Every IntervalMin..IntervalMax seconds a strike picks a point OffsetMin..OffsetMax metres from where the keeper will be in LeadSeconds,
// shows a warning ring for WarnSeconds, then hits. A keeper within HitRadius of the point loses FuelFraction of the lantern and is knocked
// back KnockDistance metres.
public static class StrikeLogic
{
    public const float IntervalMin = 6f, IntervalMax = 10f;
    public const float WarnSeconds = 1f;
    public const float LeadSeconds = 1f;
    public const float OffsetMin = 2f, OffsetMax = 4f;
    public const float HitRadius = 2.5f;
    public const float FuelFraction = 0.15f;
    public const float KnockDistance = 3f;
    // A keeper this far above or below the point (a bridge over it, a ledge above it) is not under the strike.
    public const float HitHeight = 3f;

    public static float Interval(System.Random random)
    {
        return IntervalMin + (float)random.NextDouble() * (IntervalMax - IntervalMin);
    }

    // Keeper position plus velocity x LeadSeconds, then a random offset of OffsetMin..OffsetMax metres in a random direction.
    public static Vector3 Target(Vector3 keeper, Vector3 velocity, System.Random random)
    {
        Vector3 predicted = keeper + new Vector3(velocity.x, 0f, velocity.z) * LeadSeconds;
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float distance = OffsetMin + (float)random.NextDouble() * (OffsetMax - OffsetMin);
        return predicted + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }

    public static bool Hits(Vector3 keeper, Vector3 strike)
    {
        Vector2 flat = new Vector2(keeper.x - strike.x, keeper.z - strike.z);
        return flat.magnitude <= HitRadius && Mathf.Abs(keeper.y - strike.y) <= HitHeight;
    }

    // Horizontal direction from the strike to the keeper; `fallback` when they coincide.
    public static Vector3 KnockDirection(Vector3 keeper, Vector3 strike, Vector3 fallback)
    {
        Vector3 away = new Vector3(keeper.x - strike.x, 0f, keeper.z - strike.z);
        if (away.sqrMagnitude > 0.01f)
        {
            return away.normalized;
        }

        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.01f ? fallback.normalized : Vector3.back;
    }
}
}
