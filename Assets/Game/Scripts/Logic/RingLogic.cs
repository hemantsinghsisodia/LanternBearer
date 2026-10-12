namespace LanternKeeper
{
public enum Ring { Low = 0, Mid = 1, High = 2, Summit = 3 }

// Height rings of the Island 5 spire. Hazards ratchet: the highest ring reached stays in force for the rest of the run.
public static class RingLogic
{
    public const float LowTop = 10f, MidTop = 25f, HighTop = 38f;
    public const float ThinAirRadius = 0.80f, ThinAirDrain = 1.15f;

    // Bands above sea level. A boundary height belongs to the upper ring.
    public static Ring Classify(float heightAboveSea)
    {
        if (heightAboveSea >= HighTop)
        {
            return Ring.Summit;
        }

        if (heightAboveSea >= MidTop)
        {
            return Ring.High;
        }

        return heightAboveSea >= LowTop ? Ring.Mid : Ring.Low;
    }

    // Never lower: walking back down keeps the hazards of the highest ring reached.
    public static Ring Ratchet(Ring reached, Ring current)
    {
        return current > reached ? current : reached;
    }

    // Minimum storm tension for the highest ring reached.
    public static float TensionFloor(Ring reached)
    {
        switch (reached)
        {
            case Ring.Mid:
                return 0.3f;
            case Ring.High:
            case Ring.Summit:
                return 0.6f;
            default:
                return 0f;
        }
    }

    public static bool ThinAirActive(Ring reached)
    {
        return reached >= Ring.High;
    }
}
}
