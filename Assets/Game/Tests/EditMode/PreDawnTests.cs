using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class PreDawnTests
{
    [Test]
    public void ReachValues()
    {
        Assert.AreEqual(0.25f, PreDawn.ReachFor("island1"), 1e-6f);
        Assert.AreEqual(0.45f, PreDawn.ReachFor("island2"), 1e-6f);
        Assert.AreEqual(0.65f, PreDawn.ReachFor("island3"), 1e-6f);
        Assert.AreEqual(0.85f, PreDawn.ReachFor("island4"), 1e-6f);
        Assert.AreEqual(1f, PreDawn.ReachFor("island5"), 1e-6f);
        Assert.AreEqual(1f, PreDawn.ReachFor("menu"), 1e-6f);
        Assert.AreEqual(1f, PreDawn.ReachFor(null), 1e-6f);
    }

    [Test]
    public void BlendStopsAtReach()
    {
        Assert.AreEqual(0f, PreDawn.Blend(0f, 0.45f), 1e-6f);
        Assert.AreEqual(0.45f, PreDawn.Blend(1f, 0.45f), 1e-6f);
        Assert.AreEqual(0.45f, PreDawn.Blend(2f, 0.45f), 1e-6f);
        Assert.AreEqual(0.225f, PreDawn.Blend(0.5f, 0.45f), 1e-6f);
        float last = 0f;
        for (int i = 0; i <= 20; i++)
        {
            float b = PreDawn.Blend(i / 20f, 0.65f);
            Assert.GreaterOrEqual(b, last);
            Assert.LessOrEqual(b, 0.65f + 1e-6f);
            last = b;
        }

        Assert.AreEqual(1f, PreDawn.Blend(1f, 1f), 1e-6f);
    }
}
}
