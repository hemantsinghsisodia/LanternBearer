using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class IslandLabelsTests
{
    [Test]
    public void ComposeJoinsNameAndTitleWithEmDash()
    {
        Assert.AreEqual("Island 4 — The Storm Cape", IslandLabels.Compose("Island 4", "The Storm Cape"));
    }

    [Test]
    public void ComposeWithoutTitleReturnsDisplayName()
    {
        Assert.AreEqual("Island 1", IslandLabels.Compose("Island 1", ""));
        Assert.AreEqual("Island 1", IslandLabels.Compose("Island 1", null));
    }

    [Test]
    public void ToHudSwapsEmDashForMiddleDot()
    {
        Assert.AreEqual("Island 4 · The Storm Cape", IslandLabels.ToHud("Island 4 — The Storm Cape"));
        Assert.AreEqual("Island 1", IslandLabels.ToHud("Island 1"));
        Assert.AreEqual("", IslandLabels.ToHud(null));
    }
}
}
