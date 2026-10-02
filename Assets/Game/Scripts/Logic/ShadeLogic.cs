using UnityEngine;

namespace LanternKeeper
{
public enum ShadeState
{
    Chase,
    Creep,
    Freeze,
    Retreat,
    Reforming,
    Stunned
}

// Pure rules for the Shade stalker: state, speed and fuel-steal decisions.
public static class ShadeLogic
{
    public const float TouchRadius = 1f;
    public const float ReformSeconds = 8f;
    public const float StunSeconds = 3f;
    public const float HoverHeight = 0.2f;
    public const float RescueGrace = 2f;

    private const float CreepThreshold = 0.15f;
    private const float FreezeThreshold = 0.5f;
    private const float RetreatAfterSeconds = 1.5f;

    public static ShadeState StateFor(float reveal, float frozenSeconds)
    {
        if (reveal < CreepThreshold)
        {
            return ShadeState.Chase;
        }

        if (reveal < FreezeThreshold)
        {
            return ShadeState.Creep;
        }

        if (frozenSeconds >= RetreatAfterSeconds)
        {
            return ShadeState.Retreat;
        }

        return ShadeState.Freeze;
    }

    public static float SpeedFor(ShadeState s)
    {
        if (s == ShadeState.Chase)
        {
            return 4.5f;
        }

        if (s == ShadeState.Creep)
        {
            return 0.8f;
        }

        if (s == ShadeState.Retreat)
        {
            return 3f;
        }

        return 0f;
    }

    public static float StealAmount(float fuel, float steal)
    {
        return Mathf.Max(0f, Mathf.Min(fuel, steal));
    }

    public static bool CanSteal(ShadeState s, float distance, float secondsSinceRescue)
    {
        if (distance > TouchRadius)
        {
            return false;
        }

        if (s != ShadeState.Chase && s != ShadeState.Creep)
        {
            return false;
        }

        return secondsSinceRescue >= RescueGrace;
    }
}
}
