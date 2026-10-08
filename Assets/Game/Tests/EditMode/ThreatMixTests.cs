using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class ThreatMixTests
{
    const float Tol = 1e-4f;

    [Test]
    public void MothsDrainingIsFull()
    {
        Assert.AreEqual(1f, ThreatMix.Target(true, 99f, 1f, false));
    }

    [Test]
    public void ShadeDistanceRamp()
    {
        Assert.AreEqual(1f, ThreatMix.Target(false, 4f, 1f, false), Tol);
        Assert.AreEqual(0.5f, ThreatMix.Target(false, 8f, 1f, false), Tol);
        Assert.AreEqual(0f, ThreatMix.Target(false, 12f, 1f, false), Tol);
        Assert.AreEqual(0f, ThreatMix.Target(false, 30f, 1f, false), Tol);
    }

    [Test]
    public void LowFuelRamp()
    {
        Assert.AreEqual(0f, ThreatMix.Target(false, 99f, 0.25f, false), Tol);
        Assert.AreEqual(0.5f, ThreatMix.Target(false, 99f, 0.15f, false), Tol);
        Assert.AreEqual(1f, ThreatMix.Target(false, 99f, 0.05f, false), Tol);
        Assert.AreEqual(1f, ThreatMix.Target(false, 99f, 0f, false), Tol);
    }

    [Test]
    public void MaxOfInputs()
    {
        Assert.AreEqual(1f, ThreatMix.Target(false, 8f, 0.05f, false), Tol);
    }

    [Test]
    public void SafeRingOverridesAllInputs()
    {
        Assert.AreEqual(0f, ThreatMix.Target(true, 5f, 0.1f, true));
    }

    [Test]
    public void RiseAndFallTimes()
    {
        float value = 0f;
        int steps = 0;
        while (value < 1f && steps < 100)
        {
            value = ThreatMix.Step(value, 1f, 0.1f);
            steps++;
        }

        Assert.AreEqual(15, steps);
        steps = 0;
        while (value > 0f && steps < 100)
        {
            value = ThreatMix.Step(value, 0f, 0.1f);
            steps++;
        }

        Assert.AreEqual(40, steps);
    }

    [Test]
    public void NonPositiveOrNaNDeltaDoesNotMove()
    {
        Assert.AreEqual(0.3f, ThreatMix.Step(0.3f, 1f, 0f));
        Assert.AreEqual(0.3f, ThreatMix.Step(0.3f, 0f, -1f));
        Assert.AreEqual(0.3f, ThreatMix.Step(0.3f, 1f, float.NaN));
    }

    [Test]
    public void ClampedAndNaNSafe()
    {
        Assert.AreEqual(0f, ThreatMix.Target(false, 99f, float.NaN, false), Tol);
        Assert.AreEqual(1f, ThreatMix.Target(false, -5f, 1f, false), Tol);
    }
}
}
