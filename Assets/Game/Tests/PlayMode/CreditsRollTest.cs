using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The credits roll after the Island 5 finale, live. Game types (Assembly-CSharp) are reached by reflection; CreditsRoll is in the Hud assembly.
// FinaleSequence.TestSpeed (8) makes the 66 s finale take 8.25 s. The save keys a test writes are restored in TearDown.
public class CreditsRollTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    static readonly string[] IntKeys =
    {
        "LanternKeeperIntro_island1", "LanternKeeperIntro_island2", "LanternKeeperIntro_island3", "LanternKeeperIntro_island4", "LanternKeeperIntro_island5",
        "LanternKeeperWon_island5", "LanternKeeperWon_island1"
    };
    const string BestKey = "LanternKeeperBest_island5";
    const string Best1Key = "LanternKeeperBest_island1";

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    float savedBest;
    float savedBest1;
    int savedGraphics;
    float previousScale;
    int wonCount;

    public void OnWon()
    {
        wonCount++;
    }

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

    static void SetTestSpeed(float speed)
    {
        FieldInfo field = GameType("FinaleSequence").GetField("TestSpeed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "FinaleSequence.TestSpeed");
        field.SetValue(null, speed);
    }

    [SetUp]
    public void SetUp()
    {
        LitKeyGuard.Save();
        savedInts.Clear();
        for (int i = 0; i < IntKeys.Length; i++)
        {
            savedInts[IntKeys[i]] = PlayerPrefs.HasKey(IntKeys[i]) ? PlayerPrefs.GetInt(IntKeys[i]) : -1;
        }

        savedBest = PlayerPrefs.HasKey(BestKey) ? PlayerPrefs.GetFloat(BestKey) : float.NaN;
        savedBest1 = PlayerPrefs.HasKey(Best1Key) ? PlayerPrefs.GetFloat(Best1Key) : float.NaN;
        // A first-visit intro card pauses the game.
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.SetInt("LanternKeeperIntro_island" + i, 1);
        }

        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        PlayerPrefs.SetInt(GraphicsKey, 0);
        previousScale = Time.timeScale;
        wonCount = 0;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        LitKeyGuard.Restore();
        Time.timeScale = previousScale;
        SetTestSpeed(1f);
        CreditsRoll.TestSpeed = 1f;
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

        if (float.IsNaN(savedBest))
        {
            PlayerPrefs.DeleteKey(BestKey);
        }
        else
        {
            PlayerPrefs.SetFloat(BestKey, savedBest);
        }
        if (float.IsNaN(savedBest1))
        {
            PlayerPrefs.DeleteKey(Best1Key);
        }
        else
        {
            PlayerPrefs.SetFloat(Best1Key, savedBest1);
        }

        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        PlayerPrefs.Save();
        AsyncOperation load = SceneManager.LoadSceneAsync("MainMenu");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
    }

    static IEnumerator Load(string scene)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone)
        {
            yield return null;
        }

        StormSurgeSwitch.DisableFor(scene);
        yield return null;
        yield return null;
    }

    static IEnumerator WaitForMenu(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }

        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "loaded the main menu");
        yield return null;
        yield return null;
    }

    static CreditsRoll FindCredits()
    {
        CreditsRoll credits = UnityEngine.Object.FindAnyObjectByType<CreditsRoll>(FindObjectsInactive.Include);
        Assert.IsNotNull(credits, "Island5 has the credits roll");
        return credits;
    }

    static void Refuel()
    {
        Call(Find("Lantern"), "AddFuel", 100f);
    }

    static void LightTheLamp()
    {
        Transform root = GameObject.Find("Beacons").transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Refuel();
            Assert.IsTrue((bool)Call(root.GetChild(i).GetComponent(GameType("Beacon")), "TryLight"), "beacon " + i);
        }

        Refuel();
        Component lamp = (Component)Find("GreatLamp");
        Call(lamp, "Light");
        Assert.IsTrue((bool)Get(lamp, "Lit"), "the great lamp is lit");
    }

    void Subscribe(Component manager)
    {
        EventInfo won = manager.GetType().GetEvent("WonGame");
        won.AddEventHandler(manager, Delegate.CreateDelegate(won.EventHandlerType, this, GetType().GetMethod("OnWon")));
    }

    // Plays the finale at 8x on a fresh Island5 and returns once the credits have started.
    IEnumerator WinIsland5()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Component manager = (Component)Find("GameManager");
        Subscribe(manager);
        LightTheLamp();
        float start = Time.realtimeSinceStartup;
        CreditsRoll credits = FindCredits();
        while (!credits.Started && Time.realtimeSinceStartup - start < 66f / 8f + 8f)
        {
            yield return null;
        }

        Assert.IsTrue(credits.Started, "the credits start after the finale");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RollListsEveryCredit()
    {
        yield return Load("Island5");
        CreditsRoll credits = FindCredits();
        credits.Build();
        string built = credits.BuiltText;
        List<CreditEntry> entries = CreditsFormat.Parse(Resources.Load<TextAsset>("Credits").text);
        string repoRaw = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", "CREDITS.txt"));
        Assert.AreEqual(CreditsFormat.Parse(repoRaw).Count, entries.Count, "every entry of CREDITS.txt");
        Assert.Greater(entries.Count, 0);
        for (int i = 0; i < entries.Count; i++)
        {
            StringAssert.Contains(entries[i].title, built, "title " + i);
            StringAssert.Contains(entries[i].author, built, "author of " + entries[i].title);
            StringAssert.Contains(entries[i].heading, built, "heading of " + entries[i].title);
        }

        StringAssert.Contains("Lantern Keeper", built);
        StringAssert.Contains("Made by " + Application.companyName, built);
        StringAssert.Contains("Made with Unity", built);
        StringAssert.Contains("Thank you for keeping the light.", built);
        Assert.Less(built.IndexOf("Made by ", StringComparison.Ordinal), built.IndexOf(entries[0].title, StringComparison.Ordinal));
        Assert.Less(built.IndexOf(entries[entries.Count - 1].title, StringComparison.Ordinal), built.IndexOf("Made with Unity", StringComparison.Ordinal));
        Assert.Less(built.IndexOf("Made with Unity", StringComparison.Ordinal), built.IndexOf("Thank you for keeping the light.", StringComparison.Ordinal));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator HeldKeyDoesNotSkipCredits()
    {
        yield return Load("Island5");
        CreditsRoll credits = FindCredits();
        bool held = true;
        credits.InputHeld = () => held;
        credits.Begin();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 2f)
        {
            yield return null;
        }

        Assert.IsTrue(credits.Rolling, "still rolling after 2 s with a key held from before the start");
        Assert.IsFalse(credits.Exiting);
        Assert.AreEqual("Island5", SceneManager.GetActiveScene().name);
        held = false;
        yield return null;
        yield return null;
        Assert.IsTrue(credits.Rolling, "releasing does not skip");
        held = true;
        yield return null;
        yield return null;
        Assert.IsTrue(credits.Exiting, "a press after the release skips");
        yield return WaitForMenu(8f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RollIsShort()
    {
        yield return Load("Island5");
        CreditsRoll credits = FindCredits();
        credits.Build();
        Assert.LessOrEqual(credits.Seconds, 75f, "the roll lasts 75 s or less (" + credits.Seconds.ToString("F1") + " s)");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator KeyPressedBeforeArmAndHeldAcrossDoesNotSkip()
    {
        yield return Load("Island5");
        CreditsRoll credits = FindCredits();
        bool held = false;
        credits.InputHeld = () => held;
        credits.Begin();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 0.5f)
        {
            yield return null;
        }

        held = true;
        yield return WaitArmed(credits, 10f);

        float hintWait = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - hintWait < 0.6f)
        {
            yield return null;
        }

        Assert.IsTrue(credits.Armed, "armed after 1 s");
        Assert.IsFalse(credits.Exiting, "a key pressed before arming and held across it does not skip");
        Assert.IsTrue(credits.HintAlpha > 0.05f, "the skip hint fades in once armed");
        held = false;
        yield return null;
        yield return null;
        Assert.IsFalse(credits.Exiting);
        held = true;
        yield return null;
        yield return null;
        Assert.IsTrue(credits.Exiting, "a fresh press skips");
        yield return WaitForMenu(8f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator MenuTrackFadesInUnderTheRollAfterTheCue()
    {
        yield return WinIsland5();
        MonoBehaviour director = (MonoBehaviour)Find("MusicDirector");
        AudioSource cue = (AudioSource)Get(director, "FinaleSource");
        ScriptableObject library = Resources.Load<ScriptableObject>("MusicLibrary");
        AudioClip menuTrack = (AudioClip)Get(library, "menuTrack");
        Component player = (Component)Find("MusicPlayer");
        AudioSource[] players = { (AudioSource)Get(player, "FirstMusic"), (AudioSource)Get(player, "SecondMusic") };
        Assert.IsFalse(players[0].isPlaying && players[0].clip == menuTrack || players[1].isPlaying && players[1].clip == menuTrack, "no menu track while the cue plays");

        cue.Stop();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 2.6f)
        {
            yield return null;
        }

        AudioSource menu = players[0].isPlaying && players[0].clip == menuTrack ? players[0] : players[1];
        Assert.IsTrue(menu.isPlaying && menu.clip == menuTrack, "the menu track plays under the roll once the cue is over");
        Assert.Greater(menu.volume, 0.1f, "and has faded in");
        float timeBefore = menu.time;

        CreditsRoll credits = FindCredits();
        bool held = false;
        credits.InputHeld = () => held;
        yield return null;
        held = true;
        yield return WaitForMenu(10f);
        float wait = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - wait < 1f)
        {
            yield return null;
        }

        int playing = 0;
        AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].isPlaying && sources[i].clip == menuTrack)
            {
                playing++;
                Assert.AreSame(menu, sources[i], "the same source keeps playing: no restart");
                Assert.Greater(sources[i].time, timeBefore, "the track did not restart");
            }
        }

        Assert.AreEqual(1, playing, "exactly one menu track in the menu");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RestartIsBlockedAfterTheIsland5Win()
    {
        yield return WinIsland5();
        Component manager = (Component)Find("GameManager");
        Assert.IsTrue((bool)Get(manager, "Won"));
        Assert.IsFalse((bool)Get(manager, "RestartAllowed"), "no restart during the credits roll");
        Assert.IsFalse((bool)Call(manager, "DebugPressRestart"), "R does nothing during the credits roll");
        yield return null;
        Assert.AreEqual("Island5", SceneManager.GetActiveScene().name, "still in Island5");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator Island1WinIsUnchangedByTheFinaleGuard()
    {
        yield return Load("Island1");
        Component manager = (Component)Find("GameManager");
        Assert.IsFalse((bool)Get(manager, "HasFinale"), "Island 1 has no finale");
        Call(manager, "Win");
        float start = Time.realtimeSinceStartup;
        while (!(bool)Get(manager, "Won") && Time.realtimeSinceStartup - start < 15f)
        {
            yield return null;
        }

        Assert.IsTrue((bool)Get(manager, "Won"), "Island 1 win completed");
        Assert.IsFalse((bool)Get(manager, "DawnPlaying"), "the dawn flag is released");
        // Unchanged from before the finale guard: restart is only unlocked by a loss (restartUnlockTime stays infinite after a win), so R is not offered here.
        Assert.IsFalse((bool)Get(manager, "RestartAllowed"), "Island 1 win panel offers no R restart, as before");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator Island1RestartStillWorksOnTheLosePanel()
    {
        yield return Load("Island1");
        Component manager = (Component)Find("GameManager");
        Call(manager, "Lose");
        float start = Time.realtimeSinceStartup;
        while (!(bool)Get(manager, "RestartAllowed") && Time.realtimeSinceStartup - start < 15f)
        {
            yield return null;
        }

        Assert.IsTrue((bool)Get(manager, "RestartAllowed"), "R still restarts on the Island 1 result panel (guard is Island 5 only)");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator DisabledFinaleFallsBackToTheDawn()
    {
        yield return Load("Island5");
        Component manager = (Component)Find("GameManager");
        ((Behaviour)Find("FinaleSequence")).enabled = false;
        Call(manager, "Win");
        float start = Time.realtimeSinceStartup;
        while (!(bool)Get(manager, "Won") && Time.realtimeSinceStartup - start < 15f)
        {
            yield return null;
        }

        Assert.IsTrue((bool)Get(manager, "Won"), "the win completes through the dawn path");
        Assert.IsFalse((bool)Get(manager, "DawnPlaying"), "DawnPlaying is released");
        // The fallback follows the normal win rule (Islands 1-4): the finale does not run, so the finale clause of RestartAllowed does not apply,
        // and the win panel replaces the credits. After a win R is not offered (restart unlocks only on a loss), the same as Island 1.
        Assert.IsFalse((bool)Get(manager, "FinaleRuns"), "an inactive finale does not count as running");
        Assert.IsTrue((bool)Get(manager, "HasFinale"), "the finale object is still wired");
        Assert.IsFalse((bool)Get(manager, "RestartAllowed"), "the same R rule as the Island 1 win panel");
    }

    // The roll clamps its frame time, so a fixed real-time wait can end before it is armed: wait for the arm itself.
    static IEnumerator WaitArmed(CreditsRoll credits, float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (!credits.Armed && Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }

        Assert.IsTrue(credits.Armed, "the roll armed");
    }

    static IEnumerator WaitFrames(Func<bool> done, int maxFrames)
    {
        for (int i = 0; i < maxFrames && !done(); i++)
        {
            yield return null;
        }
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator GamepadButtonHeldAcrossTheArmDoesNotSkipButAFreshPressDoes()
    {
        yield return Load("Island5");
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            CreditsRoll credits = FindCredits();
            // No manual InputSystem.Update: events are queued, the player loop processes them, and each state is awaited over frames.
            InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState { buttons = 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.South });
            yield return WaitFrames(() => pad.buttonSouth.isPressed, 30);
            Assert.IsTrue(pad.buttonSouth.isPressed, "the held press reached the device");
            credits.Begin();
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.6f)
            {
                yield return null;
            }

            Assert.IsFalse(credits.Exiting, "a pad button held across the arm does not skip");
            InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState());
            yield return WaitFrames(() => !pad.buttonSouth.isPressed, 30);
            Assert.IsFalse(pad.buttonSouth.isPressed, "the release reached the device");
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.IsFalse(credits.Exiting, "releasing does not skip");
            yield return WaitArmed(credits, 10f);
            InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState { buttons = 1u << (int)UnityEngine.InputSystem.LowLevel.GamepadButton.East });
            yield return WaitFrames(() => credits.Exiting, 30);
            Assert.IsTrue(credits.Exiting, "a fresh pad press skips");
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
        }

        yield return WaitForMenu(8f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator TouchscreenTapSkipsAndHintSaysTapToSkip()
    {
        yield return Load("Island5");
        // Game view focus must not decide whether the simulated touchscreen delivers events (as in TouchControlsTest).
        InputSettings.BackgroundBehavior savedBackground = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode savedEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Touchscreen screen = InputSystem.AddDevice<Touchscreen>();
        TouchMode.SetActiveForTests(true);
        try
        {
            CreditsRoll credits = FindCredits();
            credits.Begin();
            yield return WaitArmed(credits, 10f);
            TMPro.TMP_Text hint = null;
            foreach (TMPro.TMP_Text label in credits.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (label.text.Contains("skip"))
                {
                    hint = label;
                }
            }

            Assert.IsNotNull(hint, "the skip hint label exists");
            Assert.AreEqual("Tap to skip", hint.text, "touch mode words the hint as a tap");
            TouchMode.SetActiveForTests(false);
            yield return null;
            Assert.AreEqual("Press any key to skip", hint.text, "and it follows the mode back to the keyboard wording");
            TouchMode.SetActiveForTests(true);
            yield return null;
            Assert.IsFalse(credits.Exiting, "nothing skipped yet");
            InputSystem.QueueStateEvent(screen, new UnityEngine.InputSystem.LowLevel.TouchState
            {
                touchId = 1,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                startPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            });
            yield return WaitFrames(() => credits.Exiting, 30);
            Assert.IsTrue(credits.Exiting, "a touchscreen tap skips the credits");
        }
        finally
        {
            TouchMode.SetActiveForTests(false);
            InputSystem.RemoveDevice(screen);
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditor;
        }

        yield return WaitForMenu(8f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator SkipReturnsToMenu()
    {
        yield return Load("Island5");
        CreditsRoll credits = FindCredits();
        bool held = false;
        credits.InputHeld = () => held;
        credits.Begin();
        held = true;
        yield return null;
        yield return null;
        Assert.IsFalse(credits.Exiting, "a press in the first second is ignored");
        held = false;
        yield return WaitArmed(credits, 10f);

        held = true;
        yield return null;
        yield return null;
        Assert.IsTrue(credits.Exiting, "a press after 1.1 s skips");
        yield return WaitForMenu(8f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator RollEndsAtTheLastLineAndLoadsMenu()
    {
        yield return Load("Island5");
        CreditsRoll credits = FindCredits();
        credits.InputHeld = () => false;
        CreditsRoll.TestSpeed = 400f;
        credits.Begin();
        float length = credits.ScrollLength;
        Assert.Greater(length, 3000f, "the roll is long (px)");
        yield return WaitForMenu(length / (CreditsRoll.PixelsPerSecond * 400f) * 2f + 10f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleRecordsBestAndWon()
    {
        PlayerPrefs.DeleteKey("LanternKeeperWon_island5");
        PlayerPrefs.DeleteKey(BestKey);
        yield return WinIsland5();
        Assert.AreEqual(1, wonCount, "WonGame fires once");
        Assert.IsTrue((bool)CallStatic("GameSettings", "HasWon", "island5"), "Island 5 is marked won");
        float best = (float)CallStatic("GameSettings", "GetBestTime", "island5");
        Assert.Greater(best, 0f, "a best time is stored");
        ResultPanel result = UnityEngine.Object.FindAnyObjectByType<ResultPanel>(FindObjectsInactive.Include);
        Assert.IsFalse(result != null && result.IsShowing, "the credits replace the result panel");

        CreditsRoll credits = FindCredits();
        credits.InputHeld = () => false;
        CreditsRoll.TestSpeed = 400f;
        yield return WaitForMenu(30f);

        // A second win also completes.
        CreditsRoll.TestSpeed = 1f;
        wonCount = 0;
        yield return WinIsland5();
        Assert.AreEqual(1, wonCount, "the second win fires WonGame");
        Assert.IsTrue((bool)CallStatic("GameSettings", "HasWon", "island5"));
        Assert.Greater((float)CallStatic("GameSettings", "GetBestTime", "island5"), 0f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator ReturnToMenuStopsFinaleCue()
    {
        yield return WinIsland5();
        MonoBehaviour director = (MonoBehaviour)Find("MusicDirector");
        Assert.IsTrue((bool)Get(director, "FinalePlaying"), "the finale cue started");
        AudioSource cue = (AudioSource)Get(director, "FinaleSource");
        AudioClip finaleClip = cue.clip;
        Assert.IsNotNull(finaleClip, "finale cue clip");
        Assert.IsTrue(cue.isPlaying, "the cue is still playing when the credits start");

        CreditsRoll credits = FindCredits();
        bool held = false;
        credits.InputHeld = () => held;
        yield return WaitArmed(credits, 10f);

        held = true;
        yield return WaitForMenu(10f);
        ScriptableObject library = Resources.Load<ScriptableObject>("MusicLibrary");
        AudioClip menuTrack = (AudioClip)Get(library, "menuTrack");
        Assert.IsNotNull(menuTrack, "menu track");

        // The menu track fades in over 3 s.
        float wait = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - wait < 1f)
        {
            yield return null;
        }

        AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int menuPlaying = 0;
        for (int i = 0; i < sources.Length; i++)
        {
            Assert.IsFalse(sources[i].isPlaying && sources[i].clip == finaleClip, "no AudioSource plays the finale cue in the menu (" + sources[i].name + ")");
            if (sources[i].isPlaying && sources[i].clip == menuTrack)
            {
                menuPlaying++;
            }
        }

        Assert.AreEqual(1, menuPlaying, "exactly one source plays the menu track");
    }
}
}
