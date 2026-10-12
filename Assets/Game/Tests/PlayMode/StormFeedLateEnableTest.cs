using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// Island 5 places Wind and Lightning inactive and RingHazards enables them later; the HUD storm feed must notice (the storm chip and the steal pulse).
// Game types are reached by reflection (this assembly can't reference Assembly-CSharp).
public class StormFeedLateEnableTest
{
    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static string Describe(string typeName)
    {
        object instance = GameType(typeName).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        UnityEngine.Object o = instance as UnityEngine.Object;
        return o == null ? "null" : o.name + " active=" + ((Component)o).gameObject.activeInHierarchy + " scene=" + ((Component)o).gameObject.scene.name;
    }

    [UnityTest]
    public IEnumerator StealPulseAppearsAfterAWindEnabledLate()
    {
        // An earlier test's scene can still hold a live Wind or Lightning; park them so the feed starts from "no storm".
        foreach (string name in new[] { "Wind", "Lightning" })
        {
            foreach (UnityEngine.Object other in UnityEngine.Object.FindObjectsByType(GameType(name), FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                ((Behaviour)other).enabled = false;
            }
        }

        GameObject host = new GameObject("FeedHost");
        GameObject storm = new GameObject("LateWind");
        storm.SetActive(false);
        storm.AddComponent(GameType("Wind"));
        try
        {
            object feed = Activator.CreateInstance(GameType("StormFeed"));
            MethodInfo update = feed.GetType().GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo present = feed.GetType().GetProperty("Present", BindingFlags.Public | BindingFlags.Instance);
            Type pulse = GameType("StealPulse");

            for (int i = 0; i < 3; i++)
            {
                update.Invoke(feed, new object[] { null, host });
                yield return null;
            }

            Assert.IsFalse((bool)present.GetValue(feed), "no storm yet; Wind.Instance=" + Describe("Wind") + " Lightning.Instance=" + Describe("Lightning"));
            Assert.IsNull(host.GetComponent(pulse), "no pulse before the storm exists");

            storm.SetActive(true);
            yield return null;
            update.Invoke(feed, new object[] { null, host });
            Assert.IsTrue((bool)present.GetValue(feed), "the feed notices a storm enabled after the first frames");
            Assert.IsNotNull(host.GetComponent(pulse), "the steal pulse is attached");
        }
        finally
        {
            UnityEngine.Object.Destroy(storm);
            UnityEngine.Object.Destroy(host);
        }
    }
}
}
