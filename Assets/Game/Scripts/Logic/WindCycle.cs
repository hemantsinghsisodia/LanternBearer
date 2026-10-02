using System;
using UnityEngine;

namespace LanternKeeper
{
public enum WindPhase
{
    Calm,
    Warning,
    Gust
}

// Pure wind state machine: Calm -> Warning -> Gust -> Calm. Owns its own System.Random.
// strengthScale only affects Push, never the timings.
public class WindCycle
{
    private const float WarningSeconds = 2f;
    private const float GustMin = 3f;
    private const float GustMax = 4f;
    private const float RampSeconds = 0.5f;
    private const float MaxYawOffset = 60f;

    private readonly System.Random random;
    private readonly float prevailingDegrees;
    private readonly float calmMin;
    private readonly float calmMax;

    private float phaseDuration;
    private float phaseElapsed;
    private float nextCalm;

    public WindPhase Phase { get; private set; }
    public Vector3 Direction { get; private set; }

    public float Strength01
    {
        get
        {
            if (Phase != WindPhase.Gust)
            {
                return 0f;
            }

            float up = Mathf.Clamp01(phaseElapsed / RampSeconds);
            float down = Mathf.Clamp01((phaseDuration - phaseElapsed) / RampSeconds);
            return Mathf.Min(up, down);
        }
    }

    // Calm: remaining calm. Warning: 0. Gust: remaining gust plus the next calm (drawn when the gust begins).
    public float TimeToNextWarning
    {
        get
        {
            if (Phase == WindPhase.Calm)
            {
                return phaseDuration - phaseElapsed;
            }

            if (Phase == WindPhase.Warning)
            {
                return 0f;
            }

            return (phaseDuration - phaseElapsed) + nextCalm;
        }
    }

    // Warning: remaining warning. Otherwise: TimeToNextWarning + the warning length.
    public float WarningEndsIn
    {
        get
        {
            if (Phase == WindPhase.Warning)
            {
                return phaseDuration - phaseElapsed;
            }

            return TimeToNextWarning + WarningSeconds;
        }
    }

    public WindCycle(int seed, float prevailingDegrees, float strengthScale, bool hard)
    {
        random = new System.Random(seed);
        this.prevailingDegrees = prevailingDegrees;
        calmMin = hard ? 10f : 12f;
        calmMax = hard ? 16f : 20f;
        Direction = DirectionFromYaw(prevailingDegrees);
        Phase = WindPhase.Calm;
        phaseDuration = Range(calmMin, calmMax);
        phaseElapsed = 0f;
    }

    public void Tick(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        phaseElapsed += dt;
        while (phaseElapsed >= phaseDuration)
        {
            float carry = phaseElapsed - phaseDuration;
            Advance();
            phaseElapsed = carry;
        }
    }

    public static Vector3 Push(Vector3 dir, float strength01, float strengthScale, bool airborne, bool sheltered)
    {
        Vector3 push = dir * strength01 * StormTuning.PeakPush * strengthScale;
        if (airborne)
        {
            push *= 0.5f;
        }

        if (sheltered)
        {
            push *= 0.2f;
        }

        return push;
    }

    public static bool PushAllowed(bool paused, bool dying, bool rescuing, bool inSafeRing)
    {
        return !(paused || dying || rescuing || inSafeRing);
    }

    private void Advance()
    {
        if (Phase == WindPhase.Calm)
        {
            Phase = WindPhase.Warning;
            phaseDuration = WarningSeconds;
            float yaw = prevailingDegrees + ((float)random.NextDouble() * 2f - 1f) * MaxYawOffset;
            Direction = DirectionFromYaw(yaw);
        }
        else if (Phase == WindPhase.Warning)
        {
            Phase = WindPhase.Gust;
            phaseDuration = Range(GustMin, GustMax);
            nextCalm = Range(calmMin, calmMax);
        }
        else
        {
            Phase = WindPhase.Calm;
            phaseDuration = nextCalm;
        }
    }

    private float Range(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static Vector3 DirectionFromYaw(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }
}
}
