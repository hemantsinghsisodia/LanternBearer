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
//  - H4 retune: RisePerSecond 1.0 -> 1.4, PushBack 3 -> 5 (StartDelay, drain and cap unchanged). The surge now outpaces the steepest
//    climb rate (about 1.1 m/s), so a caught keeper no longer out-climbs it: recovery is by lighting the next beacons (5 m push-back each).
//    StormSurgeLogicTests.CaughtKeeperAt85PercentCanRecover proves it with a fuel model (Normal x island 1.6 drain, x3 while caught, 15 point
//    beacon cost with a 3 point refund, route fireflies): at 4 m the 85% keeper is caught for 16 s and the x3 drain empties the lantern before the
//    lamp; at 5 m it is caught for 5 s, clears the surge by beacon 5 and reaches the lamp with fuel left; at 6 m it is never caught (which would
//    break SlowClimberGetsCaught). Fuel is thin for a slow keeper: with 60% of the fireflies grabbed it starves 0.2 points short of the lamp.
//  - The surge starts at the sea, so the first seconds are close: the full-pace keeper is only 2.8 m up when it starts (the lowest gap
//    anywhere). With the 5 m push-back per beacon, 1.4 m/s keeps full pace 10 m ahead at the tightest point of the open stretch
//    (10-30 m up), widening to 29 m there and 38 m at the plateau; never caught.
//  - A keeper at 85% pace with a player's stops (1.5 s a beacon, the full bridge waits) is caught around the third or fourth beacon
//    (lowest gap -3 m) and recovers by lighting the next beacons.
public static class StormSurgeLogic
{
    public const float StartDelay = 8f, PushBack = 5f, CaughtDrain = 3f;
    public const float ChipGap = 25f;
    public const float MusicGap = 15f;
    public const float MusicFloorFar = 0.6f, MusicFloorNear = 0.9f;

    public static float RisePerSecond = 1.4f;

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
