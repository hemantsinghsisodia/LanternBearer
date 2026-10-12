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
        Assert.LessOrEqual(Vector2.Distance(path[path.Length - 1], SpireLayout.LampXZ()), 6.01f);
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
    public void FourRequiredGaps()
    {
        SpireGap[] gaps = SpireLayout.Gaps();
        Assert.AreEqual(4, gaps.Length);
        float[] beaconU = SpireLayout.BeaconParameters();
        for (int i = 0; i < gaps.Length; i++)
        {
            SpireGap g = gaps[i];
            Assert.IsTrue(g.required, "gap " + i + " required");
            Assert.IsTrue(g.nearBeacon >= 0 && g.nearBeacon < beaconU.Length && g.farBeacon >= -1 && g.farBeacon < beaconU.Length, "gap " + i);
            float span = Vector2.Distance(g.nearXZ, g.farXZ);
            Assert.IsTrue(span >= 11.5f && span <= 12.5f, "required gap " + i + " width " + span);

            Vector2 uSpan = SpireLayout.GapSpanU(i);
            Assert.Less(uSpan.x, uSpan.y, "gap " + i + " near u must come before far u");
            Assert.LessOrEqual(Vector2.Distance(g.nearXZ, SpireLayout.PathPoint(uSpan.x)), 0.01f, "gap " + i + " near point");
            Assert.LessOrEqual(Vector2.Distance(g.farXZ, SpireLayout.PathPoint(uSpan.y)), 0.01f, "gap " + i + " far point");
            Assert.Less(beaconU[g.nearBeacon], uSpan.x, "gap " + i + " near beacon is before the gap");
        }

        // Some beacon sits beyond each of gaps 1-3, so every bridge leads to something to light.
        for (int i = 0; i < 3; i++)
        {
            bool follows = false;
            for (int b = 0; b < beaconU.Length; b++)
            {
                follows |= beaconU[b] > SpireLayout.GapSpanU(i).y;
            }

            Assert.IsTrue(follows, "a beacon follows gap " + i);
        }

        Assert.LessOrEqual(Vector2.Distance(gaps[3].farXZ, SpireLayout.LampXZ()), 6.01f);
    }


    [Test]
    public void FarSideOfEveryGapIsHigherThanAJumpApex()
    {
        for (int i = 0; i < 4; i++)
        {
            Vector2 span = SpireLayout.GapSpanU(i);
            float rise = SpireLayout.PathHeight(span.y) - SpireLayout.PathHeight(span.x);
            Assert.GreaterOrEqual(rise, 3.5f, "gap " + i + " far lip rise");
        }
    }
    [Test]
    public void PathWidthIsAWalkway()
    {
        Assert.GreaterOrEqual(SpireLayout.PathWidth, 3f);
        Assert.LessOrEqual(SpireLayout.PathWidth, 4f);
    }

    [Test]
    public void PathTangentIsUnitAndFollowsTheSpiral()
    {
        for (int i = 0; i <= 20; i++)
        {
            float u = i / 20f;
            Vector2 t = SpireLayout.PathTangent(u);
            Assert.AreEqual(1f, t.magnitude, 0.001f, "unit length at u=" + u);
            Vector2 ahead = SpireLayout.PathPoint(Mathf.Min(1f, u + 0.0003f)) - SpireLayout.PathPoint(Mathf.Max(0f, u - 0.0003f));
            Assert.Greater(Vector2.Dot(t, ahead.normalized), 0.995f, "tangent points forward at u=" + u);
        }
    }

    [Test]
    public void NearestUFindsThePathParameter()
    {
        for (int i = 0; i <= 40; i++)
        {
            float u = i / 40f;
            Assert.AreEqual(u, SpireLayout.NearestU(SpireLayout.PathPoint(u)), 0.001f, "on the path at u=" + u);
        }

        // One metre to the side of the path still maps to (about) the same u.
        float mid = 0.3f;
        Vector2 side = SpireLayout.PathPoint(mid) + new Vector2(-SpireLayout.PathTangent(mid).y, SpireLayout.PathTangent(mid).x);
        Assert.AreEqual(mid, SpireLayout.NearestU(side), 0.01f);
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
