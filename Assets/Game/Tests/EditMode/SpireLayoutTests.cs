using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class SpireLayoutTests
{
    [Test]
    public void SpiralStartsAtBeachEndsAtSummit()
    {
        Vector2[] path = SpireLayout.SpiralPath(200);
        Assert.GreaterOrEqual(path[0].magnitude, 40f);
        Assert.LessOrEqual(Vector2.Distance(path[path.Length - 1], SpireLayout.LampXZ()), 4f);
        Assert.Less(SpireLayout.PathHeight(0f), 1f);
        Assert.Greater(SpireLayout.PathHeight(1f), 38f);
    }

    [Test]
    public void EightBeaconsOrderedUp()
    {
        Assert.AreEqual(8, SpireLayout.BeaconXZ().Length);
        float[] u = SpireLayout.BeaconParameters();
        Assert.AreEqual(8, u.Length);
        for (int i = 1; i < u.Length; i++)
        {
            Assert.Greater(SpireLayout.PathHeight(u[i]), SpireLayout.PathHeight(u[i - 1]));
        }
    }

    [Test]
    public void BeaconsClearOfGapSpans()
    {
        float[] u = SpireLayout.BeaconParameters();
        for (int g = 0; g < 4; g++)
        {
            Vector2 span = SpireLayout.GapSpanU(g);
            foreach (float b in u)
            {
                Assert.IsFalse(b > span.x - 0.005f && b < span.y + 0.005f, "beacon u " + b + " inside gap " + (g + 1));
            }
        }
    }

    [Test]
    public void FourRequiredTwoOptionalGaps()
    {
        SpireGap[] gaps = SpireLayout.Gaps();
        Assert.AreEqual(6, gaps.Length);
        int required = 0;
        for (int i = 0; i < gaps.Length; i++)
        {
            SpireGap g = gaps[i];
            Assert.IsTrue(g.nearBeacon >= 0 && g.nearBeacon < 8 && g.farBeacon >= 0 && g.farBeacon < 8, "gap " + i);
            float span = Vector2.Distance(g.nearXZ, g.farXZ);
            if (g.required)
            {
                required++;
                Assert.IsTrue(span >= 6f && span <= 9f, "required gap " + i + " width " + span);
            }
            else
            {
                Assert.LessOrEqual(span, 12f, "optional gap " + i);
            }
        }

        Assert.AreEqual(4, required);
        Assert.IsTrue(gaps[0].required && gaps[1].required && gaps[2].required && gaps[3].required);
        Assert.LessOrEqual(Vector2.Distance(gaps[3].farXZ, SpireLayout.LampXZ()), 6f);
    }

    [Test]
    public void OptionalGapsAreShortcuts()
    {
        SpireGap[] gaps = SpireLayout.Gaps();
        float[] u = SpireLayout.BeaconParameters();
        for (int i = 4; i < 6; i++)
        {
            float dh = SpireLayout.PathHeight(u[gaps[i].farBeacon]) - SpireLayout.PathHeight(u[gaps[i].nearBeacon]);
            Assert.IsTrue(dh > 0f && dh <= 6f, "optional gap " + i + " height difference " + dh);
        }
    }

    [Test]
    public void LayoutIsDeterministic()
    {
        CollectionAssert.AreEqual(SpireLayout.SpiralPath(50), SpireLayout.SpiralPath(50));
        CollectionAssert.AreEqual(SpireLayout.BeaconXZ(), SpireLayout.BeaconXZ());
        SpireGap[] a = SpireLayout.Gaps();
        SpireGap[] b = SpireLayout.Gaps();
        for (int i = 0; i < a.Length; i++)
        {
            Assert.AreEqual(a[i].nearXZ, b[i].nearXZ);
            Assert.AreEqual(a[i].farXZ, b[i].farXZ);
            Assert.AreEqual(a[i].required, b[i].required);
        }
    }
}
}
