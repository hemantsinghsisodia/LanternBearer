using UnityEngine;

namespace LanternKeeper
{
public enum LightningPhase
{
    Waiting,
    Thunder,
    Flash,
    Afterglow
}

// Pure lightning scheduler: Waiting -> Thunder -> Flash -> Afterglow -> Waiting. Owns its own System.Random.
// Each drawn interval is flash-to-flash. A strike whose window would touch a wind warning is delayed
// until the warning has ended. Wind values are seconds from now; a negative startsIn means no warning.
public class LightningSchedule
{
    public const float ThunderLead = 1.5f;
    public const float FlashSeconds = 0.3f;
    public const float AfterglowSeconds = 0.5f;
    private const float WarningPadding = 0.1f;

    private readonly System.Random random;
    private readonly Vector2 interval;

    private float untilThunder;
    private float phaseRemaining;

    public LightningPhase Phase { get; private set; }
    public bool FlashedThisTick { get; private set; }

    public float Flash01
    {
        get
        {
            if (Phase == LightningPhase.Flash)
            {
                return 1f;
            }

            if (Phase == LightningPhase.Afterglow)
            {
                return Mathf.Clamp01(phaseRemaining / AfterglowSeconds);
            }

            return 0f;
        }
    }

    public LightningSchedule(int seed, Vector2 interval)
    {
        this.interval = interval;
        random = new System.Random(seed);
        Phase = LightningPhase.Waiting;
        untilThunder = Mathf.Max(0f, DrawInterval() - ThunderLead);
    }

    // Test hook: jumps straight to the start of a flash.
    public void ForceFlash()
    {
        Phase = LightningPhase.Flash;
        phaseRemaining = FlashSeconds;
        FlashedThisTick = true;
    }

    public void Tick(float dt, bool roundOver, float windWarningStartsIn, float windWarningEndsIn)
    {
        FlashedThisTick = false;

        if (roundOver)
        {
            Phase = LightningPhase.Waiting;
            phaseRemaining = 0f;
            return;
        }

        if (dt <= 0f)
        {
            return;
        }

        bool hasWarning = windWarningStartsIn >= 0f;
        float remaining = dt;
        float startsIn = windWarningStartsIn;
        float endsIn = windWarningEndsIn;
        // Each pass consumes time or changes phase; the guard only protects against a runaway loop.
        for (int guard = 0; guard < 64 && remaining > 0f; guard++)
        {
            if (Phase == LightningPhase.Waiting)
            {
                float step = Mathf.Min(untilThunder, remaining);
                untilThunder -= step;
                remaining -= step;
                startsIn -= step;
                endsIn -= step;
                if (untilThunder > 0f)
                {
                    break;
                }

                if (hasWarning && Overlaps(startsIn, endsIn))
                {
                    untilThunder = endsIn + WarningPadding;
                    continue;
                }

                Phase = LightningPhase.Thunder;
                phaseRemaining = ThunderLead;
            }
            else
            {
                float step = Mathf.Min(phaseRemaining, remaining);
                phaseRemaining -= step;
                remaining -= step;
                startsIn -= step;
                endsIn -= step;
                if (phaseRemaining > 0f)
                {
                    break;
                }

                if (Phase == LightningPhase.Thunder)
                {
                    Phase = LightningPhase.Flash;
                    phaseRemaining = FlashSeconds;
                    FlashedThisTick = true;
                }
                else if (Phase == LightningPhase.Flash)
                {
                    Phase = LightningPhase.Afterglow;
                    phaseRemaining = AfterglowSeconds;
                }
                else
                {
                    Phase = LightningPhase.Waiting;
                    phaseRemaining = 0f;
                    // Interval is flash-to-flash: the flash began Flash + Afterglow ago, and the next
                    // thunder starts ThunderLead before the next flash.
                    untilThunder = Mathf.Max(0f, DrawInterval() - FlashSeconds - AfterglowSeconds - ThunderLead);
                }
            }
        }
    }

    // The whole strike window [0, ThunderLead + Flash + Afterglow] against the warning [startsIn, endsIn].
    private static bool Overlaps(float startsIn, float endsIn)
    {
        float windowEnd = ThunderLead + FlashSeconds + AfterglowSeconds;
        return endsIn >= 0f && startsIn <= windowEnd;
    }

    private float DrawInterval()
    {
        return interval.x + (float)random.NextDouble() * (interval.y - interval.x);
    }
}
}
