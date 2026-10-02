using UnityEngine;

namespace LanternKeeper
{
// Pure difficulty tuning for the Storm Cape. difficulty: 0 Easy, 1 Normal, 2 Hard.
public static class StormTuning
{
    public const float WalkSpeed = 3.5f;
    public const float PeakPush = 2.5f;

    public static float GustMultiplier(int difficulty)
    {
        if (difficulty <= 0)
        {
            return 0.7f;
        }

        if (difficulty >= 2)
        {
            return 1.25f;
        }

        return 1f;
    }

    public static int MaxShades(int difficulty)
    {
        if (difficulty <= 0)
        {
            return 3;
        }

        if (difficulty >= 2)
        {
            return 6;
        }

        return 5;
    }

    public static float ShadeSteal(int difficulty)
    {
        if (difficulty <= 0)
        {
            return 10f;
        }

        if (difficulty >= 2)
        {
            return 20f;
        }

        return 15f;
    }

    public static Vector2 LightningInterval(int difficulty)
    {
        if (difficulty <= 0)
        {
            return new Vector2(30f, 45f);
        }

        if (difficulty >= 2)
        {
            return new Vector2(45f, 65f);
        }

        return new Vector2(35f, 55f);
    }

    public static int ShadeTarget(int baseCount, int litCount, int cap)
    {
        return Mathf.Min(cap, baseCount + litCount / 3);
    }
}
}
