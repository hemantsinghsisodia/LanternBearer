using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
// Game sounds reach the player through named cues. The test listens to AudioManager.CuePlayed (a static spy event).
// Game types are reached by reflection (this assembly cannot reference Assembly-CSharp).
public class GameCueTest
{
    const string IntroKey = "LanternKeeperIntro_island1";
    const string GraphicsKey = "LanternKeeperGraphics";

    bool hadIntroKey;
    int previousIntro;
    int previousGraphics;
    readonly List<string> played = new List<string>();
    Action<string> spy;

    [SetUp]
    public void SetUp()
    {
        hadIntroKey = PlayerPrefs.HasKey(IntroKey);
        previousIntro = PlayerPrefs.GetInt(IntroKey, 0);
        previousGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        PlayerPrefs.SetInt(IntroKey, 1);
        played.Clear();
        spy = cue => played.Add(cue);
        EventInfo cuePlayed = GameType("AudioManager").GetEvent("CuePlayed", BindingFlags.Public | BindingFlags.Static);
        cuePlayed.AddEventHandler(null, spy);
    }

    [TearDown]
    public void TearDown()
    {
        EventInfo cuePlayed = GameType("AudioManager").GetEvent("CuePlayed", BindingFlags.Public | BindingFlags.Static);
        cuePlayed.RemoveEventHandler(null, spy);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (hadIntroKey)
        {
            PlayerPrefs.SetInt(IntroKey, previousIntro);
        }
        else
        {
            PlayerPrefs.DeleteKey(IntroKey);
        }

        PlayerPrefs.SetInt(GraphicsKey, previousGraphics);
        PlayerPrefs.Save();
    }

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static IEnumerator Load(string scene)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone)
        {
            yield return null;
        }

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
    }

    static Component Lantern()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Assert.IsNotNull(player, "player");
        Component lantern = player.GetComponentInChildren(GameType("Lantern"));
        Assert.IsNotNull(lantern, "lantern");
        return lantern;
    }

    static float Fuel(Component lantern)
    {
        return (float)lantern.GetType().GetProperty("Fuel").GetValue(lantern);
    }

    static float MaxFuel(Component lantern)
    {
        return (float)lantern.GetType().GetProperty("MaxFuel").GetValue(lantern);
    }

    // Spends fuel until `normalized` of the tank is left.
    static void SpendTo(Component lantern, float normalized)
    {
        float amount = Fuel(lantern) - MaxFuel(lantern) * normalized;
        lantern.GetType().GetMethod("TrySpend").Invoke(lantern, new object[] { amount });
    }

    static void Add(Component lantern, float amount)
    {
        lantern.GetType().GetMethod("AddFuel").Invoke(lantern, new object[] { amount });
    }

    int Count(string cue)
    {
        int n = 0;
        for (int i = 0; i < played.Count; i++)
        {
            if (played[i] == cue)
            {
                n++;
            }
        }

        return n;
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator RefuelCuePlaysOnFireflyCollect()
    {
        yield return Load("Island1");
        Component lantern = Lantern();
        SpendTo(lantern, 0.5f);
        played.Clear();
        UnityEngine.Object[] fireflies = UnityEngine.Object.FindObjectsByType(GameType("Firefly"), FindObjectsSortMode.None);
        Assert.Greater(fireflies.Length, 0, "fireflies on Island1");
        Component firefly = (Component)fireflies[0];
        Collider player = GameObject.FindGameObjectWithTag("Player").GetComponent<Collider>();
        firefly.GetType().GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(firefly, new object[] { player });
        Assert.AreEqual(1, Count(SoundCues.LanternRefuel), "refuel cue on a fuel gain");
        Assert.AreEqual(1, Count(SoundCues.FireflyChime), "the firefly chime still plays");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator StealCuePlaysOnShadeSteal()
    {
        yield return Load("Island1");
        played.Clear();
        FieldInfo stole = GameType("Shade").GetField("Stole", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(stole, "Shade.Stole backing field");
        Action<float> handler = (Action<float>)stole.GetValue(null);
        Assert.IsNotNull(handler, "someone listens to Shade.Stole");
        handler(5f);
        Assert.AreEqual(1, Count(SoundCues.ShadeSteal), "steal cue");
        Assert.AreEqual(1, Count(SoundCues.KeeperHit), "the keeper reacts with a hit cue");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator SputterOncePerCrossing()
    {
        yield return Load("Island1");
        Component lantern = Lantern();
        SpendTo(lantern, 0.5f);
        played.Clear();
        SpendTo(lantern, 0.10f);
        Assert.AreEqual(1, Count(SoundCues.LanternSputter), "first crossing below 15%");
        SpendTo(lantern, 0.08f);
        Assert.AreEqual(1, Count(SoundCues.LanternSputter), "no repeat while it stays below");
        Add(lantern, MaxFuel(lantern) * 0.30f);
        Assert.AreEqual(1, Count(SoundCues.LanternRefuel), "the gain refuels");
        SpendTo(lantern, 0.10f);
        Assert.AreEqual(2, Count(SoundCues.LanternSputter), "second crossing after a refill");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator BeaconFireLoopOnlyWhenLit()
    {
        yield return Load("Island1");
        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(GameType("Beacon"), FindObjectsSortMode.None);
        Assert.Greater(found.Length, 1, "beacons on Island1");
        Assert.AreEqual(0, Count(SoundCues.BeaconFire), "no fire loop before any beacon is lit");
        for (int i = 0; i < found.Length; i++)
        {
            Assert.IsNull(((Component)found[i]).transform.Find("FireLoop"), "an unlit beacon has no fire loop");
        }

        Component beacon = (Component)found[0];
        Assert.IsTrue((bool)beacon.GetType().GetMethod("TryLight").Invoke(beacon, null), "the beacon lights");
        Assert.AreEqual(1, Count(SoundCues.BeaconFire), "one fire loop started");
        Transform loop = beacon.transform.Find("FireLoop");
        Assert.IsNotNull(loop, "the lit beacon owns a fire loop");
        AudioSource source = loop.GetComponent<AudioSource>();
        Assert.IsTrue(source.loop);
        Assert.AreEqual(1f, source.spatialBlend, 0.001f, "3D");
        Assert.AreEqual(10f, source.maxDistance, 0.001f);
        Assert.IsTrue(source.isPlaying);
        for (int i = 1; i < found.Length; i++)
        {
            Assert.IsNull(((Component)found[i]).transform.Find("FireLoop"), "the other beacons stay silent");
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator PageTurnCueOnLogPage()
    {
        yield return Load("MainMenu");
        Component menu = (Component)UnityEngine.Object.FindFirstObjectByType(GameType("MainMenuScreen"));
        Component log = (Component)UnityEngine.Object.FindFirstObjectByType(GameType("KeepersLogScreen"), FindObjectsInactive.Include);
        Assert.IsNotNull(menu);
        Assert.IsNotNull(log);
        Button logButton = null;
        Button[] buttons = menu.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == "LogButton")
            {
                logButton = buttons[i];
            }
        }

        Assert.IsNotNull(logButton);
        logButton.onClick.Invoke();
        played.Clear();
        MethodInfo turn = log.GetType().GetMethod("TurnTo");
        turn.Invoke(log, new object[] { 1 });
        Assert.AreEqual(1, Count(SoundCues.UiPageTurn), "turning a page");
        turn.Invoke(log, new object[] { 1 });
        Assert.AreEqual(1, Count(SoundCues.UiPageTurn), "no sound when the page does not change");
    }
}
}
