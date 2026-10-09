using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class RingLogicTests
{
    [Test]
    public void RingBandsClassify()
    {
        Assert.AreEqual(Ring.Low, RingLogic.Classify(0f));
        Assert.AreEqual(Ring.Low, RingLogic.Classify(9.99f));
        Assert.AreEqual(Ring.Mid, RingLogic.Classify(10f));
        Assert.AreEqual(Ring.Mid, RingLogic.Classify(24.9f));
        Assert.AreEqual(Ring.High, RingLogic.Classify(25f));
        Assert.AreEqual(Ring.Summit, RingLogic.Classify(38f));
    }

    [Test]
    public void RatchetNeverLowers()
    {
        Assert.AreEqual(Ring.High, RingLogic.Ratchet(Ring.High, Ring.Low));
        Assert.AreEqual(Ring.Summit, RingLogic.Ratchet(Ring.Mid, Ring.Summit));
    }

    [Test]
    public void TensionFloors()
    {
        Assert.AreEqual(0f, RingLogic.TensionFloor(Ring.Low));
        Assert.AreEqual(0.3f, RingLogic.TensionFloor(Ring.Mid));
        Assert.AreEqual(0.6f, RingLogic.TensionFloor(Ring.High));
        Assert.AreEqual(0.6f, RingLogic.TensionFloor(Ring.Summit));
    }

    [Test]
    public void ThinAirOnlyHighUp()
    {
        Assert.IsFalse(RingLogic.ThinAirActive(Ring.Low));
        Assert.IsFalse(RingLogic.ThinAirActive(Ring.Mid));
        Assert.IsTrue(RingLogic.ThinAirActive(Ring.High));
        Assert.IsTrue(RingLogic.ThinAirActive(Ring.Summit));
    }
}
}
