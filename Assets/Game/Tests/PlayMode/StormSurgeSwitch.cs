using System;
using System.Reflection;
using NUnit.Framework;

namespace LanternKeeper.Tests
{
// Island 5 now has a rising storm surge that would catch a test that lingers. Fixtures that are not about the surge switch it off after loading
// Island 5 (StormSurge.DisabledForTests; its OnDestroy resets it when the scene unloads).
public static class StormSurgeSwitch
{
    public static void DisableFor(string scene)
    {
        if (scene == "Island5")
        {
            Set(true);
        }
    }

    public static void Set(bool disabled)
    {
        Type type = Type.GetType("LanternKeeper.StormSurge, Assembly-CSharp");
        Assert.IsNotNull(type, "StormSurge type");
        FieldInfo field = type.GetField("DisabledForTests", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "StormSurge.DisabledForTests");
        field.SetValue(null, disabled);
    }
}
}
