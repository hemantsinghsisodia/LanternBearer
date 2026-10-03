using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class KeeperLeanTests
{
    [Test]
    public void GustUnshelteredLeansByStrength()
    {
        Assert.AreEqual(0.8f, KeeperLean.TargetWeight(WindPhase.Gust, 0.8f, false, true), 1e-5f);
    }

    [Test]
    public void ShelterCancelsLean()
    {
        Assert.AreEqual(0f, KeeperLean.TargetWeight(WindPhase.Gust, 0.8f, true, true), 1e-5f);
    }

    [Test]
    public void WarningDoesNotLean()
    {
        Assert.AreEqual(0f, KeeperLean.TargetWeight(WindPhase.Warning, 1f, false, true), 1e-5f);
    }

    [Test]
    public void NoWindMeansNoLean()
    {
        Assert.AreEqual(0f, KeeperLean.TargetWeight(WindPhase.Gust, 1f, false, false), 1e-5f);
    }

    [Test]
    public void StepEasesLinearly()
    {
        Assert.AreEqual(0.5f, KeeperLean.Step(0f, 1f, 0.15f), 1e-4f);
    }

    [Test]
    public void StepHoldsWhenPaused()
    {
        Assert.AreEqual(0.4f, KeeperLean.Step(0.4f, 1f, 0f), 1e-5f);
    }

    [Test]
    public void StepDoesNotOvershoot()
    {
        Assert.AreEqual(1f, KeeperLean.Step(0.9f, 1f, 1f), 1e-5f);
    }
}
}
