using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
// The finale's cloud banks and god rays: layer count per preset, the rays' preset and Reduce flashing limits, and the timing off the timeline.
public class FinaleCloudMathTests
{
    [Test]
    public void ThreeLayersNormallyOneOnLow()
    {
        Assert.AreEqual(3, FinaleCloudMath.LayerCount(false));
        Assert.AreEqual(1, FinaleCloudMath.LayerCount(true));
    }

    [Test]
    public void GodRaysAreOffOnLow()
    {
        Assert.IsFalse(FinaleCloudMath.RaysEnabled(true));
        Assert.IsTrue(FinaleCloudMath.RaysEnabled(false));
        for (float t = 0f; t <= FinaleTimeline.Total; t += 0.5f)
        {
            Assert.AreEqual(0f, FinaleCloudMath.RayStrength(t, false, true), "no rays on Low at " + t);
        }
    }

    [Test]
    public void GodRaysOnlyInTheSunrise()
    {
        Assert.AreEqual(0f, FinaleCloudMath.RayStrength(FinaleTimeline.SunriseStart - 0.01f, false, false), 1e-6f);
        Assert.AreEqual(0f, FinaleCloudMath.RayStrength(FinaleTimeline.BeamStart, false, false), 1e-6f);
        Assert.Greater(FinaleCloudMath.RayStrength(FinaleTimeline.Total, false, false), 0.3f, "the rays are up by the end of the sunrise");
    }

    [Test]
    public void ReduceFlashingCapsTheGodRays()
    {
        for (float t = FinaleTimeline.SunriseStart; t <= FinaleTimeline.Total; t += 0.5f)
        {
            float normal = FinaleCloudMath.RayStrength(t, false, false);
            float reduced = FinaleCloudMath.RayStrength(t, true, false);
            Assert.LessOrEqual(reduced, normal + 1e-6f, "reduced never exceeds normal at " + t);
            Assert.LessOrEqual(reduced, FinaleCloudMath.RayPeak * SettingsMath.FlashCap(true) + 1e-6f, "reduced stays under the cap at " + t);
            Assert.AreEqual(normal * SettingsMath.FlashCap(true), reduced, 1e-5f, "the cap scales the strength at " + t);
        }
    }

    [Test]
    public void CrossFadeStartsWithTheBreak()
    {
        Assert.AreEqual(0f, FinaleCloudMath.CrossFade(FinaleTimeline.BeamStart - 0.01f), 1e-6f, "no clouds before the storm breaks");
        Assert.AreEqual(1f, FinaleCloudMath.CrossFade(FinaleTimeline.BeamStart + FinaleCloudMath.CrossFadeSeconds), 1e-6f);
        float mid = FinaleCloudMath.CrossFade(FinaleTimeline.BeamStart + FinaleCloudMath.CrossFadeSeconds * 0.5f);
        Assert.Greater(mid, 0.1f);
        Assert.Less(mid, 0.9f);
    }

    [Test]
    public void BreakAndDawnFollowThePhases()
    {
        Assert.AreEqual(0f, FinaleCloudMath.Break01(FinaleTimeline.BeamStart), 1e-6f);
        Assert.AreEqual(1f, FinaleCloudMath.Break01(FinaleTimeline.IslandsStart), 1e-6f);
        Assert.AreEqual(0f, FinaleCloudMath.Dawn01(FinaleTimeline.SunriseStart), 1e-6f);
        Assert.AreEqual(1f, FinaleCloudMath.Dawn01(FinaleTimeline.Total), 1e-6f);
        Assert.AreEqual(1f, FinaleCloudMath.GapScale(FinaleTimeline.SunriseStart), 1e-6f);
        Assert.AreEqual(FinaleCloudMath.SunriseGapScale, FinaleCloudMath.GapScale(FinaleTimeline.Total), 1e-6f);
    }

    [Test]
    public void GapDriftsTheShortWayToTheSun()
    {
        Assert.AreEqual(0.9f, FinaleCloudMath.GapBearing(0.9f, 0.1f, FinaleTimeline.IslandsStart), 1e-5f, "starts at the beam bearing");
        Assert.AreEqual(0.1f, FinaleCloudMath.GapBearing(0.9f, 0.1f, FinaleTimeline.SunriseStart), 1e-5f, "ends at the sun");
        float midway = FinaleCloudMath.GapBearing(0.9f, 0.1f, (FinaleTimeline.IslandsStart + FinaleTimeline.SunriseStart) * 0.5f);
        Assert.AreEqual(0f, Mathf.Min(midway, 1f - midway), 1e-4f, "through north, the short way round");
    }
}
}
