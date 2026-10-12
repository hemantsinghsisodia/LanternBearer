using System;
using System.Reflection;
using NUnit.Framework;

namespace LanternKeeper.Tests
{
// "Build Island 5" wipes the bridges and rings, so BuildIsland5 must run the installers afterwards (bridges before rings).
public class Island5BuildChainTests
{
    [Test]
    public void BuildIsland5RunsBridgeRingAndPerfInstallers()
    {
        Type builder = Type.GetType("LanternKeeper.IslandBuilder, Assembly-CSharp-Editor");
        Assert.IsNotNull(builder, "IslandBuilder type");
        FieldInfo field = builder.GetField("Island5PostSteps", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "IslandBuilder.Island5PostSteps");
        var steps = (ValueTuple<string, Action>[])field.GetValue(null);
        Assert.AreEqual(3, steps.Length);
        Assert.AreEqual("BridgeInstaller", steps[0].Item1);
        Assert.AreEqual("RingInstaller", steps[1].Item1);
        Assert.AreEqual("Island5PerfInstaller", steps[2].Item1);
        Assert.IsNotNull(steps[0].Item2);
        Assert.IsNotNull(steps[1].Item2);
        Assert.IsNotNull(steps[2].Item2);
    }
}
}
