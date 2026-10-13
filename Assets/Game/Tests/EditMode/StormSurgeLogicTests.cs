using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class StormSurgeLogicTests
{
    const float Cap = 37f;

    [Test]
    public void StartsAfterDelay()
    {
        Assert.AreEqual(0f, StormSurgeLogic.Height(0f, 1, Cap));
        Assert.AreEqual(0f, StormSurgeLogic.Height(StormSurgeLogic.StartDelay, 0, Cap), 1e-4f);
        Assert.Greater(StormSurgeLogic.Height(StormSurgeLogic.StartDelay + 10f, 0, Cap), 0f);
        Assert.AreEqual(10f * StormSurgeLogic.RisePerSecond, StormSurgeLogic.Height(StormSurgeLogic.StartDelay + 10f, 0, Cap), 1e-3f);
    }

    [Test]
    public void PushBackPerBeacon()
    {
        float t = StormSurgeLogic.StartDelay + 40f;
        float none = StormSurgeLogic.Height(t, 0, Cap);
        Assert.AreEqual(none - StormSurgeLogic.PushBack, StormSurgeLogic.Height(t, 1, Cap), 1e-3f);
        Assert.AreEqual(none - 3f * StormSurgeLogic.PushBack, StormSurgeLogic.Height(t, 3, Cap), 1e-3f);
    }

    [Test]
    public void CappedAtPlateau()
    {
        Assert.AreEqual(Cap, StormSurgeLogic.Height(10000f, 0, Cap), 1e-4f);
        Assert.AreEqual(Cap - 24f, StormSurgeLogic.Height(10000f, 8, Cap), 1e-4f, "the push applies after the cap");
    }

    [Test]
    public void NeverBelowSea()
    {
        Assert.AreEqual(0f, StormSurgeLogic.Height(StormSurgeLogic.StartDelay + 1f, 8, Cap));
        Assert.AreEqual(0f, StormSurgeLogic.Height(-5f, 0, Cap));
        Assert.AreEqual(0f, StormSurgeLogic.Height(100f, 99, Cap));
    }

    [Test]
    public void CaughtAndGap()
    {
        Assert.IsTrue(StormSurgeLogic.Caught(4f, 5f));
        Assert.IsFalse(StormSurgeLogic.Caught(5f, 5f));
        Assert.AreEqual(6f, StormSurgeLogic.Gap(11f, 5f), 1e-4f);
        Assert.AreEqual(-2f, StormSurgeLogic.Gap(3f, 5f), 1e-4f);
    }

    [Test]
    public void ChipShowsUnderTwentyFiveMetres()
    {
        Assert.IsFalse(StormSurgeLogic.ChipVisible(25f));
        Assert.IsTrue(StormSurgeLogic.ChipVisible(24.9f));
        Assert.AreEqual("Storm rising: 10 m below", StormSurgeLogic.ChipText(9.4f));
        Assert.AreEqual("Storm rising: 1 m below", StormSurgeLogic.ChipText(0.2f));
    }

    [Test]
    public void MusicFloorRisesAsTheGapCloses()
    {
        Assert.AreEqual(0.3f, StormSurgeLogic.MusicFloor(20f, 0.3f), 1e-4f, "far away the ring floor rules");
        Assert.AreEqual(0.6f, StormSurgeLogic.MusicFloor(15f, 0.3f), 1e-4f);
        Assert.AreEqual(0.75f, StormSurgeLogic.MusicFloor(7.5f, 0.6f), 1e-4f);
        Assert.AreEqual(0.9f, StormSurgeLogic.MusicFloor(0f, 0.6f), 1e-4f);
        Assert.AreEqual(0.9f, StormSurgeLogic.MusicFloor(-5f, 0.6f), 1e-4f);
        Assert.AreEqual(0.95f, StormSurgeLogic.MusicFloor(3f, 0.95f), 1e-4f, "never lowers the ring ratchet");
    }

    // ---- the steady climber ----
    // Walks the Island 5 route (SpireLayout) at a fraction of the 6 m/s walking speed, stopping to light each beacon and for the bridges,
    // against the surge clock that starts at the first beacon.
    const float WalkSpeed = 6f;
    const float LightSeconds = 1.5f;
    const float BridgeWait = BridgeTimings.TotalSeconds;
    const float SummitWait = BridgeTimings.ColumnSeconds + BridgeTimings.TotalSeconds;

    struct ClimbResult
    {
        public float minGap, maxGap, minGapAt, endTime, gapAtEnd, startGap, lowestGap;
        public int samples;
        public string trace;
    }

    static float[] RouteArcLengths(int n)
    {
        float[] cum = new float[n + 1];
        Vector2 previous = SpireLayout.PathPoint(0f);
        float previousHeight = SpireLayout.PathHeightSmooth(0f);
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)n;
            Vector2 p = SpireLayout.PathPoint(u);
            float h = SpireLayout.PathHeightSmooth(u);
            cum[i] = cum[i - 1] + Mathf.Sqrt((p - previous).sqrMagnitude + (h - previousHeight) * (h - previousHeight));
            previous = p;
            previousHeight = h;
        }

        return cum;
    }

    static ClimbResult Climb(float paceFraction, float lightSeconds, float bridgeWait, float summitWait)
    {
        const int n = 4000;
        float[] cum = RouteArcLengths(n);
        float[] beacons = SpireLayout.BeaconParameters();
        float[] stops = new float[12];
        float[] waits = new float[12];
        bool[] isBeacon = new bool[12];
        for (int i = 0; i < 8; i++)
        {
            stops[i] = beacons[i];
            waits[i] = lightSeconds;
            isBeacon[i] = true;
        }

        for (int g = 0; g < 4; g++)
        {
            stops[8 + g] = SpireLayout.GapSpanU(g).x;
            waits[8 + g] = g == 3 ? summitWait : bridgeWait;
        }

        // Sort the stops by u (insertion sort over 12 entries).
        for (int i = 1; i < 12; i++)
        {
            for (int j = i; j > 0 && stops[j] < stops[j - 1]; j--)
            {
                float u = stops[j]; stops[j] = stops[j - 1]; stops[j - 1] = u;
                float w = waits[j]; waits[j] = waits[j - 1]; waits[j - 1] = w;
                bool b = isBeacon[j]; isBeacon[j] = isBeacon[j - 1]; isBeacon[j - 1] = b;
            }
        }

        ClimbResult result = new ClimbResult { minGap = float.MaxValue, lowestGap = float.MaxValue, startGap = -1f, trace = "" };
        const float dt = 0.05f;
        float speed = WalkSpeed * paceFraction;
        float time = 0f, distance = 0f, wait = 0f, firstLit = -1f, nextTrace = 0f;
        int lit = 0, next = 0, index = 0;
        for (int step = 0; step < 400000; step++)
        {
            while (index < n && cum[index] < distance)
            {
                index++;
            }

            float climbU = index / (float)n;
            float keeperY = SpireLayout.PathHeightSmooth(climbU);
            if (next < 12 && climbU >= stops[next])
            {
                wait = waits[next];
                if (isBeacon[next])
                {
                    lit++;
                    if (firstLit < 0f)
                    {
                        firstLit = time;
                    }
                }

                next++;
            }

            if (firstLit >= 0f)
            {
                float since = time - firstLit;
                float surge = StormSurgeLogic.Height(since, lit, SpireLayout.PathTopHeight - 1.5f);
                if (since >= StormSurgeLogic.StartDelay && keeperY >= 1f)
                {
                    float gap = StormSurgeLogic.Gap(keeperY, surge);
                    if (result.startGap < 0f)
                    {
                        result.startGap = gap;
                    }

                    result.samples++;
                    result.lowestGap = Mathf.Min(result.lowestGap, gap);
                    if (keeperY >= 10f && keeperY <= 30f)
                    {
                        result.maxGap = Mathf.Max(result.maxGap, gap);
                        if (gap < result.minGap)
                        {
                            result.minGap = gap;
                            result.minGapAt = time;
                        }
                    }

                    result.gapAtEnd = gap;
                    if (time >= nextTrace)
                    {
                        result.trace += "[" + time.ToString("F0") + "s lit" + lit + " keeper " + keeperY.ToString("F1") + " surge " + surge.ToString("F1") + " gap " + gap.ToString("F1") + "] ";
                        nextTrace = time + 10f;
                    }
                }
            }

            if (climbU >= 0.999f)
            {
                result.endTime = time;
                break;
            }

            if (wait > 0f)
            {
                wait -= dt;
            }
            else
            {
                distance += speed * dt;
            }

            time += dt;
        }

        if (result.samples == 0)
        {
            // The climb finished before the surge started: it was never closer than the whole height.
            result.minGap = float.MaxValue;
            result.gapAtEnd = float.MaxValue;
        }

        return result;
    }

    // The full-pace bot's stops are the ones BeaconReachabilityTest measured (beacon 1 at 5.5 s, lamp at 48.9 s): a key press per beacon and
    // the bridge waits overlapped with walking. A player's stops are longer (1.5 s a beacon, the full bridge waits).
    static ClimbResult FullPace()
    {
        return Climb(1f, 0.25f, 1.2f, 2.5f);
    }

    static ClimbResult Player(float pace)
    {
        return Climb(pace, 1.5f, BridgeWait, SummitWait);
    }

    [Test]
    public void SteadyClimberStaysAhead()
    {
        // Full walking pace: while the keeper is on the route's open stretch (10 to 30 m up) the surge is 8 to 12 m behind at its tightest.
        ClimbResult full = FullPace();
        Debug.Log("StormSurge full pace: ends " + full.endTime.ToString("F0") + " s, start gap " + full.startGap.ToString("F1")
            + " m, tightest (10-30 m up) " + full.minGap.ToString("F1") + " m at " + full.minGapAt.ToString("F0") + " s, widest " + full.maxGap.ToString("F1")
            + " m, lowest ever " + full.lowestGap.ToString("F1") + " m, gap at the plateau " + full.gapAtEnd.ToString("F1") + " m. " + full.trace);
        Assert.Greater(full.samples, 100, "the surge must be active while the full-pace climber is still climbing");
        Assert.GreaterOrEqual(full.minGap, 8f);
        Assert.LessOrEqual(full.minGap, 12f);
        Assert.GreaterOrEqual(full.lowestGap, 0f, "never caught");
    }

    [Test]
    public void SlowClimberGetsCaught()
    {
        // 70% pace with a player's stops: the surge catches up at some point before the summit.
        ClimbResult slow = Player(0.7f);
        Debug.Log("StormSurge 0.7 pace: ends " + slow.endTime.ToString("F0") + " s, lowest gap " + slow.lowestGap.ToString("F1") + " m. " + slow.trace);
        Assert.Less(slow.lowestGap, 0f, "caught on the way up");
        // Anyone slower than that is caught too.
        Assert.Less(Player(0.4f).lowestGap, 0f);
    }

    [Test]
    public void WaitingAtTheBeachGetsCaught()
    {
        float beachY = SpireLayout.PathBaseHeight + 0.1f;
        float t = StormSurgeLogic.StartDelay + 15f;
        Assert.IsTrue(StormSurgeLogic.Caught(beachY, StormSurgeLogic.Height(t, 1, 37f)), "23 s after the first beacon, a keeper on the beach is under");
    }
}
}
