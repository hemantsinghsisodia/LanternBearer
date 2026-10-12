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
        Assert.AreEqual(FinalePhase.Islands, FinaleTimeline.PhaseAt(18f));
        Assert.AreEqual(FinalePhase.Sunrise, FinaleTimeline.PhaseAt(36f));
        Assert.AreEqual(FinalePhase.Sunrise, FinaleTimeline.PhaseAt(51.9f));
        Assert.AreEqual(FinalePhase.Credits, FinaleTimeline.PhaseAt(52f));
    }

    [Test]
    public void LengthInSpec()
    {
        Assert.GreaterOrEqual(FinaleTimeline.Total, 45f);
        Assert.LessOrEqual(FinaleTimeline.Total, 60f);
    }

    [Test]
    public void FlaresInsideIslandsPhase()
    {
        float prev = float.NegativeInfinity;
        float[] expected = { 20f, 24f, 28f, 32f };
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
        Assert.AreEqual(0.5f, FinaleTimeline.Phase01(44f, FinalePhase.Sunrise), 0.0001f);
        Assert.AreEqual(1f, FinaleTimeline.Phase01(60f, FinalePhase.Sunrise), 0.0001f);
    }

    [Test]
    public void CameraRisesThenHolds()
    {
        Assert.AreEqual(6f, FinaleTimeline.CameraHeight(0f), 0.001f);
        Assert.AreEqual(120f, FinaleTimeline.CameraHeight(18f), 0.001f);
        Assert.AreEqual(120f, FinaleTimeline.CameraHeight(40f), 0.001f);
        float prev = FinaleTimeline.CameraHeight(0f);
        for (float t = 0.25f; t <= 18f; t += 0.25f)
        {
            float h = FinaleTimeline.CameraHeight(t);
            Assert.GreaterOrEqual(h, prev);
            prev = h;
        }
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
            Assert.AreEqual(1f, FinaleLayout.SilhouetteScale(i), 0.0001f);
        }
    }
}
}
