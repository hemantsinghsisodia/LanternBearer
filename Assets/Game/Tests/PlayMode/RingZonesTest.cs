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
// Island 5 height rings: the ratchet, the storm that switches on with it, thin air, the music floor and hush, and the Retry cleanup.
// Game types are reached by reflection (this assembly can't reference Assembly-CSharp); the Logic types are used directly.
// Every frame advances the game by the same step (captureDeltaTime), so timings do not depend on how slowly the editor renders.
public class RingZonesTest
{
    const float Frame = 1f / 60f;
    static readonly string[] IntKeys =
    {
        "LanternKeeperIntro_island1", "LanternKeeperIntro_island2", "LanternKeeperIntro_island3", "LanternKeeperIntro_island4", "LanternKeeperIntro_island5",
        "LanternKeeperWon_island1", "LanternKeeperWon_island2", "LanternKeeperWon_island3", "LanternKeeperWon_island4", "LanternKeeperWon_island5"
    };
    static readonly string[] FloatKeys = { "LanternKeeperBest_island1", "LanternKeeperBest_island2", "LanternKeeperBest_island3", "LanternKeeperBest_island4", "LanternKeeperBest_island5" };

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
    float previousCaptureDelta;
    float previousMaxDelta;
    float previousScale;

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

    static object CallStatic(string typeName, string method, params object[] args)
    {
        MethodInfo info = GameType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + method + " not found");
        return info.Invoke(null, args);
    }

    static object StaticProperty(string typeName, string property)
    {
        PropertyInfo info = GameType(typeName).GetProperty(property, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + property + " not found");
        return info.GetValue(null);
    }

    static object PrivateStatic(string typeName, string method, params object[] args)
    {
        MethodInfo info = GameType(typeName).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + method + " not found");
        return info.Invoke(null, args);
    }

    static Ring Reached()
    {
        return (Ring)StaticProperty("RingZones", "Reached");
    }

    [SetUp]
    public void SetUp()
    {
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
        PlayerPrefs.SetInt("LanternKeeperIntro_island1", 1);
        PlayerPrefs.SetInt("LanternKeeperIntro_island2", 1);
        PlayerPrefs.SetInt("LanternKeeperIntro_island3", 1);
        PlayerPrefs.SetInt("LanternKeeperIntro_island4", 1);
        PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
        previousCaptureDelta = Time.captureDeltaTime;
        previousMaxDelta = Time.maximumDeltaTime;
        previousScale = Time.timeScale;
        Time.captureDeltaTime = Frame;
        Time.maximumDeltaTime = 0.05f;
    }

    [TearDown]
    public void TearDown()
    {
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

    // Game seconds, counted from the frame step.
    static IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    static void Refuel()
    {
        Component lantern = (Component)Find("Lantern");
        Call(lantern, "AddFuel", 100f);
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

    static void Teleport(Vector3 position)
    {
        Component player = (Component)Find("PlayerController");
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

    static IEnumerator ReachRing(Ring ring)
    {
        float[] heights = { 2f, 16f, 31f, 0f };
        if (ring == Ring.Summit)
        {
            // The summit span runs to the plateau, so the top ring is the lamp plateau itself.
            Terrain terrain = Terrain.activeTerrain;
            Vector3 plateau = new Vector3(3f, 0f, 0f);
            plateau.y = terrain.SampleHeight(plateau) + terrain.transform.position.y + 0.4f;
            Teleport(plateau);
        }
        else
        {
            Teleport(Walkway(heights[(int)ring]));
        }
        float elapsed = 0f;
        while (Reached() < ring && elapsed < 4f)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        Assert.AreEqual(ring, Reached(), "reached " + ring + " within 4 s");
    }

    static int LightBridgeCount()
    {
        FieldInfo all = GameType("LightBridge").GetField("All", BindingFlags.Public | BindingFlags.Static);
        return ((System.Collections.ICollection)all.GetValue(null)).Count;
    }

    // The Island 5 finale runs 52 s on unscaled time; the test speed makes it 6.5 s (FinaleSequence resets it when the scene unloads).
    static void FastFinale()
    {
        GameType("FinaleSequence").GetField("TestSpeed", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 8f);
    }

    static Component Director()
    {
        return (Component)Find("MusicDirector");
    }

    static float Floor()
    {
        return (float)Get(Director(), "TensionFloor");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator WindAndShadesStartAtMid()
    {
        yield return Load("Island5");
        Assert.AreEqual(Ring.Low, Reached());
        Assert.IsNull(StaticProperty("Wind", "Instance"), "no wind at the start");
        Assert.IsNull(StaticProperty("Lightning", "Instance"), "no lightning at the start");
        Assert.AreEqual(0, (int)CallStatic("Shade", "LivingCount"), "no Shades at the start");
        yield return ReachRing(Ring.Mid);
        float elapsed = 0f;
        while ((int)CallStatic("Shade", "LivingCount") <= 0 && elapsed < 2f)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        Component wind = (Component)StaticProperty("Wind", "Instance");
        Assert.IsNotNull(wind, "wind is on at Mid");
        Assert.IsTrue(((Behaviour)wind).isActiveAndEnabled);
        Assert.Greater((int)CallStatic("Shade", "LivingCount"), 0, "Shades patrol within 2 s of Mid");
        Assert.IsNull(StaticProperty("Lightning", "Instance"), "lightning waits for High");
        Assert.AreEqual(0.3f, Floor(), 1e-4f, "Mid raises the tension floor to 0.3");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator SpawnsStayOnTheWalkwayAtOrBelowTheRing()
    {
        yield return Load("Island5");
        // Moths spawn at the start, on the Low stretch of the walkway.
        yield return null;
        UnityEngine.Object[] moths = UnityEngine.Object.FindObjectsByType(GameType("Moth"), FindObjectsSortMode.None);
        Assert.Greater(moths.Length, 0, "moths spawned");
        for (int i = 0; i < moths.Length; i++)
        {
            Vector3 p = ((Component)moths[i]).transform.position;
            float u = SpireLayout.NearestU(new Vector2(p.x, p.z));
            float off = (SpireLayout.PathPoint(u) - new Vector2(p.x, p.z)).magnitude;
            Assert.LessOrEqual(off, SpireLayout.PathWidth + 1.5f, "moth " + i + " is " + off.ToString("F1") + " m from the path");
            Assert.LessOrEqual(SpireLayout.PathHeight(u), RingLogic.LowTop + 2f, "moth " + i + " spawned above the Low ring");
        }

        yield return ReachRing(Ring.Mid);
        HashSet<UnityEngine.Object> seen = new HashSet<UnityEngine.Object>();
        float elapsed = 0f;
        int checkedShades = 0;
        while (elapsed < 2f)
        {
            yield return null;
            elapsed += Time.deltaTime;
            UnityEngine.Object[] shades = UnityEngine.Object.FindObjectsByType(GameType("Shade"), FindObjectsSortMode.None);
            for (int i = 0; i < shades.Length; i++)
            {
                Component shade = (Component)shades[i];
                if (!seen.Add(shade))
                {
                    continue;
                }

                Vector3 p = shade.transform.position;
                float u = SpireLayout.NearestU(new Vector2(p.x, p.z));
                float off = (SpireLayout.PathPoint(u) - new Vector2(p.x, p.z)).magnitude;
                Assert.LessOrEqual(off, SpireLayout.PathWidth + 1.5f, "Shade is " + off.ToString("F1") + " m from the path at spawn");
                Assert.LessOrEqual(SpireLayout.PathHeight(u), RingLogic.MidTop + 2f, "Shade spawned above the Mid ring");
                checkedShades++;
            }
        }

        Assert.Greater(checkedShades, 0, "saw the Shades spawn");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator JumpApexDoesNotRaiseTheRing()
    {
        yield return Load("Island5");
        yield return ReachRing(Ring.Mid);
        // The keeper hangs 16 m above a Mid walkway point (an apex far past the High boundary), airborne, for two seconds.
        Vector3 high = Walkway(16f);
        float elapsed = 0f;
        while (elapsed < 2f)
        {
            Teleport(high + Vector3.up * 16f);
            yield return null;
            elapsed += Time.deltaTime;
            Assert.AreEqual(Ring.Mid, Reached(), "an airborne keeper never raises the ring (" + elapsed.ToString("F2") + " s)");
        }
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator LightningAndThinAirAtHigh()
    {
        yield return Load("Island5");
        Component lantern = (Component)Find("Lantern");
        Assert.AreEqual(1f, (float)Get(lantern, "RadiusScale"), 1e-4f);
        yield return ReachRing(Ring.High);
        yield return Wait(0.3f);
        Assert.IsNotNull(StaticProperty("Lightning", "Instance"), "lightning is on at High");
        Assert.IsTrue(((Behaviour)StaticProperty("Lightning", "Instance")).isActiveAndEnabled);
        Assert.AreEqual(0.80f, (float)Get(lantern, "RadiusScale"), 1e-4f);
        Component thin = (Component)Find("ThinAir");
        Assert.AreEqual(1.15f, (float)Call(lantern, "GetDrainModifier", thin), 1e-4f);
        Assert.AreEqual(0.6f, Floor(), 1e-4f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RatchetSurvivesDescent()
    {
        yield return Load("Island5");
        Component lantern = (Component)Find("Lantern");
        yield return ReachRing(Ring.High);
        yield return Wait(0.3f);
        Teleport(Walkway(2f));
        yield return Wait(2f);
        Assert.AreEqual(Ring.High, Reached(), "walking back down keeps the ring");
        Assert.IsNotNull(StaticProperty("Lightning", "Instance"), "lightning stays on");
        Assert.AreEqual(0.80f, (float)Get(lantern, "RadiusScale"), 1e-4f);
        Assert.AreEqual(0.6f, Floor(), 1e-4f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator SummitHushPlays()
    {
        yield return Load("Island5");
        Component director = Director();
        AudioSource track = (AudioSource)Get(director, "Track");
        yield return Wait(3.2f);
        Assert.Greater(track.volume, 0.05f, "the track is audible before the summit");
        Assert.IsFalse((bool)Get(director, "Hushed"));
        yield return ReachRing(Ring.Summit);
        Assert.IsTrue((bool)Get(director, "Hushed"), "reaching the summit starts the hush");
        float elapsed = 0f;
        while (track.volume > 0.0001f && elapsed < 3.5f)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Frame;
        }

        Assert.LessOrEqual(track.volume, 0.0001f, "the track faded out within 3.5 s");
        AudioSource tension = (AudioSource)Get(director, "Tension");
        Assert.LessOrEqual(tension.volume, 0.0001f, "the tension layer is out too");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator BridgesShelterFromWindAndRain()
    {
        yield return Load("Island5");
        yield return ReachRing(Ring.High);
        Assert.IsTrue(LightBeacon(1));
        yield return Wait(2.8f);
        Component bridge = GameObject.Find("LightBridge_Gap0").GetComponent(GameType("LightBridge"));
        Assert.IsTrue((bool)Get(bridge, "Walkable"));
        Vector3 mid = (Vector3)Get(bridge, "MidPoint") + Vector3.up * 0.3f;
        Teleport(mid);
        yield return Wait(0.5f);
        Assert.IsTrue((bool)PrivateStatic("Wind", "InSafeRing", mid), "the wind treats the bridge as shelter");
        Component rain = (Component)Find("Rain");
        MethodInfo inSafe = rain.GetType().GetMethod("InSafeRing", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(inSafe);
        Assert.IsTrue((bool)inSafe.Invoke(rain, null), "the rain treats the bridge as shelter");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RetryResetsRingsAndBridges()
    {
        yield return Load("Island5");
        yield return ReachRing(Ring.High);
        Assert.IsTrue(LightBeacon(1));
        yield return Wait(2.8f);
        Assert.AreEqual(1, (int)StaticProperty("LightBridge", "BuiltCount"), "a bridge is built");
        Assert.AreEqual(0.6f, Floor(), 1e-4f);
        Component manager = (Component)Find("GameManager");
        Call(manager, "RestartLevel");
        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(Ring.Low, Reached(), "rings reset");
        Assert.AreEqual(0, (int)StaticProperty("LightBridge", "BuiltCount"), "no bridge survives the retry");
        Assert.AreEqual(0f, Floor(), 1e-4f, "the tension floor resets");
        Assert.AreEqual(1f, (float)Get(Find("Lantern"), "RadiusScale"), 1e-4f, "thin air resets");
        Assert.IsNull(StaticProperty("Wind", "Instance"), "the storm is off again");
        Assert.IsNull(StaticProperty("Lightning", "Instance"));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RetryCleansUpAfterTheLampAndGate()
    {
        yield return Load("Island5");
        int wins = 0;
        Component manager = (Component)Find("GameManager");
        EventInfo won = manager.GetType().GetEvent("WonGame");
        Action count = () => wins++;
        won.AddEventHandler(manager, count);
        for (int i = 0; i < 8; i++)
        {
            Assert.IsTrue(LightBeacon(i), "beacon " + i);
            yield return null;
        }

        yield return Wait(5f);
        Component gate = (Component)Find("SummitGate");
        Assert.IsTrue((bool)Get(gate, "Open"), "the gate opened");
        FastFinale();
        Call((Component)Find("GreatLamp"), "Light");
        float waited = 0f;
        while (!(bool)Get(manager, "Won") && waited < 40f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Frame;
            Refuel();
        }

        Assert.IsTrue((bool)Get(manager, "Won"));
        Assert.AreEqual(1, wins, "the first run wins once");
        won.RemoveEventHandler(manager, count);

        Call(manager, "RestartLevel");
        yield return null;
        yield return null;
        yield return null;
        manager = (Component)Find("GameManager");
        Assert.IsFalse((bool)Get((Component)Find("SummitGate"), "Open"), "the gate is closed again");
        Assert.AreEqual(0, (int)StaticProperty("LightBridge", "BuiltCount"));
        Assert.AreEqual(Ring.Low, Reached());
        Assert.IsFalse((bool)Get((Component)Find("GreatLamp"), "Lit"), "the lamp is dark again");
        Assert.AreEqual(4, ((System.Collections.ICollection)GameType("LightBridge").GetField("All", BindingFlags.Public | BindingFlags.Static).GetValue(null)).Count,
            "four bridges, no duplicates");

        // One beacon: exactly one bridge reacts (a duplicate Beacon.Lit handler would still show here as extra bridges or builds).
        Assert.IsTrue(LightBeacon(1));
        yield return Wait(2.8f);
        int triggered = 0;
        for (int i = 0; i < 4; i++)
        {
            Component bridge = GameObject.Find("LightBridge_Gap" + i).GetComponent(GameType("LightBridge"));
            triggered += (bool)Get(bridge, "Triggered") ? 1 : 0;
        }

        Assert.AreEqual(1, triggered, "one beacon starts exactly one bridge");
        Assert.AreEqual(1, (int)StaticProperty("LightBridge", "BuiltCount"));

        // The second run wins exactly once, with exactly one win stinger.
        wins = 0;
        EventInfo won2 = manager.GetType().GetEvent("WonGame");
        Action count2 = () => wins++;
        won2.AddEventHandler(manager, count2);
        for (int i = 0; i < 8; i++)
        {
            if (i != 1)
            {
                Assert.IsTrue(LightBeacon(i), "beacon " + i);
                yield return null;
            }
        }

        yield return Wait(5f);
        Component director = Director();
        int stingersBefore = (int)Get(director, "StingerRequests");
        FastFinale();
        Call((Component)Find("GreatLamp"), "Light");
        waited = 0f;
        while (!(bool)Get(manager, "Won") && waited < 40f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Frame;
            Refuel();
        }

        yield return Wait(0.5f);
        Assert.IsTrue((bool)Get(manager, "Won"), "the second run wins");
        Assert.AreEqual(1, wins, "exactly one WonGame");
        // Island 5 plays the finale cue instead of the win stinger (MusicDirector.OnWon skips it when the scene has a finale).
        Assert.AreEqual(0, (int)Get(director, "StingerRequests") - stingersBefore, "no win stinger: the finale cue plays instead");
        Assert.IsTrue((bool)Get(director, "FinalePlaying"), "the finale cue started");
        won2.RemoveEventHandler(manager, count2);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator OtherIslandsUnaffected()
    {
        string[] calm = { "Island1", "Island2", "Island3" };
        for (int i = 0; i < calm.Length; i++)
        {
            yield return Load(calm[i]);
            Component lantern = (Component)Find("Lantern");
            Assert.AreEqual(1f, (float)Get(lantern, "RadiusScale"), 1e-6f, calm[i] + " lantern radius is unscaled");
            Assert.AreEqual(0f, Floor(), 1e-6f, calm[i] + " has no tension floor");
            Assert.IsNull(Find("RingZones"), calm[i] + " has no rings");
            Assert.AreEqual(Ring.Low, Reached());
            Assert.AreEqual(0, LightBridgeCount(), calm[i] + " has no light bridges");
            if (i == 0)
            {
                Assert.IsNull(StaticProperty("Wind", "Instance"));
            }
        }

        yield return Load("Island4");
        yield return Wait(2f);
        Assert.IsNotNull(StaticProperty("Wind", "Instance"), "Island 4 wind still starts at once");
        Assert.IsNotNull(StaticProperty("Lightning", "Instance"), "Island 4 lightning still starts at once");
        Assert.IsNull(Find("RingZones"));
        Assert.AreEqual(0, LightBridgeCount(), "Island 4 has no light bridges");
        Assert.AreEqual(1f, (float)Get(Find("Lantern"), "RadiusScale"), 1e-6f);
        Assert.AreEqual(0f, (float)Get(Director(), "TensionFloor"), 1e-6f);
        Assert.Greater((int)CallStatic("Shade", "LivingCount"), 0, "Island 4 Shades still spawn at once");
    }
}
}
