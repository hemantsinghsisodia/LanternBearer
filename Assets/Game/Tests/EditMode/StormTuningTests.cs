using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class StormTuningTests
{
    [Test]
    public void GustMultiplierPerDifficulty()
    {
        Assert.AreEqual(0.7f, StormTuning.GustMultiplier(0), 0.0001f);
        Assert.AreEqual(1f, StormTuning.GustMultiplier(1), 0.0001f);
        Assert.AreEqual(1.25f, StormTuning.GustMultiplier(2), 0.0001f);
    }

    [Test]
    public void MaxShadesPerDifficulty()
    {
        Assert.AreEqual(3, StormTuning.MaxShades(0));
        Assert.AreEqual(5, StormTuning.MaxShades(1));
        Assert.AreEqual(6, StormTuning.MaxShades(2));
    }

    [Test]
    public void ShadeStealPerDifficulty()
    {
        Assert.AreEqual(10f, StormTuning.ShadeSteal(0), 0.0001f);
        Assert.AreEqual(15f, StormTuning.ShadeSteal(1), 0.0001f);
        Assert.AreEqual(20f, StormTuning.ShadeSteal(2), 0.0001f);
    }

    [Test]
    public void LightningIntervalPerDifficulty()
    {
        Assert.AreEqual(new Vector2(30f, 45f), StormTuning.LightningInterval(0));
        Assert.AreEqual(new Vector2(35f, 55f), StormTuning.LightningInterval(1));
        Assert.AreEqual(new Vector2(45f, 65f), StormTuning.LightningInterval(2));
    }

    [Test]
    public void ShadeTargetScalesWithLitBeaconsAndCaps()
    {
        Assert.AreEqual(2, StormTuning.ShadeTarget(2, 0, 5));
        Assert.AreEqual(3, StormTuning.ShadeTarget(2, 3, 5));
        Assert.AreEqual(5, StormTuning.ShadeTarget(2, 9, 5));
        Assert.AreEqual(3, StormTuning.ShadeTarget(2, 9, 3));
    }
}
}
