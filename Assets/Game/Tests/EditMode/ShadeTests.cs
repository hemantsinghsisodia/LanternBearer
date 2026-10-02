using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class ShadeTests
{
    [Test]
    public void ThresholdsMapToStates()
    {
        Assert.AreEqual(ShadeState.Chase, ShadeLogic.StateFor(0.149f, 0f));
        Assert.AreEqual(ShadeState.Creep, ShadeLogic.StateFor(0.15f, 0f));
        Assert.AreEqual(ShadeState.Creep, ShadeLogic.StateFor(0.499f, 0f));
        Assert.AreEqual(ShadeState.Freeze, ShadeLogic.StateFor(0.5f, 0f));
    }

    [Test]
    public void RetreatAfterFrozen()
    {
        Assert.AreEqual(ShadeState.Freeze, ShadeLogic.StateFor(0.8f, 1.49f));
        Assert.AreEqual(ShadeState.Retreat, ShadeLogic.StateFor(0.8f, 1.5f));
    }

    [Test]
    public void SpeedsMatchSpec()
    {
        Assert.AreEqual(4.5f, ShadeLogic.SpeedFor(ShadeState.Chase), 0.0001f);
        Assert.AreEqual(0.8f, ShadeLogic.SpeedFor(ShadeState.Creep), 0.0001f);
        Assert.AreEqual(3f, ShadeLogic.SpeedFor(ShadeState.Retreat), 0.0001f);
        Assert.AreEqual(0f, ShadeLogic.SpeedFor(ShadeState.Freeze), 0.0001f);
        Assert.AreEqual(0f, ShadeLogic.SpeedFor(ShadeState.Reforming), 0.0001f);
        Assert.AreEqual(0f, ShadeLogic.SpeedFor(ShadeState.Stunned), 0.0001f);
    }

    [Test]
    public void StealNeverExceedsFuel()
    {
        Assert.AreEqual(7f, ShadeLogic.StealAmount(7f, 15f), 0.0001f);
        Assert.AreEqual(0f, ShadeLogic.StealAmount(0f, 15f), 0.0001f);
        Assert.AreEqual(10f, ShadeLogic.StealAmount(50f, 10f), 0.0001f);
        Assert.AreEqual(0f, ShadeLogic.StealAmount(-3f, 15f), 0.0001f);
        Assert.AreEqual(0f, ShadeLogic.StealAmount(5f, -4f), 0.0001f);
    }

    [Test]
    public void NoStealDuringRescueGrace()
    {
        Assert.IsFalse(ShadeLogic.CanSteal(ShadeState.Chase, 0.5f, 1.99f));
        Assert.IsTrue(ShadeLogic.CanSteal(ShadeState.Chase, 0.5f, 2f));
        Assert.IsFalse(ShadeLogic.CanSteal(ShadeState.Chase, 1.01f, 5f));
        Assert.IsTrue(ShadeLogic.CanSteal(ShadeState.Creep, 1f, 5f));
    }

    [Test]
    public void NoStealWhenFrozenOrStunned()
    {
        Assert.IsFalse(ShadeLogic.CanSteal(ShadeState.Freeze, 0.5f, 10f));
        Assert.IsFalse(ShadeLogic.CanSteal(ShadeState.Stunned, 0.5f, 10f));
        Assert.IsFalse(ShadeLogic.CanSteal(ShadeState.Retreat, 0.5f, 10f));
        Assert.IsFalse(ShadeLogic.CanSteal(ShadeState.Reforming, 0.5f, 10f));
    }
}
}
