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
// Island 5's rising storm surge, live: it starts on the first beacon, catches a keeper below it (drain x4, no safe light, dark fog,
// heartbeat), frees a caught keeper when the next beacon is lit, resets on Retry and stops when the great lamp is lit.
// Game types are reached by reflection (this assembly can't reference Assembly-CSharp); the Logic types are used directly.
// Every frame advances the game by the same step (captureDeltaTime), so timings do not depend on how slowly the editor renders.
public class StormSurgeTest
{
    const float Frame = 1f / 60f;
    static readonly string[] IntKeys =
    {
        "LanternKeeperIntro_island5", "LanternKeeperWon_island5"
    };
    static readonly string[] FloatKeys = { "LanternKeeperBest_island5" };

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
    float previousCaptureDelta;
    float previousMaxDelta;
    float previousScale;
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
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(info, target.GetType().Name + "." + method + " not found");
        return info.Invoke(target, args);
    }

    static object StaticProperty(string typeName, string property)
    {
        PropertyInfo info = GameType(typeName).GetProperty(property, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + property + " not found");
        return info.GetValue(null);
    }

    static void SetStatic(string typeName, string field, object value)
    {
        FieldInfo info = GameType(typeName).GetField(field, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + field + " not found");
        info.SetValue(null, value);
    }

    static Component Surge()
    {
        return (Component)Find("StormSurge");
    }

    static float Height()
    {
        return (float)Get(Surge(), "Height");
    }

    static bool Caught()
    {
        return (bool)Get(Surge(), "IsCaught");
    }

    static void Hold(float heightAboveSea)
    {
        Call(Surge(), "HoldHeightForTests", heightAboveSea);
    }

    [SetUp]
    public void SetUp()
    {
        LitKeyGuard.Save();
        savedInts.Clear();
        savedFloats.Clear();
        for (int i = 0; i < IntKeys.Length; i++)
        {
            savedInts[IntKeys[i]] = PlayerPrefs.HasKey(IntKeys[i]) ? PlayerPrefs.GetInt(IntKeys[i]) : -1;
        }

        for (int i = 0; i < FloatKeys.Length; i++)
        {
            savedFloats[FloatKeys[i]] = PlayerPrefs.HasKey(FloatKeys[i]) ? PlayerPrefs.GetFloat(FloatKeys[i]) : float.NaN;
        }

        // A first-visit intro card pauses the game.
        PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
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

        foreach (KeyValuePair<string, float> pair in savedFloats)
        {
            if (float.IsNaN(pair.Value))
            {
                PlayerPrefs.DeleteKey(pair.Key);
            }
            else
            {
                PlayerPrefs.SetFloat(pair.Key, pair.Value);
            }
        }

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

        // This fixture is about the surge, not the hunting lightning.
        StormSurgeSwitch.SetHunter(true);
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

    static Component Lantern()
    {
        return (Component)Find("Lantern");
    }

    static void Refuel()
    {
        Call(Lantern(), "AddFuel", 100f);
    }

    static Component Beacon(int index)
    {
        Transform root = GameObject.Find("Beacons").transform;
        return root.GetChild(index).GetComponent(GameType("Beacon"));
    }

    static bool LightBeacon(int index)
    {
        Refuel();
        return (bool)Call(Beacon(index), "TryLight");
    }

    static Component Player()
    {
        return (Component)Find("PlayerController");
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

    static float KeeperHeight()
    {
        return Player().transform.position.y - Sea();
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

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator SurgeRisesAfterFirstBeacon()
    {
        yield return Load("Island5");
        Component surge = Surge();
        Assert.IsNotNull(surge, "Island 5 has a StormSurge");
        yield return Wait(2f);
        Assert.IsFalse((bool)Get(surge, "Started"), "nothing before the first beacon");
        Assert.AreEqual(0f, (float)Get(surge, "Elapsed"), 1e-4f);
        Assert.AreEqual(0, (int)Get(surge, "DrawCalls"), "nothing is drawn before the surge");

        Assert.IsTrue(LightBeacon(0));
        yield return Wait(1f);
        Assert.IsTrue((bool)Get(surge, "Started"), "the first beacon starts the clock");
        Assert.AreEqual(1f, (float)Get(surge, "Elapsed"), 0.3f);

        Call(surge, "SetElapsedForTests", StormSurgeLogic.StartDelay - 5f);
        yield return null;
        Assert.AreEqual(0f, Height(), 1e-3f, "still waiting out the start delay");
        Call(surge, "SetElapsedForTests", StormSurgeLogic.StartDelay + 30f);
        yield return null;
        float expected = StormSurgeLogic.Height(StormSurgeLogic.StartDelay + 30f, 1, (float)Get(surge, "CapHeight"));
        Assert.AreEqual(expected, Height(), 0.2f, "30 s after the delay, one beacon pushing it back");
        Assert.Greater(Height(), 15f);
        Assert.AreEqual(Sea() + Height(), (float)Get(surge, "SurfaceY"), 1e-3f);
        Assert.AreEqual(1, (int)Get(surge, "DrawCalls"), "one draw call");
        Assert.Greater((float)Get(surge, "Visibility"), 0.99f);

        // The surface follows the surge height.
        Transform layer = surge.transform.Find("SurgeLayer");
        Assert.IsNotNull(layer);
        Assert.AreEqual((float)Get(surge, "SurfaceY"), layer.position.y, 0.5f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator CaughtDrainsThreeTimes()
    {
        yield return Load("Island5");
        Assert.IsTrue(LightBeacon(2));
        Teleport(Walkway(7f));
        yield return Wait(1.5f);
        Component lantern = Lantern();
        Component surge = Surge();
        Assert.IsTrue((bool)Get(lantern, "InSafeLight"), "the keeper stands in the lit beacon's safe ring");
        float calm = (float)Get(lantern, "DrainRelative");

        Hold(KeeperHeight() + 8f);
        yield return Wait(0.6f);
        Assert.IsTrue(Caught(), "below the surface");
        Assert.IsTrue((bool)StaticProperty("StormSurge", "SafeLightSuppressed"));
        Assert.IsFalse((bool)Get(lantern, "InSafeLight"), "safe rings do not protect inside the surge");
        Assert.AreEqual(StormSurgeLogic.CaughtDrain, (float)Call(lantern, "GetDrainModifier", surge), 1e-4f);
        float escalation = (float)Get(lantern, "EscalationMultiplier");
        Assert.GreaterOrEqual((float)Get(lantern, "DrainRelative"), 2.99f * escalation, "drain x3 and no safe discount");
        Assert.Greater((float)Get(lantern, "DrainRelative"), calm * 2.9f);

        // The fuel really falls ~3x as fast.
        float before = (float)Get(lantern, "Fuel");
        yield return Wait(1f);
        float after = (float)Get(lantern, "Fuel");
        float basePerSecond = (float)Get(lantern, "BaseDrainPerSecond");
        Assert.GreaterOrEqual(before - after, basePerSecond * (float)Get(lantern, "DifficultyDrain") * 3f * escalation * 0.9f, "fuel drained by " + (before - after).ToString("F2"));

        // Heartbeat, dark fog.
        object audio = StaticProperty("AudioManager", "Instance");
        if (audio != null)
        {
            Assert.IsTrue((bool)Get(audio, "HeartbeatOverride"), "the heartbeat plays");
        }

        float fog = 0f;
        for (int i = 0; i < 90 && fog < 0.2f; i++)
        {
            Refuel();
            yield return null;
            fog = (float)Get(surge, "FogAmount");
        }

        Assert.Greater(fog, 0.2f, "dark fog once the camera is below the surface");
        Assert.Greater(RenderSettings.fogDensity, 0.05f);

        // Lifting the surge away ends all of it.
        Call(surge, "HoldHeightForTests", float.NaN);
        yield return Wait(6f);
        Refuel();
        yield return Wait(0.5f);
        Assert.IsFalse(Caught());
        Assert.AreEqual(1f, (float)Call(lantern, "GetDrainModifier", surge), 1e-4f);
        Assert.IsTrue((bool)Get(lantern, "InSafeLight"), "the safe ring protects again");
        if (audio != null)
        {
            Assert.IsFalse((bool)Get(audio, "HeartbeatOverride"));
        }

        Assert.Less((float)Get(surge, "FogAmount"), 0.05f, "the fog clears");
    }

    // No softlock: the surge now rises faster than the keeper can climb, so a keeper caught low recovers by lighting the next beacon;
    // its 4 m push-back lifts the keeper clear and the drain is normal again.
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator CaughtKeeperRecoversByLightingABeacon()
    {
        yield return Load("Island5");
        Assert.IsTrue(LightBeacon(0));
        Component lantern = Lantern();
        Component surge = Surge();
        Teleport(Walkway(7.5f));
        yield return Wait(0.5f);
        // A real (not held) surge 1.6 m above the keeper with one beacon lit: seconds = delay + (height + one push-back) / rise rate.
        float surgeHeight = KeeperHeight() + 1.6f;
        Call(surge, "SetElapsedForTests", StormSurgeLogic.StartDelay + (surgeHeight + StormSurgeLogic.PushBack) / StormSurgeLogic.RisePerSecond);
        yield return Wait(0.3f);
        Assert.IsTrue(Caught(), "caught at " + KeeperHeight().ToString("F1") + " m under a surge at " + Height().ToString("F1") + " m");
        Assert.AreEqual(StormSurgeLogic.CaughtDrain, (float)Call(lantern, "GetDrainModifier", surge), 1e-4f);

        // Lighting the next beacon takes 4 m off the surge.
        Assert.IsTrue(LightBeacon(1));
        float elapsed = 0f;
        while (elapsed < 3f && Caught())
        {
            Refuel();
            yield return null;
            elapsed += Time.deltaTime;
        }

        Assert.IsFalse(Caught(), "the push-back freed the keeper (keeper " + KeeperHeight().ToString("F1") + " m, surge " + Height().ToString("F1") + " m)");
        yield return Wait(0.3f);
        Assert.AreEqual(1f, (float)Call(lantern, "GetDrainModifier", surge), 1e-4f, "the drain is back to normal above the surge");
        Assert.IsFalse((bool)StaticProperty("StormSurge", "SafeLightSuppressed"));
        Assert.Less((float)Get(lantern, "DrainRelative"), 3f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RetryResetsSurge()
    {
        yield return Load("Island5");
        Assert.IsTrue(LightBeacon(0));
        Teleport(Walkway(3f));
        yield return Wait(1f);
        Hold(KeeperHeight() + 10f);
        yield return Wait(1.5f);
        Assert.IsTrue(Caught());
        Assert.IsTrue((bool)StaticProperty("StormSurge", "SafeLightSuppressed"));
        Component manager = (Component)Find("GameManager");
        Call(manager, "RestartLevel");
        yield return null;
        yield return null;
        yield return null;
        Component surge = Surge();
        Assert.IsNotNull(surge, "a fresh surge");
        Assert.IsFalse((bool)Get(surge, "Started"), "the clock is reset");
        Assert.AreEqual(0f, (float)Get(surge, "Elapsed"), 1e-4f);
        Assert.AreEqual(0f, Height(), 1e-4f);
        Assert.IsFalse(Caught());
        Assert.IsFalse((bool)StaticProperty("StormSurge", "SafeLightSuppressed"), "nothing static leaks");
        Assert.AreEqual(1f, (float)Call(Lantern(), "GetDrainModifier", surge), 1e-4f);
        Assert.AreEqual(0, (int)Get(surge, "DrawCalls"));
        object audio = StaticProperty("AudioManager", "Instance");
        if (audio != null)
        {
            Assert.IsFalse((bool)Get(audio, "HeartbeatOverride"), "the heartbeat is off");
        }

        Assert.AreEqual(0f, (float)Get(surge, "FogAmount"), 1e-4f);
        Assert.IsFalse((bool)GameType("StormSurge").GetField("DisabledForTests", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator LampStopsSurge()
    {
        yield return Load("Island5");
        for (int i = 0; i < 8; i++)
        {
            Assert.IsTrue(LightBeacon(i), "beacon " + i);
            yield return null;
        }

        yield return Wait(5f);
        Component gate = (Component)Find("SummitGate");
        Assert.IsTrue((bool)Get(gate, "Open"), "the gate opened");
        Component surge = Surge();
        Call(surge, "SetElapsedForTests", StormSurgeLogic.StartDelay + 100f);
        yield return null;
        Assert.IsTrue((bool)Get(surge, "Active"), "the surge is running");
        float height = Height();
        Assert.Greater(height, 3f, "the cap minus eight push-backs still leaves a surge");
        // Caught: stand low under a held surge, then light the lamp.
        Teleport(Walkway(3f));
        yield return Wait(0.5f);
        Hold(KeeperHeight() + 6f);
        yield return Wait(0.6f);
        Assert.IsTrue(Caught());

        GameType("FinaleSequence").GetField("TestSpeed", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 8f);
        Refuel();
        Assert.IsTrue((bool)Call((Component)Find("GreatLamp"), "TryLight"), "the lamp lights");
        yield return Wait(0.5f);
        Assert.IsTrue((bool)Get(surge, "Stopped"), "the lamp stops the surge");
        Assert.IsFalse((bool)Get(surge, "Active"));
        Assert.IsFalse(Caught(), "no longer caught");
        Assert.AreEqual(1f, (float)Call(Lantern(), "GetDrainModifier", surge), 1e-4f);
        float frozen = Height();
        yield return Wait(2f);
        Assert.AreEqual(frozen, Height(), 1e-3f, "it no longer moves");
        yield return Wait(3f);
        Assert.AreEqual(0, (int)Get(surge, "DrawCalls"), "and it fades away");
    }
}
}
