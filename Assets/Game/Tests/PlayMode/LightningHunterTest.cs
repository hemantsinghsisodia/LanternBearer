using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// Island 5's hunting lightning and scarce fuel, live: the warning ring comes first, a hit costs 15% fuel and knocks the keeper back,
// bridges and lit beacon rings are immune, Reduce flashing caps the flash, only Island 5 refunds fuel on a beacon, and half the swarms are off.
// Game types are reached by reflection (this assembly can't reference Assembly-CSharp); the Logic types are used directly.
// Every frame advances the game by the same step (captureDeltaTime), so timings do not depend on how slowly the editor renders.
public class LightningHunterTest
{
    const float Frame = 1f / 60f;
    static readonly string[] IntKeys = { "LanternKeeperIntro_island1", "LanternKeeperIntro_island4", "LanternKeeperIntro_island5", "LanternKeeperWon_island5" };

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    float previousCaptureDelta;
    float previousMaxDelta;
    float previousScale;
    bool previousReduce;
    bool yawWasExternal;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static UnityEngine.Object Find(string typeName)
    {
        return UnityEngine.Object.FindAnyObjectByType(GameType(typeName));
    }

    static object Get(object target, string member)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
        {
            return property.GetValue(target);
        }

        FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, type.Name + "." + member + " not found");
        return field.GetValue(target);
    }

    static object Call(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, target.GetType().Name + "." + method + " not found");
        return info.Invoke(target, args);
    }

    static void SetStatic(string typeName, string field, object value)
    {
        FieldInfo info = GameType(typeName).GetField(field, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + field + " not found");
        info.SetValue(null, value);
    }

    static object StaticProperty(string typeName, string property)
    {
        PropertyInfo info = GameType(typeName).GetProperty(property, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + property + " not found");
        return info.GetValue(null);
    }

    [SetUp]
    public void SetUp()
    {
        LitKeyGuard.Save();
        savedInts.Clear();
        for (int i = 0; i < IntKeys.Length; i++)
        {
            savedInts[IntKeys[i]] = PlayerPrefs.HasKey(IntKeys[i]) ? PlayerPrefs.GetInt(IntKeys[i]) : -1;
            // A first-visit intro card pauses the game.
            if (IntKeys[i].StartsWith("LanternKeeperIntro"))
            {
                PlayerPrefs.SetInt(IntKeys[i], 1);
            }
        }

        previousReduce = UserSettings.ReduceFlashing;
        UserSettings.ReduceFlashing = false;
        previousCaptureDelta = Time.captureDeltaTime;
        previousMaxDelta = Time.maximumDeltaTime;
        previousScale = Time.timeScale;
        yawWasExternal = (bool)GameType("CameraFollow").GetField("UseExternalYaw", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Time.captureDeltaTime = Frame;
        Time.maximumDeltaTime = 0.05f;
    }

    [TearDown]
    public void TearDown()
    {
        LitKeyGuard.Restore();
        foreach (KeyValuePair<string, int> pair in savedInts)
        {
            if (pair.Value < 0)
            {
                PlayerPrefs.DeleteKey(pair.Key);
            }
            else
            {
                PlayerPrefs.SetInt(pair.Key, pair.Value);
            }
        }

        UserSettings.ReduceFlashing = previousReduce;
        PlayerPrefs.Save();
        SetStatic("PlayerController", "ExternalMove", Vector2.zero);
        SetStatic("CameraFollow", "UseExternalYaw", yawWasExternal);
        Time.captureDeltaTime = previousCaptureDelta;
        Time.maximumDeltaTime = previousMaxDelta;
        Time.timeScale = previousScale;
    }

    static IEnumerator Load(string scene)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    static Component Player()
    {
        return (Component)Find("PlayerController");
    }

    static Component Lantern()
    {
        return (Component)Find("Lantern");
    }

    static void Teleport(Vector3 position)
    {
        Component player = Player();
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        controller.enabled = true;
    }

    static float Sea()
    {
        return GameObject.Find("Water").transform.position.y;
    }

    static bool InGap(float u)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector2 span = SpireLayout.GapSpanU(i);
            if (u > span.x - 0.02f && u < span.y + 0.02f)
            {
                return true;
            }
        }

        return false;
    }

    // A point on solid walkway ground, `height` metres above the sea (a little above the ground so the keeper drops onto it).
    static Vector3 Walkway(float height)
    {
        Terrain terrain = Terrain.activeTerrain;
        for (int step = 0; step <= 1000; step++)
        {
            float u = step / 1000f;
            if (SpireLayout.PathHeight(u) < height || InGap(u))
            {
                continue;
            }

            Vector2 xz = SpireLayout.PathPoint(u);
            Vector3 p = new Vector3(xz.x, 0f, xz.y);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.4f;
            return p;
        }

        Assert.Fail("no walkway at " + height);
        return Vector3.zero;
    }

    // Island 5 with the surge off, the High ring's storm switched on (as RingHazards does), the keeper on open walkway 30 m up and the
    // lantern's drain frozen so fuel changes come only from the strike. Returns the hunter.
    static IEnumerator Stage(Action<Component> done)
    {
        yield return Load("Island5");
        StormSurgeSwitch.Set(true);
        Transform rings = GameObject.Find("Rings").transform;
        Transform hunterObject = rings.Find("LightningHunter");
        Assert.IsNotNull(hunterObject, "Island 5 has a LightningHunter under Rings");
        Assert.IsFalse(hunterObject.gameObject.activeSelf, "the hunter waits for the High ring");
        rings.Find("Lightning").gameObject.SetActive(true);
        hunterObject.gameObject.SetActive(true);
        Teleport(Walkway(30f));
        Call(Lantern(), "AddFuel", 100f);
        Lantern().GetType().GetProperty("DrainFrozen").SetValue(Lantern(), true);
        yield return Wait(0.6f);
        Component hunter = hunterObject.GetComponent(GameType("LightningHunter"));
        Assert.IsNotNull(hunter);
        // Keep the timer from striking on its own while the test sets things up.
        Call(hunter, "SetCountdownForTests", 1000f);
        done(hunter);
    }

    static float Fuel()
    {
        return (float)Get(Lantern(), "Fuel");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator StrikesWarnFirst()
    {
        Component hunter = null;
        yield return Stage(h => hunter = h);
        Assert.IsFalse((bool)Get(hunter, "WarningActive"));
        Call(hunter, "SetCountdownForTests", 0.05f);
        float waited = 0f;
        while (!(bool)Get(hunter, "WarningActive") && waited < 2f)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        Assert.IsTrue((bool)Get(hunter, "WarningActive"), "a strike picks a target");
        Assert.IsTrue((bool)Get(hunter, "RingVisible"), "the warning ring shows");
        Assert.AreEqual(0, (int)Get(hunter, "Strikes"), "nothing has struck yet");
        Vector3 point = (Vector3)Get(hunter, "StrikePoint");
        Vector3 keeper = Player().transform.position;
        float away = new Vector2(point.x - keeper.x, point.z - keeper.z).magnitude;
        Assert.GreaterOrEqual(away, StrikeLogic.OffsetMin - 1.2f, "the target is a few metres from the keeper");
        Assert.LessOrEqual(away, StrikeLogic.OffsetMax + 1.2f);
        Material material = (Material)Get(hunter, "RingMaterial");
        Color colour = material.GetColor("_Color");
        Assert.Greater(colour.b, colour.r, "the ring is cold blue, never amber");
        Assert.Greater(colour.b, 0.9f);

        yield return Wait(StrikeLogic.WarnSeconds * 0.5f);
        Assert.IsTrue((bool)Get(hunter, "WarningActive"), "still warning half way");
        Assert.AreEqual(0, (int)Get(hunter, "Strikes"));
        Assert.AreEqual(0.5f, (float)Get(hunter, "WarnProgress"), 0.15f);

        yield return Wait(StrikeLogic.WarnSeconds * 0.5f + 0.2f);
        Assert.AreEqual(1, (int)Get(hunter, "Strikes"), "the strike lands after the warning");
        Assert.IsFalse((bool)Get(hunter, "WarningActive"));
        Assert.IsFalse((bool)Get(hunter, "RingVisible"), "the ring is gone after the strike");
        float next = (float)Get(hunter, "SecondsToNextStrike");
        Assert.GreaterOrEqual(next, StrikeLogic.IntervalMin - 0.5f);
        Assert.LessOrEqual(next, StrikeLogic.IntervalMax);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator HitCostsFifteenPercent()
    {
        Component hunter = null;
        yield return Stage(h => hunter = h);
        float before = Fuel();
        float max = (float)Get(Lantern(), "MaxFuel");
        Vector3 start = Player().transform.position;
        Call(hunter, "BeginWarning", start + new Vector3(0.5f, 0f, 0f));
        yield return Wait(StrikeLogic.WarnSeconds + 0.5f);
        Assert.AreEqual(1, (int)Get(hunter, "Strikes"));
        Assert.AreEqual(1, (int)Get(hunter, "Hits"), "a strike on the keeper hits");
        Assert.AreEqual(before - max * 0.15f, Fuel(), 0.05f, "a hit costs 15% of max fuel");
        Vector3 after = Player().transform.position;
        Assert.Greater(new Vector2(after.x - start.x, after.z - start.z).magnitude, 1.5f, "the keeper is knocked back");

        // A near miss: the keeper moved 3 m away from the last hit; a strike 6 m off does nothing.
        float fuel = Fuel();
        Call(hunter, "BeginWarning", Player().transform.position + new Vector3(0f, 0f, 6f));
        yield return Wait(StrikeLogic.WarnSeconds + 0.5f);
        Assert.AreEqual(2, (int)Get(hunter, "Strikes"));
        Assert.AreEqual(1, (int)Get(hunter, "Hits"), "a strike 6 m away misses");
        Assert.AreEqual(fuel, Fuel(), 0.01f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator NoStrikeInsideShelter()
    {
        Component hunter = null;
        yield return Stage(h => hunter = h);
        // Out in the open the hunter does target the keeper.
        Assert.IsTrue((bool)Call(hunter, "TryTarget"), "an open keeper is targeted");
        Call(hunter, "SetCountdownForTests", 1000f);
        yield return Wait(StrikeLogic.WarnSeconds + 0.3f);
        Assert.AreEqual(1, (int)Get(hunter, "Strikes"));

        // Inside a lit beacon's safe ring: no target, and a strike that lands anyway does nothing.
        Transform beacons = GameObject.Find("Beacons").transform;
        Component beacon = beacons.GetChild(0).GetComponent(GameType("Beacon"));
        Call(Lantern(), "AddFuel", 100f);
        Assert.IsTrue((bool)Call(beacon, "TryLight"));
        Teleport(beacon.transform.position + new Vector3(1.5f, 0.5f, 0f));
        yield return Wait(0.5f);
        Call(Lantern(), "AddFuel", 100f);
        int hitsBefore = (int)Get(hunter, "Hits");
        Assert.IsFalse((bool)Call(hunter, "TryTarget"), "a keeper inside a lit ring is never targeted");
        Assert.IsFalse((bool)Get(hunter, "WarningActive"));
        Call(hunter, "SetCountdownForTests", 0.05f);
        yield return Wait(2f);
        Assert.IsFalse((bool)Get(hunter, "WarningActive"), "no warning starts for a sheltered keeper");
        Assert.AreEqual(1, (int)Get(hunter, "Strikes"), "no new strike");
        float fuel = Fuel();
        Call(hunter, "BeginWarning", Player().transform.position);
        yield return Wait(StrikeLogic.WarnSeconds + 0.3f);
        Assert.AreEqual(hitsBefore, (int)Get(hunter, "Hits"), "a strike on a lit ring does no harm");
        Assert.AreEqual(fuel, Fuel(), 0.01f);

        // Even while the surge is catching the keeper (safe light suppressed), the ring still shelters from strikes.
        StormSurgeSwitch.Set(false);
        Component surge = (Component)Find("StormSurge");
        Call(surge, "HoldHeightForTests", Player().transform.position.y - Sea() + 5f);
        yield return Wait(0.3f);
        Assert.IsTrue((bool)Get(surge, "IsCaught"), "the keeper is caught in the surge");
        Assert.IsFalse((bool)Call(hunter, "TryTarget"), "lit rings are immune even while caught");
        Call(surge, "HoldHeightForTests", float.NaN);
        StormSurgeSwitch.Set(true);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator BridgeIsSafeFromStrikes()
    {
        Component hunter = null;
        yield return Stage(h => hunter = h);
        GameObject bridgeObject = GameObject.Find("LightBridge_Gap0");
        Assert.IsNotNull(bridgeObject);
        Component bridge = bridgeObject.GetComponent(GameType("LightBridge"));
        Component near = (Component)Get(bridge, "NearBeacon");
        Call(Lantern(), "AddFuel", 100f);
        Call(near, "TryLight");
        float waited = 0f;
        while (!(bool)Get(bridge, "Walkable") && waited < 20f)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        Assert.IsTrue((bool)Get(bridge, "Walkable"), "the bridge is built");
        Vector3[] points = (Vector3[])Get(bridge, "Points");
        Vector3 middle = points[points.Length / 2];
        Assert.IsTrue((bool)GameType("LightBridge").GetMethod("AnySafe").Invoke(null, new object[] { middle }), "the span is safe light");
        Teleport(middle + Vector3.up * 0.6f);
        yield return Wait(0.5f);
        Assert.IsFalse((bool)Call(hunter, "TryTarget"), "a keeper on a bridge is never targeted");
        Assert.IsFalse((bool)Get(hunter, "WarningActive"));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator StrikeRespectsReduceFlashing()
    {
        Component hunter = null;
        yield return Stage(h => hunter = h);
        Assert.AreEqual(1f, (float)Get(hunter, "PulseDepth"), 1e-4f);
        float normalPeak = 0f;
        yield return StrikeAndSample(hunter, v => normalPeak = v);
        Assert.Greater(normalPeak, 0.9f, "a strike flashes the sky at full strength by default");

        UserSettings.ReduceFlashing = true;
        Assert.Less((float)Get(hunter, "PulseDepth"), 0.5f, "the ring pulses softly");
        float reducedPeak = 0f;
        yield return StrikeAndSample(hunter, v => reducedPeak = v);
        float cap = SettingsMath.FlashCap(true);
        Assert.LessOrEqual(reducedPeak, cap + 0.01f, "the flash is capped by Reduce flashing");
        Assert.Greater(reducedPeak, 0.05f, "but the strike is still seen");
    }

    // Strikes beside the keeper (a miss) and returns the highest world flash seen (Lightning.CurrentFlash) and strike light, as a 0..1 flash value.
    static IEnumerator StrikeAndSample(Component hunter, Action<float> peak)
    {
        Call(hunter, "BeginWarning", Player().transform.position + new Vector3(0f, 0f, 8f));
        float maxFlash = 0f;
        float elapsed = 0f;
        while (elapsed < StrikeLogic.WarnSeconds + 0.8f)
        {
            yield return null;
            elapsed += Time.deltaTime;
            maxFlash = Mathf.Max(maxFlash, (float)StaticProperty("Lightning", "CurrentFlash"));
            float light = (float)Get(hunter, "StrikeLightIntensity");
            float lightCap = (float)GameType("LightningHunter").GetProperty("LightPeak", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert.LessOrEqual(light, lightCap + 0.01f);
        }

        peak(maxFlash);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RefundOnlyOnIslandFive()
    {
        yield return Load("Island1");
        Component first = GameObject.Find("Beacons").transform.GetChild(0).GetComponent(GameType("Beacon"));
        Assert.AreEqual(0f, (float)Get(first, "Refund01"), "Island 1 refunds nothing");
        Lantern().GetType().GetProperty("DrainFrozen").SetValue(Lantern(), true);
        Assert.IsTrue((bool)Call(Lantern(), "TrySpend", 40f));
        float before = Fuel();
        Assert.IsTrue((bool)Call(first, "TryLight"));
        Assert.AreEqual(before - (float)Get(first, "FuelCost"), Fuel(), 0.01f, "Island 1: the beacon costs its full price");

        yield return Load("Island4");
        Component four = GameObject.Find("Beacons").transform.GetChild(0).GetComponent(GameType("Beacon"));
        Assert.AreEqual(0f, (float)Get(four, "Refund01"), "Island 4 refunds nothing");

        yield return Load("Island5");
        StormSurgeSwitch.DisableFor("Island5");
        Transform beacons = GameObject.Find("Beacons").transform;
        for (int i = 0; i < beacons.childCount; i++)
        {
            Assert.AreEqual(0.2f, (float)Get(beacons.GetChild(i).GetComponent(GameType("Beacon")), "Refund01"), 1e-4f, "Island 5 beacon " + i);
        }

        Component beacon = beacons.GetChild(0).GetComponent(GameType("Beacon"));
        Lantern().GetType().GetProperty("DrainFrozen").SetValue(Lantern(), true);
        Assert.IsTrue((bool)Call(Lantern(), "TrySpend", 40f));
        before = Fuel();
        float max = (float)Get(Lantern(), "MaxFuel");
        Assert.IsTrue((bool)Call(beacon, "TryLight"));
        Assert.AreEqual(before - (float)Get(beacon, "FuelCost") + max * 0.2f, Fuel(), 0.01f, "Island 5: lighting a beacon refunds 20% of max fuel");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator HalfTheSwarms()
    {
        yield return Load("Island5");
        UnityEngine.Object[] all = UnityEngine.Object.FindObjectsByType(GameType("FireflySwarm"), FindObjectsInactive.Include);
        UnityEngine.Object[] active = UnityEngine.Object.FindObjectsByType(GameType("FireflySwarm"), FindObjectsInactive.Exclude);
        Assert.AreEqual(26, all.Length, "Island 5 keeps 26 firefly pickups in the scene");
        Assert.AreEqual(13, active.Length, "half of them are switched off");
        Transform fireflies = GameObject.Find("Fireflies").transform;
        for (int i = 0; i < fireflies.childCount; i++)
        {
            Assert.AreEqual(i % 2 == 0, fireflies.GetChild(i).gameObject.activeSelf, "pickup " + i + " (every second one is off)");
        }

        yield return Load("Island4");
        UnityEngine.Object[] four = UnityEngine.Object.FindObjectsByType(GameType("FireflySwarm"), FindObjectsInactive.Include);
        UnityEngine.Object[] fourActive = UnityEngine.Object.FindObjectsByType(GameType("FireflySwarm"), FindObjectsInactive.Exclude);
        Assert.AreEqual(four.Length, fourActive.Length, "Island 4 keeps all its swarms");
    }
}
}
