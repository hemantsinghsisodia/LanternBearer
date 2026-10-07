using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
// The themed HUD running on Island1. Game types are reached by reflection (this assembly can't reference Assembly-CSharp).
public class HudLiveTest
{
    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static UnityEngine.Object Find(string typeName)
    {
        UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(GameType(typeName));
        Assert.IsNotNull(found, typeName + " in scene");
        return found;
    }

    static IEnumerator LoadIsland1()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("Island1");
        while (!load.isDone)
        {
            yield return null;
        }

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    static object Call(UnityEngine.Object target, string method, params object[] args)
    {
        return target.GetType().GetMethod(method).Invoke(target, args);
    }

    static T Get<T>(UnityEngine.Object target, string property)
    {
        return (T)target.GetType().GetProperty(property).GetValue(target);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FuelGaugeTracksFuel()
    {
        yield return LoadIsland1();
        FuelGaugeWidget gauge = UnityEngine.Object.FindFirstObjectByType<FuelGaugeWidget>();
        Assert.IsNotNull(gauge, "the HUD carries a fuel gauge");
        UnityEngine.Object lantern = Find("Lantern");
        float before = gauge.FlameScale;
        Assert.IsTrue((bool)Call(lantern, "TrySpend", 40f));
        yield return Frames(3);
        Assert.Less(gauge.FlameScale, before - 0.1f, "flame shrinks when fuel is spent");
        Assert.IsTrue(gauge.ChangeVisible, "a spend flashes the change");
        Assert.AreEqual("−40", gauge.ChangeText);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator LightingBeaconLightsRoof()
    {
        yield return LoadIsland1();
        BeaconRoofs roofs = UnityEngine.Object.FindFirstObjectByType<BeaconRoofs>();
        Assert.IsNotNull(roofs);
        UnityEngine.Object[] beacons = UnityEngine.Object.FindObjectsByType(GameType("Beacon"), FindObjectsSortMode.None);
        Assert.AreEqual(beacons.Length, roofs.VisibleCount, "one roof per beacon");
        Assert.AreEqual(0, roofs.LitCount);
        Assert.IsTrue((bool)Call(beacons[0], "TryLight"));
        yield return Frames(3);
        Assert.AreEqual(1, roofs.LitCount, "the lit beacon's roof turns amber");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MothDrainShowsMothIcon()
    {
        yield return LoadIsland1();
        DrainIcons icons = UnityEngine.Object.FindFirstObjectByType<DrainIcons>();
        Assert.IsNotNull(icons);
        Assert.IsFalse(icons.MothVisible);
        UnityEngine.Object lantern = Find("Lantern");
        GameObject holder = new GameObject("TestMoth");
        Component moth = holder.AddComponent(GameType("Moth"));
        ((Behaviour)moth).enabled = false; // the moth's own AI would clear the modifier; this one only has to count
        Call(lantern, "SetDrainModifier", moth, 2f);
        yield return Frames(3);
        Assert.IsTrue(icons.MothVisible, "moth icon while a moth drains");
        Assert.AreEqual("×1", icons.MothText);
        UnityEngine.Object.Destroy(holder);
        yield return Frames(3);
        Assert.IsFalse(icons.MothVisible, "moth icon hides when the moth is gone");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator SafeRingShowsShield()
    {
        yield return LoadIsland1();
        DrainIcons icons = UnityEngine.Object.FindFirstObjectByType<DrainIcons>();
        Assert.IsFalse(icons.ShieldVisible);
        Component beacon = (Component)UnityEngine.Object.FindFirstObjectByType(GameType("Beacon"));
        Assert.IsTrue((bool)Call(beacon, "TryLight"));
        Component player = (Component)Find("PlayerController");
        player.transform.position = beacon.transform.position;
        Physics.SyncTransforms();
        yield return Frames(5);
        Assert.IsTrue(icons.ShieldVisible, "shield inside a lit ring");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MothDrainInsideLitRingShowsMothAndShield()
    {
        yield return LoadIsland1();
        DrainIcons icons = UnityEngine.Object.FindFirstObjectByType<DrainIcons>();
        Component beacon = (Component)UnityEngine.Object.FindFirstObjectByType(GameType("Beacon"));
        Assert.IsTrue((bool)Call(beacon, "TryLight"));
        Component player = (Component)Find("PlayerController");
        player.transform.position = beacon.transform.position;
        Physics.SyncTransforms();
        UnityEngine.Object lantern = Find("Lantern");
        GameObject holder = new GameObject("TestMoth");
        Component moth = holder.AddComponent(GameType("Moth"));
        ((Behaviour)moth).enabled = false;
        Call(lantern, "SetDrainModifier", moth, 2f);
        yield return Frames(5);
        Assert.IsTrue(icons.ShieldVisible, "shield inside the lit ring");
        Assert.IsTrue(icons.MothVisible, "moth icon while a moth drains inside the ring");
        Assert.AreEqual("×1", icons.MothText, "one draining moth");
        UnityEngine.Object.Destroy(holder);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator RetryResetsHud()
    {
        yield return LoadIsland1();
        BeaconRoofs roofs = UnityEngine.Object.FindFirstObjectByType<BeaconRoofs>();
        FuelGaugeWidget gauge = UnityEngine.Object.FindFirstObjectByType<FuelGaugeWidget>();
        UnityEngine.Object lantern = Find("Lantern");
        Component beacon = (Component)UnityEngine.Object.FindFirstObjectByType(GameType("Beacon"));
        Assert.IsTrue((bool)Call(beacon, "TryLight"));
        yield return Frames(3);
        Assert.AreEqual(1, roofs.LitCount);
        GameObject mothHolder = new GameObject("TestMoth");
        Component drainMoth = mothHolder.AddComponent(GameType("Moth"));
        ((Behaviour)drainMoth).enabled = false;
        Call(lantern, "SetDrainModifier", drainMoth, 2f);
        yield return Frames(3);
        Assert.IsTrue(UnityEngine.Object.FindFirstObjectByType<DrainIcons>().MothVisible, "a moth drain was showing before the retry");
        Call(lantern, "TrySpend", Get<float>(lantern, "Fuel"));
        yield return Frames(10);
        Assert.IsTrue(gauge.ChangeVisible, "the lose spend flashed");

        Call(Find("GameManager"), "RestartLevel");
        yield return null;
        yield return Frames(6);

        roofs = UnityEngine.Object.FindFirstObjectByType<BeaconRoofs>();
        gauge = UnityEngine.Object.FindFirstObjectByType<FuelGaugeWidget>();
        TimerLabel timer = UnityEngine.Object.FindFirstObjectByType<TimerLabel>();
        Assert.AreEqual(0, roofs.LitCount, "no lit roofs after retry");
        Assert.IsFalse(gauge.ChangeVisible, "no flash after retry");
        DrainIcons icons = UnityEngine.Object.FindFirstObjectByType<DrainIcons>();
        Assert.IsFalse(icons.MothVisible, "moth icon reset after retry");
        Assert.IsFalse(icons.ShieldVisible, "shield reset after retry");
        Assert.IsFalse(icons.EmberVisible, "ember icon reset after retry");
        Assert.IsFalse(icons.MultiplierVisible, "multiplier reset after retry");
        Assert.IsTrue(timer.Text == "00:00" || timer.Text == "00:01", "timer restarted, was " + timer.Text);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator HudLabelsReadable()
    {
        yield return LoadIsland1();
        yield return Frames(5);
        List<string> problems = new List<string>();
        Canvas canvas = ((Component)Find("HUD")).GetComponent<Canvas>();
        TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
        int checkedCount = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            if (!IsAlwaysOnHud(texts[i].transform, canvas.transform))
            {
                continue;
            }

            checkedCount++;
            // On-screen size: the font size times every scale between the canvas and the text (the compass's far markers are 0.9 x 20).
            float effective = texts[i].fontSize * texts[i].transform.lossyScale.x / canvas.transform.lossyScale.x;
            if (effective < 18f - 0.01f)
            {
                problems.Add(texts[i].name + " is " + effective + " px");
            }

            if (!HasBacking(texts[i].transform, canvas.transform))
            {
                problems.Add(texts[i].name + " has no backing");
            }
        }

        Assert.Greater(checkedCount, 4, "found the HUD texts");
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // The always-on HUD: the GameHud prefab and the compass markers (the prompt, cards and toasts are task 4's).
    static bool IsAlwaysOnHud(Transform text, Transform canvas)
    {
        for (Transform walk = text; walk != null && walk != canvas; walk = walk.parent)
        {
            if (walk.name == "GameHud" || walk.name == "EdgeMarkers")
            {
                return true;
            }
        }

        return false;
    }

    // An ancestor is an image, or has a "Backing" child image (the chips' ink backing).
    static bool HasBacking(Transform text, Transform canvas)
    {
        for (Transform walk = text.parent; walk != null && walk != canvas; walk = walk.parent)
        {
            Image image = walk.GetComponent<Image>();
            if (image != null && image.color.a > 0.3f)
            {
                return true;
            }

            Transform backing = walk.Find("Backing");
            if (backing != null && backing.GetComponent<Image>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
}
