using System;
using System.Reflection;
using NUnit.Framework;

namespace LanternKeeper.Tests
{
// Island 5 now has a rising storm surge and a lightning hunter that would catch or strike a test that lingers. Fixtures that are not about
// them switch both off after loading Island 5 (StormSurge.DisabledForTests and LightningHunter.DisabledForTests; each OnDestroy resets its
// switch when the scene unloads).
public static class StormSurgeSwitch
{
    public static void DisableFor(string scene)
    {
        if (scene == "Island5")
        {
            Set(true);
            SetHunter(true);
        }
    }

    public static void Set(bool disabled)
    {
        SetField("StormSurge", disabled);
    }

    public static void SetHunter(bool disabled)
    {
        SetField("LightningHunter", disabled);
    }

    static void SetField(string typeName, bool disabled)
    {
        Type type = Type.GetType("LanternKeeper." + typeName + ", Assembly-CSharp");
        Assert.IsNotNull(type, typeName + " type");
        FieldInfo field = type.GetField("DisabledForTests", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, typeName + ".DisabledForTests");
        field.SetValue(null, disabled);
    }
}
}
