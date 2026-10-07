using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class FireflyCurveTests
{
    [Test]
    public void AtLeastTwoLitAlways()
    {
        foreach (int count in new[] { 6, 4 })
        {
            foreach (float seed in new[] { 0f, 0.37f, 0.99f })
            {
                float period = FireflyCurve.Period(seed, false);
                for (float t = 0f; t < period * 3f; t += 0.01f)
                {
                    Assert.GreaterOrEqual(FireflyCurve.LitCount(t, period, seed, count, false), 2, "count " + count + " seed " + seed + " t " + t);
                }
            }
        }
    }

    [Test]
    public void BlinkRiseIsAboutPointThreeSeconds()
    {
        float peakT = -1f;
        for (float t = 0f; t <= 1f; t += 0.001f)
        {
            if (FireflyCurve.Blink(t, 3f, 0f, 6, false) >= 0.999f)
            {
                peakT = t;
                break;
            }
        }

        Assert.AreEqual(0.3f, peakT, 0.01f);
    }

    [Test]
    public void DarkGapWithinOneToThreeSeconds()
    {
        foreach (float period in new[] { 2.6f, 3.4f })
        {
            float dark = 0f;
            const float dt = 0.005f;
            for (float t = 0f; t < period; t += dt)
            {
                if (FireflyCurve.Blink(t, period, 0f, 6, false) < 0.05f)
                {
                    dark += dt;
                }
            }

            Assert.GreaterOrEqual(dark, 1f, "period " + period);
            Assert.LessOrEqual(dark, 3f, "period " + period);
        }
    }

    [Test]
    public void ReduceFlashingFloor()
    {
        for (float t = 0f; t < 7f; t += 0.01f)
        {
            Assert.GreaterOrEqual(FireflyCurve.Blink(t, 3f, 0.2f, 6, true), 0.3f - 1e-6f);
        }
    }

    [Test]
    public void StreamReachesTargetAtHalfSecond()
    {
        Assert.AreEqual(1f, FireflyCurve.Stream01(0.5f), 1e-6f);
        Vector3 s = new Vector3(1f, 2f, 3f);
        Vector3 target = new Vector3(-4f, 1f, 6f);
        Assert.Less(Vector3.Distance(FireflyCurve.StreamPoint(s, target, 1f, 1f), target), 1e-4f);
        Assert.Less(Vector3.Distance(FireflyCurve.StreamPoint(s, target, -1f, 0f), s), 1e-4f);
        Assert.AreEqual(0f, FireflyCurve.StreamScale(1f), 1e-6f);
        Assert.AreEqual(1f, FireflyCurve.StreamScale(0.5f), 1e-6f);
    }

    [Test]
    public void LingerEndsByTwoSeconds()
    {
        Assert.AreEqual(0f, FireflyCurve.LingerAlpha(2f), 1e-6f);
        Assert.AreEqual(1f, FireflyCurve.LingerAlpha(1f), 1e-6f);
        for (float t = 0f; t < 2f; t += 0.1f)
        {
            Assert.AreEqual(0.3f, FireflyCurve.LingerOffset(0, t).magnitude, 0.01f);
        }
    }

    [Test]
    public void FadeInDoneByOneSecond()
    {
        foreach (int count in new[] { 6, 4 })
        {
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(1f, FireflyCurve.FadeIn(i, count, 1f), 1e-6f);
                if (i > 0)
                {
                    Assert.AreEqual(0f, FireflyCurve.FadeIn(i, count, 0f), 1e-6f);
                }
            }
        }
    }

    [Test]
    public void PeriodRange()
    {
        Assert.AreEqual(2.6f, FireflyCurve.Period(0f, false), 1e-6f);
        Assert.AreEqual(3.4f, FireflyCurve.Period(1f, false), 1e-5f);
        Assert.AreEqual(2.6f * 1.3f, FireflyCurve.Period(0f, true), 1e-5f);
    }
}
}
