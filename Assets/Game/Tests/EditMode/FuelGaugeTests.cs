using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class FuelGaugeTests
{
    private const float SmoothTime = 0.3f;
    private const float Snap = 0.0005f;
    private const float Frame = 1f / 60f;

    [Test]
    public void SlowDrainTracksTarget()
    {
        float target = 1f;
        float displayed = 1f;
        float velocity = 0f;
        float drainPerSecond = 1f / 180f;
        float time = 0f;
        for (int i = 0; i < 60 * 180 + 120; i++)
        {
            target = UnityEngine.Mathf.Max(0f, target - drainPerSecond * Frame);
            displayed = FuelGauge.Step(displayed, target, ref velocity, SmoothTime, Frame, Snap);
            time += Frame;
            if (time > 0.5f)
            {
                Assert.LessOrEqual(UnityEngine.Mathf.Abs(displayed - target), 0.01f, "frame " + i);
            }
        }

        Assert.AreEqual(0f, displayed, 1e-6f);
    }

    [Test]
    public void BigJumpEasesAndSettlesExactly()
    {
        float displayed = 0.5f;
        float velocity = 0f;
        float target = 0.7f;
        displayed = FuelGauge.Step(displayed, target, ref velocity, SmoothTime, Frame, Snap);
        Assert.Greater(displayed, 0.5f);
        Assert.Less(displayed, 0.7f);
        for (int i = 0; i < 600; i++)
        {
            displayed = FuelGauge.Step(displayed, target, ref velocity, SmoothTime, Frame, Snap);
        }

        Assert.AreEqual(target, displayed);
    }

    [Test]
    public void ZeroDeltaDoesNotMove()
    {
        float velocity = 0f;
        float displayed = FuelGauge.Step(0.5f, 0.9f, ref velocity, SmoothTime, 0f, Snap);
        Assert.AreEqual(0.5f, displayed);
    }
}
}
