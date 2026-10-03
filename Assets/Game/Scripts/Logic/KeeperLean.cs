using UnityEngine;

namespace LanternKeeper
{
// Pure mapping for the keeper's wind lean: the target weight and a linear ease toward it.
public static class KeeperLean
{
    public static float TargetWeight(WindPhase phase, float strength01, bool sheltered, bool hasWind)
    {
        if (!hasWind || sheltered || phase != WindPhase.Gust)
        {
            return 0f;
        }
        return Mathf.Clamp01(strength01);
    }

    public static float Step(float current, float target, float dt, float seconds = 0.3f)
    {
        if (dt <= 0f)
        {
            return current;
        }
        return Mathf.MoveTowards(current, target, dt / seconds);
    }
}
}
