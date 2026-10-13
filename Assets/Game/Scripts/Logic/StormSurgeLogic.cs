using UnityEngine;

namespace LanternKeeper
{
// Island 5's rising storm surge, as pure numbers. Heights are metres above the sea; the surge starts at the sea.
// Clock: seconds since the first beacon was lit. Nothing happens for StartDelay, then the surge rises RisePerSecond, never above the
// cap (the base of the summit plateau). Each lit beacon then pushes it down PushBack (applied after the cap, so a keeper who has lit all
// eight beacons can never be caught on the way to the summit span), and it never goes below the sea.
//
// RisePerSecond derivation (the "steady climber" check is StormSurgeLogicTests.SteadyClimberStaysAhead):
//  - The keeper walks at 6 m/s (PlayerController.moveSpeed). The spire route is 244 m on the map, 256 m in 3D (BeaconReachabilityTest
//    logs 255.9 m), and the climb rises 38 m, steepest mid-route at about 1.1 m/s of height when walking flat out.
//  - A bot walking the whole route at full pace needs 49 s: first beacon at 5.5 s, lamp at 48.9 s. It is done about when the surge starts
//    (first beacon + 45 s), so it is never near it.
//  - The "steady climber" is an unhurried player: 40% of the walking pace (2.4 m/s over the ground), stopping 1.5 s to light each beacon,
//    2.5 s at each of the first three bridges (BridgeTimings.TotalSeconds) and 3.7 s at the summit span (ColumnSeconds + TotalSeconds).
//    Their gap to the surge starts large (the surge begins at the sea) and closes as the surge rises; RisePerSecond is set so the
//    tightest point of that climb is 8 to 12 m, while a bot at full pace stays far ahead.
//  - A keeper who walks flat out out-climbs the surge on the steep stretches (peak about 1.1 m/s), so one who is caught can climb out.
public static class StormSurgeLogic
{
    public const float StartDelay = 45f, PushBack = 3f, CaughtDrain = 4f;
    public const float ChipGap = 25f;
    public const float MusicGap = 15f;
    public const float MusicFloorFar = 0.6f, MusicFloorNear = 0.9f;

    public static float RisePerSecond = 0.8f;

    // Surge height above the sea. `capHeight` is the height of the summit plateau base above the sea.
    public static float Height(float secondsSinceStart, int beaconsLit, float capHeight)
    {
        float rising = Mathf.Max(0f, secondsSinceStart - StartDelay) * RisePerSecond;
        float top = Mathf.Min(rising, Mathf.Max(0f, capHeight));
        return Mathf.Max(0f, top - PushBack * Mathf.Max(0, beaconsLit));
    }

    public static bool Caught(float keeperY, float surgeY)
    {
        return keeperY < surgeY;
    }

    // Metres the keeper stands above the surge (negative when caught).
    public static float Gap(float keeperY, float surgeY)
    {
        return keeperY - surgeY;
    }

    public static bool ChipVisible(float gap)
    {
        return gap < ChipGap;
    }

    // The chip text; "N m below" is the surge's distance below the keeper, rounded up so it never reads 0 before the surge arrives.
    public static string ChipText(float gap)
    {
        if (gap <= 0f)
        {
            return "In the storm! Climb!";
        }

        return "Storm rising: " + Mathf.CeilToInt(gap) + " m below";
    }

    // The music tension floor: the ring floor, raised from 0.6 to 0.9 as the gap closes from MusicGap to 0. Never below the ring floor.
    public static float MusicFloor(float gap, float ringFloor)
    {
        if (gap > MusicGap)
        {
            return ringFloor;
        }

        float surge = Mathf.Lerp(MusicFloorNear, MusicFloorFar, Mathf.Clamp01(gap / MusicGap));
        return Mathf.Max(ringFloor, surge);
    }
}
}
