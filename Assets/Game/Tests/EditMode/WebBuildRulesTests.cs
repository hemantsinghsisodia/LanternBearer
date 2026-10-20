using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class WebBuildRulesTests
{
    // Regression: a WebGL player built while Windows was the active Editor platform shipped without the Forward lit
    // shader variants (terrain, trees, rocks, keeper invisible, magenta objects). Only a WebGL-active Editor may build it.
    [Test]
    public void WebGLBuildNeedsWebGLActive()
    {
        Assert.IsFalse(WebBuildRules.IsSafeToBuild(true, false));
        Assert.IsTrue(WebBuildRules.IsSafeToBuild(true, true));
    }

    [Test]
    public void OtherPlatformBuildsAreNotRestricted()
    {
        Assert.IsTrue(WebBuildRules.IsSafeToBuild(false, false));
        Assert.IsTrue(WebBuildRules.IsSafeToBuild(false, true));
    }

    [Test]
    public void UnsafeMessageSaysWhatToDo()
    {
        StringAssert.Contains("WebGL", WebBuildRules.UnsafeMessage);
        StringAssert.Contains("Switch", WebBuildRules.UnsafeMessage);
    }
}
}
