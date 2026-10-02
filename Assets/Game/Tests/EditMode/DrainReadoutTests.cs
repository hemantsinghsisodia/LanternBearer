using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class DrainReadoutTests
{
    [Test]
    public void NoMothsDrainingHasNoSuffix()
    {
        Assert.AreEqual("Drain x1.00   Moths 0/3", DrainReadout.Format(1f, 0, 0, 3));
    }

    [Test]
    public void OneMothUsesSingular()
    {
        Assert.AreEqual("Drain x1.50   Moths 1/3  • 1 moth on you", DrainReadout.Format(1.5f, 1, 1, 3));
    }

    [Test]
    public void SeveralMothsUsePlural()
    {
        Assert.AreEqual("Drain x2.25   Moths 2/4  • 2 moths on you", DrainReadout.Format(2.25f, 2, 2, 4));
    }

    [Test]
    public void DrainUsesTwoDecimals()
    {
        Assert.AreEqual("Drain x1.13   Moths 0/0", DrainReadout.Format(1.125f, 0, 0, 0));
        Assert.AreEqual("Drain x0.50   Moths 0/1", DrainReadout.Format(0.5f, 0, 0, 1));
    }

    [Test]
    public void NegativeDrainingCountIsTreatedAsNone()
    {
        Assert.AreEqual("Drain x1.00   Moths 0/0", DrainReadout.Format(1f, -1, 0, 0));
    }
}
}
