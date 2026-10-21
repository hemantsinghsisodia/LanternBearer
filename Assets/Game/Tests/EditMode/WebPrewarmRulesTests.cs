using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class WebPrewarmRulesTests
{
    [Test]
    public void WarmsGameAndPipelineShaders()
    {
        Assert.IsTrue(WebPrewarmRules.ShouldWarm("Universal Render Pipeline/Lit"));
        Assert.IsTrue(WebPrewarmRules.ShouldWarm("LanternKeeper/Foliage"));
    }

    [Test]
    public void SkipsHiddenAndTerrainShaders()
    {
        Assert.IsFalse(WebPrewarmRules.ShouldWarm("Hidden/Universal Render Pipeline/Blit"));
        Assert.IsFalse(WebPrewarmRules.ShouldWarm("Universal Render Pipeline/Terrain/Lit"));
        Assert.IsFalse(WebPrewarmRules.ShouldWarm(""));
        Assert.IsFalse(WebPrewarmRules.ShouldWarm(null));
    }
}
}
