using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class FinaleTimelineTests
{
    [Test]
    public void PhasesInOrder()
    {
        Assert.AreEqual(FinalePhase.Ignition, FinaleTimeline.PhaseAt(-1f));
        Assert.AreEqual(FinalePhase.Ignition, FinaleTimeline.PhaseAt(0f));
        Assert.AreEqual(FinalePhase.Ignition, FinaleTimeline.PhaseAt(5.9f));
        Assert.AreEqual(FinalePhase.BeamAndStorm, FinaleTimeline.PhaseAt(6f));
        Assert.AreEqual(FinalePhase.BeamAndStorm, FinaleTimeline.PhaseAt(17.9f));
        Assert.AreEqual(FinalePhase.Islands, FinaleTimeline.PhaseAt(18f));
        Assert.AreEqual(FinalePhase.Islands, FinaleTimeline.PhaseAt(41.9f));
        Assert.AreEqual(FinalePhase.Sunrise, FinaleTimeline.PhaseAt(42f));
        Assert.AreEqual(FinalePhase.Sunrise, FinaleTimeline.PhaseAt(59.9f));
        Assert.AreEqual(FinalePhase.Hold, FinaleTimeline.PhaseAt(60f));
        Assert.AreEqual(FinalePhase.Hold, FinaleTimeline.PhaseAt(65.9f));
        Assert.AreEqual(FinalePhase.Credits, FinaleTimeline.PhaseAt(66f));
    }

    [Test]
    public void PhaseEnumIsOrderedWithHoldBeforeCredits()
    {
        Assert.Less((int)FinalePhase.Sunrise, (int)FinalePhase.Hold);
        Assert.Less((int)FinalePhase.Hold, (int)FinalePhase.Credits);
    }

    [Test]
    public void TimelineValues()
    {
        Assert.AreEqual(0f, FinaleTimeline.IgnitionStart, 0.0001f);
        Assert.AreEqual(6f, FinaleTimeline.BeamStart, 0.0001f);
        Assert.AreEqual(18f, FinaleTimeline.IslandsStart, 0.0001f);
        Assert.AreEqual(42f, FinaleTimeline.SunriseStart, 0.0001f);
        Assert.AreEqual(60f, FinaleTimeline.HoldStart, 0.0001f);
        Assert.AreEqual(66f, FinaleTimeline.Total, 0.0001f);
        Assert.AreEqual(18f, FinaleTimeline.SunriseLength, 0.0001f);
        Assert.AreEqual(FinaleTimeline.HoldStart - FinaleTimeline.SunriseStart, FinaleTimeline.SunriseLength, 0.0001f, "the dawn fills the sunrise phase");
    }

    [Test]
    public void LengthInSpec()
    {
        Assert.GreaterOrEqual(FinaleTimeline.Total, 60f);
        Assert.LessOrEqual(FinaleTimeline.Total, 75f, "the credits roll (75 s or less) and the 80.6 s finale cue still cover it");
    }

    [Test]
    public void FlaresInsideIslandsPhase()
    {
        float prev = float.NegativeInfinity;
        float[] expected = { 21f, 27f, 33f, 39f };
        for (int i = 0; i < 4; i++)
        {
            float t = FinaleTimeline.IslandFlareTime(i);
            Assert.GreaterOrEqual(t, FinaleTimeline.IslandsStart);
            Assert.Less(t, FinaleTimeline.SunriseStart);
            Assert.Greater(t, prev);
            Assert.AreEqual(expected[i], t, 0.0001f);
            prev = t;
        }
    }

    [Test]
    public void Phase01IsClamped()
    {
        Assert.AreEqual(0f, FinaleTimeline.Phase01(0f, FinalePhase.Sunrise), 0.0001f);
        Assert.AreEqual(0.5f, FinaleTimeline.Phase01(51f, FinalePhase.Sunrise), 0.0001f);
        Assert.AreEqual(1f, FinaleTimeline.Phase01(60f, FinalePhase.Sunrise), 0.0001f);
        Assert.AreEqual(0f, FinaleTimeline.Phase01(59f, FinalePhase.Hold), 0.0001f);
        Assert.AreEqual(0.5f, FinaleTimeline.Phase01(63f, FinalePhase.Hold), 0.0001f);
        Assert.AreEqual(1f, FinaleTimeline.Phase01(70f, FinalePhase.Hold), 0.0001f);
    }

    [Test]
    public void SilhouettesOnTheHorizon()
    {
        float prevBearing = float.NaN;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = FinaleLayout.SilhouetteXZ(i);
            Assert.AreEqual(FinaleLayout.SilhouetteDistance, p.magnitude, 0.01f);
            float bearing = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;
            if (i > 0) Assert.AreEqual(45f, bearing - prevBearing, 0.01f);
            prevBearing = bearing;
            Assert.AreEqual(2f, FinaleLayout.SilhouetteScale(i), 0.0001f);
        }
    }
}
}
