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
        Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode, "fire loop uses linear rolloff");
        Assert.AreEqual(2f, source.minDistance, 0.001f);
        // Linear rolloff: gain falls from 1 at minDistance to 0 at maxDistance and stays 0 beyond it.
        float atEleven = Mathf.Clamp01(1f - (11f - source.minDistance) / (source.maxDistance - source.minDistance));
        Assert.AreEqual(0f, atEleven, 1e-6f, "silent at 11 m from the listener");
        Assert.IsTrue(source.isPlaying);
        for (int i = 1; i < found.Length; i++)
        {
            Assert.IsNull(((Component)found[i]).transform.Find("FireLoop"), "the other beacons stay silent");
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator BeaconFireLoopIsGoneAfterSceneReload()
    {
        yield return Load("Island1");
        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(GameType("Beacon"), FindObjectsSortMode.None);
        Component beacon = (Component)found[0];
        Assert.IsTrue((bool)beacon.GetType().GetMethod("TryLight").Invoke(beacon, null), "the beacon lights");
        Transform loop = beacon.transform.Find("FireLoop");
        Assert.IsNotNull(loop, "the lit beacon owns a fire loop");
        AudioSource source = loop.GetComponent<AudioSource>();
        Assert.IsTrue(source.isPlaying);
        yield return Load("Island1");
        Assert.IsTrue(source == null, "the old fire loop source is destroyed with its scene");
        UnityEngine.Object[] reloaded = UnityEngine.Object.FindObjectsByType(GameType("Beacon"), FindObjectsSortMode.None);
        for (int i = 0; i < reloaded.Length; i++)
        {
            Assert.IsNull(((Component)reloaded[i]).transform.Find("FireLoop"), "no beacon starts lit after a reload");
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
        played.Clear();
        logButton.onClick.Invoke();
        Assert.AreEqual(0, Count(SoundCues.UiPageTurn), "opening the log plays no page turn");
        played.Clear();
        MethodInfo turn = log.GetType().GetMethod("TurnTo");
        turn.Invoke(log, new object[] { 1 });
        Assert.AreEqual(1, Count(SoundCues.UiPageTurn), "turning a page");
        turn.Invoke(log, new object[] { 1 });
        Assert.AreEqual(1, Count(SoundCues.UiPageTurn), "no sound when the page does not change");
    }

    static object Manager()
    {
        return GameType("GameManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    static IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++)
        {
            yield return null;
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator IntroCardMakesNoPauseCuesButRealPauseDoes()
    {
        PlayerPrefs.DeleteKey(IntroKey);
        yield return Load("Island1");
        object manager = Manager();
        Type type = manager.GetType();
        Assert.IsTrue((bool)type.GetProperty("IntroShowing").GetValue(manager), "the intro card shows on a first visit");
        yield return Frames(5);
        Assert.AreEqual(0, Count(SoundCues.UiPauseOpen), "no pause-open cue for the intro card");
        type.GetMethod("EndIntro").Invoke(manager, null);
        yield return Frames(5);
        Assert.AreEqual(0, Count(SoundCues.UiPauseOpen), "still none after the intro");
        Assert.AreEqual(0, Count(SoundCues.UiPauseClose), "no pause-close cue when the intro card closes");

        type.GetMethod("Pause").Invoke(manager, null);
        yield return Frames(5);
        Assert.AreEqual(1, Count(SoundCues.UiPauseOpen), "pause opens with its cue");
        type.GetMethod("Resume").Invoke(manager, null);
        yield return Frames(5);
        Assert.AreEqual(1, Count(SoundCues.UiPauseClose), "resume closes with its cue");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ToggleOnlyOnUserChange()
    {
        yield return Load("MainMenu");
        Component menu = (Component)UnityEngine.Object.FindFirstObjectByType(GameType("MainMenuScreen"));
        Button settingsButton = null;
        Button[] buttons = menu.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == "SettingsButton")
            {
                settingsButton = buttons[i];
            }
        }

        Assert.IsNotNull(settingsButton);
        played.Clear();
        settingsButton.onClick.Invoke();
        yield return Frames(3);
        Assert.AreEqual(0, Count(SoundCues.UiToggle), "opening settings fills the rows without a toggle sound");
        SwitchRow row = UnityEngine.Object.FindFirstObjectByType<SwitchRow>(FindObjectsInactive.Include);
        Assert.IsNotNull(row, "a switch row");
        row.Cycle(1);
        Assert.AreEqual(1, Count(SoundCues.UiToggle), "a user change toggles");
        row.Index = row.Index == 0 ? 1 : 0;
        Assert.AreEqual(1, Count(SoundCues.UiToggle), "setting the index from code stays silent");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator AmbienceFollowsScene()
    {
        yield return Load("Island1");
        object manager = GameType("AudioManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Assert.AreEqual(SoundCues.AmbienceIsland1, (string)manager.GetType().GetProperty("AmbienceCue").GetValue(manager));
        yield return Load("MainMenu");
        manager = GameType("AudioManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Assert.IsNull((string)manager.GetType().GetProperty("AmbienceCue").GetValue(manager), "the menu has no island bed");
    }

    [Test]
    public void WaterSurfaceMapsToWaterFootstep()
    {
        MethodInfo map = GameType("AudioManager").GetMethod("FootstepCueFor", BindingFlags.Public | BindingFlags.Static);
        Assert.AreEqual(SoundCues.FootstepGrass, map.Invoke(null, new object[] { 0 }));
        Assert.AreEqual(SoundCues.FootstepDirt, map.Invoke(null, new object[] { 1 }));
        Assert.AreEqual(SoundCues.FootstepRock, map.Invoke(null, new object[] { 2 }));
        Assert.AreEqual(SoundCues.FootstepWater, map.Invoke(null, new object[] { 3 }));
    }
}
}
