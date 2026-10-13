using UnityEngine;

namespace LanternKeeper
{
// Island 5's rising storm surge, as pure numbers. Heights are metres above the sea; the surge starts at the sea.
// Clock: seconds since the first beacon was lit. Nothing happens for StartDelay, then the surge rises RisePerSecond, never above the
// cap (the base of the summit plateau). Each lit beacon then pushes it down PushBack (applied after the cap, so a keeper who has lit all
// eight beacons can never be caught on the way to the summit span), and it never goes below the sea.
//
// RisePerSecond derivation (checks: StormSurgeLogicTests.SteadyClimberStaysAhead and SlowClimberGetsCaught; retuned at the midway gate so
// the storm pressures full-pace players too, StartDelay 45 s -> 8 s):
//  - The keeper walks at 6 m/s (PlayerController.moveSpeed). The spire route is 244 m on the map, 256 m in 3D (BeaconReachabilityTest
//    logs 255.9 m) and climbs 38 m; walking flat out the open stretch climbs at up to about 1.1 m/s.
//  - The full-pace bot (measured): first beacon at 5.5 s, lamp at 48.9 s. The surge starts 8 s after the first beacon, so it is on the
//    mountain for the whole climb. The sim uses a key press per beacon (0.25 s) and the bridge waits that overlap walking (1.2 s, summit 2.5 s).
//  - The surge starts at the sea, so the first seconds are close: the keeper is only about 3 m up when it starts. After that, with the 3 m
//    push-back per beacon, 1.0 m/s keeps full pace 9.8 m ahead at the tightest point of the open stretch (10-30 m up), widening to 21 m
//    there and 26 m at the plateau. 1.1 gives 8.4 m, 1.2 gives 7.0 m, so 1.0 is the largest round rate that holds 8 m or more.
//  - 1.0 m/s stays under the 1.1 m/s peak climb rate, so a caught keeper can still out-climb it on the steep stretches (and every beacon
//    lit takes 3 m off it). A keeper at 70% pace with a player's stops (1.5 s a beacon, the full bridge waits) is caught around the
//    second bridge and gets out again once the summit stretch is reached.
public static class StormSurgeLogic
{
    public const float StartDelay = 8f, PushBack = 3f, CaughtDrain = 3f;
    public const float ChipGap = 25f;
    public const float MusicGap = 15f;
    public const float MusicFloorFar = 0.6f, MusicFloorNear = 0.9f;

    public static float RisePerSecond = 1.0f;

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

        int metres = Mathf.CeilToInt(gap);
        if (metres >= ChipCache.Length)
        {
            return "Storm rising: " + metres + " m below";
        }

        // Cached per whole metre so the chip never allocates a string per frame.
        string text = ChipCache[metres];
        if (text == null)
        {
            text = "Storm rising: " + metres + " m below";
            ChipCache[metres] = text;
        }

        return text;
    }

    static readonly string[] ChipCache = new string[64];

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
