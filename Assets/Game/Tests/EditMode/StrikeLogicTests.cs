using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class StrikeLogicTests
{
    [Test]
    public void IntervalStaysInSixToTenSeconds()
    {
        System.Random random = new System.Random(5506);
        for (int i = 0; i < 500; i++)
        {
            float interval = StrikeLogic.Interval(random);
            Assert.GreaterOrEqual(interval, 6f);
            Assert.LessOrEqual(interval, 10f);
        }
    }

    [Test]
    public void TargetIsTwoToFourMetresFromThePredictedPosition()
    {
        System.Random random = new System.Random(1);
        Vector3 keeper = new Vector3(10f, 30f, -5f);
        Vector3 velocity = new Vector3(3f, 0f, 4f);
        Vector3 predicted = keeper + velocity * StrikeLogic.LeadSeconds;
        for (int i = 0; i < 200; i++)
        {
            Vector3 target = StrikeLogic.Target(keeper, velocity, random);
            float offset = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(predicted.x, predicted.z));
            Assert.GreaterOrEqual(offset, 2f - 1e-3f);
            Assert.LessOrEqual(offset, 4f + 1e-3f);
        }
    }

    [Test]
    public void HitsWithinTwoAndAHalfMetres()
    {
        Vector3 strike = new Vector3(0f, 20f, 0f);
        Assert.IsTrue(StrikeLogic.Hits(new Vector3(2.4f, 20f, 0f), strike));
        Assert.IsFalse(StrikeLogic.Hits(new Vector3(2.6f, 20f, 0f), strike));
        Assert.IsFalse(StrikeLogic.Hits(new Vector3(0f, 26f, 0f), strike), "a keeper far above is not under the strike");
    }

    [Test]
    public void KnockGoesAwayFromTheStrike()
    {
        Vector3 away = StrikeLogic.KnockDirection(new Vector3(3f, 5f, 0f), Vector3.zero, Vector3.forward);
        Assert.AreEqual(1f, away.x, 1e-4f);
        Assert.AreEqual(0f, away.y, 1e-4f);
        Vector3 fallback = StrikeLogic.KnockDirection(Vector3.zero, Vector3.zero, Vector3.right);
        Assert.AreEqual(1f, fallback.x, 1e-4f);
    }

    [Test]
    public void TuningMatchesTheSpec()
    {
        Assert.AreEqual(1f, StrikeLogic.WarnSeconds);
        Assert.AreEqual(0.15f, StrikeLogic.FuelFraction);
        Assert.AreEqual(3f, StrikeLogic.KnockDistance);
        Assert.AreEqual(2.5f, StrikeLogic.HitRadius);
    }
}
}
