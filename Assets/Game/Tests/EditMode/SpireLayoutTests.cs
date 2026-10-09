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
        Vector2[] xz = SpireLayout.BeaconXZ();
        for (int g = 0; g < 4; g++)
        {
            Vector2 span = SpireLayout.GapSpanU(g);
            Vector2 near = SpireLayout.PathPoint(span.x);
            Vector2 far = SpireLayout.PathPoint(span.y);
            for (int i = 0; i < u.Length; i++)
            {
                Assert.IsFalse(u[i] >= span.x && u[i] <= span.y, "beacon " + i + " inside gap " + (g + 1));
                Assert.GreaterOrEqual(Vector2.Distance(xz[i], near), 1.5f, "beacon " + i + " near gap " + (g + 1));
                Assert.GreaterOrEqual(Vector2.Distance(xz[i], far), 1.5f, "beacon " + i + " near gap " + (g + 1));
            }
        }

        Assert.Less(u[u.Length - 1], SpireLayout.GapSpanU(3).x, "gap 4 must start after the last beacon");
        for (int i = 1; i < u.Length; i++)
        {
            float length = 0f;
            Vector2 previous = SpireLayout.PathPoint(u[i - 1]);
            for (int s = 1; s <= 200; s++)
            {
                Vector2 point = SpireLayout.PathPoint(u[i - 1] + (u[i] - u[i - 1]) * s / 200f);
                length += Vector2.Distance(previous, point);
                previous = point;
            }

            Assert.GreaterOrEqual(length, 12f, "beacons " + (i - 1) + " and " + i + " too close along the path");
        }
    }

    [Test]
    public void BeaconsSpreadAcrossRings()
    {
        int low = 0, mid = 0, high = 0;
        foreach (float u in SpireLayout.BeaconParameters())
        {
            Ring ring = RingLogic.Classify(SpireLayout.PathHeight(u));
            low += ring == Ring.Low ? 1 : 0;
            mid += ring == Ring.Mid ? 1 : 0;
            high += ring == Ring.High ? 1 : 0;
        }

        Assert.GreaterOrEqual(low, 3);
        Assert.GreaterOrEqual(mid, 2);
        Assert.GreaterOrEqual(high, 2);
    }

    [Test]
    public void RequiredGapsReferenceTheirNearBeacon()
    {
        SpireGap[] gaps = SpireLayout.Gaps();
        Assert.AreEqual(1, gaps[0].nearBeacon);
        Assert.AreEqual(3, gaps[1].nearBeacon);
        Assert.AreEqual(5, gaps[2].nearBeacon);
        Assert.AreEqual(7, gaps[3].nearBeacon);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(-1, gaps[i].farBeacon);
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
            Assert.IsTrue(g.nearBeacon >= 0 && g.nearBeacon < 8 && g.farBeacon >= -1 && g.farBeacon < 8, "gap " + i);
            Assert.IsTrue(g.required || g.farBeacon >= 0, "optional gap " + i + " needs a far beacon");
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
        for (int i = 4; i < 6; i++)
        {
            Assert.AreNotEqual(gaps[i].nearBeacon, gaps[i].farBeacon, "optional gap " + i);
            Assert.LessOrEqual(Vector2.Distance(gaps[i].nearXZ, gaps[i].farXZ), 12f, "optional gap " + i);
            Vector2 span = SpireLayout.OptionalSpanU(i - 4);
            float dh = SpireLayout.PathHeight(span.y) - SpireLayout.PathHeight(span.x);
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
