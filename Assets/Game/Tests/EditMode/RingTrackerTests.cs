using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class RingTrackerTests
{
    const float Frame = 1f / 60f;

    static bool Stand(RingTracker tracker, float height, float seconds)
    {
        bool raised = false;
        int frames = Mathf.CeilToInt(seconds / Frame);
        for (int i = 0; i < frames; i++)
        {
            raised |= tracker.Step(true, height, Frame);
        }

        return raised;
    }

    [Test]
    public void StaysLowOnLowGround()
    {
        RingTracker tracker = new RingTracker();
        Assert.IsFalse(Stand(tracker, 4f, 3f));
        Assert.AreEqual(Ring.Low, tracker.Reached);
    }

    [Test]
    public void NewRingNeedsHalfASecondGrounded()
    {
        RingTracker tracker = new RingTracker();
        Assert.IsFalse(Stand(tracker, 12f, 0.4f), "0.4 s is not enough");
        Assert.AreEqual(Ring.Low, tracker.Reached);
        Assert.IsTrue(Stand(tracker, 12f, 0.2f), "0.6 s is");
        Assert.AreEqual(Ring.Mid, tracker.Reached);
    }

    [Test]
    public void JumpApexNeverRaisesTheRing()
    {
        RingTracker tracker = new RingTracker();
        Stand(tracker, 22.6f, 1f);
        Assert.AreEqual(Ring.Mid, tracker.Reached);
        // A 2.8 m jump from 22.6 m peaks at 25.4 m, above the High boundary. Airborne frames never count.
        for (int i = 0; i < 120; i++)
        {
            float t = i / 120f;
            float height = 22.6f + 2.8f * 4f * t * (1f - t);
            Assert.IsFalse(tracker.Step(false, height, Frame));
        }

        Assert.AreEqual(Ring.Mid, tracker.Reached);
        Assert.AreEqual(22.6f, tracker.LastGroundedHeight, 1e-4f);
    }

    [Test]
    public void LeavingTheNewBandResetsTheClock()
    {
        RingTracker tracker = new RingTracker();
        Stand(tracker, 26f, 0.4f);
        Stand(tracker, 20f, 0.2f);
        Assert.AreEqual(Ring.Low, tracker.Reached);
        Assert.IsFalse(Stand(tracker, 26f, 0.4f), "the clock restarted");
        Assert.IsTrue(Stand(tracker, 26f, 0.2f));
        Assert.AreEqual(Ring.High, tracker.Reached);
    }

    [Test]
    public void RatchetSurvivesDescent()
    {
        RingTracker tracker = new RingTracker();
        Stand(tracker, 30f, 1f);
        Assert.AreEqual(Ring.High, tracker.Reached);
        Stand(tracker, 2f, 3f);
        Assert.AreEqual(Ring.High, tracker.Reached);
    }

    [Test]
    public void ResetGoesBackToLow()
    {
        RingTracker tracker = new RingTracker();
        Stand(tracker, 39f, 1f);
        Assert.AreEqual(Ring.Summit, tracker.Reached);
        tracker.Reset();
        Assert.AreEqual(Ring.Low, tracker.Reached);
        Assert.IsFalse(tracker.Step(false, 50f, Frame), "no ground yet, nothing counts");
    }
}

public class WalkwaySpawnTests
{
    [Test]
    public void RollsLandOnTheWalkwayAtOrBelowTheRing()
    {
        Ring[] rings = { Ring.Low, Ring.Mid, Ring.High, Ring.Summit };
        for (int r = 0; r < rings.Length; r++)
        {
            int hits = 0;
            for (int i = 0; i < 200; i++)
            {
                Vector2 xz;
                if (!WalkwaySpawn.TryRoll(rings[r], (i * 0.61803f) % 1f, (i * 0.37f) % 1f, out xz))
                {
                    continue;
                }

                hits++;
                Assert.IsTrue(WalkwaySpawn.OnWalkway(xz), "on the walkway");
                float u = SpireLayout.NearestU(xz);
                Assert.LessOrEqual(RingLogic.Classify(SpireLayout.PathHeight(u)), rings[r], "ring " + rings[r] + " at u " + u);
            }

            Assert.Greater(hits, 150, "most rolls succeed for " + rings[r]);
        }
    }

    [Test]
    public void RingLimitsGrowWithTheRing()
    {
        float low = WalkwaySpawn.MaxU(Ring.Low);
        float mid = WalkwaySpawn.MaxU(Ring.Mid);
        float high = WalkwaySpawn.MaxU(Ring.High);
        Assert.Greater(low, 0.05f);
        Assert.Less(low, mid);
        Assert.Less(mid, high);
        Assert.Less(high, 1f);
        Assert.AreEqual(1f, WalkwaySpawn.MaxU(Ring.Summit));
    }

    [Test]
    public void FarFromThePathIsRefused()
    {
        Assert.IsFalse(WalkwaySpawn.OnWalkway(new Vector2(0f, 0f)), "the lamp plateau centre is off the path");
        Vector2 onPath = SpireLayout.PathPoint(0.05f);
        Assert.IsTrue(WalkwaySpawn.Allowed(onPath, Ring.Low));
        Assert.IsFalse(WalkwaySpawn.Allowed(SpireLayout.PathPoint(0.9f), Ring.Low), "summit stretch is not allowed at Low");
    }
}
}
