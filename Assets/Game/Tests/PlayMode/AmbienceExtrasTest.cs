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
// The island 2 owl: scheduled one-shots that never play on other islands, while paused, or after the round is over.
public class AmbienceExtrasTest
{
    const string Owl = "Ambience.Owl";
    static readonly string[] IntroKeys = { "LanternKeeperIntro_island1", "LanternKeeperIntro_island2" };

    readonly List<string> played = new List<string>();
    readonly Dictionary<string, int> savedIntro = new Dictionary<string, int>();
    Action<string> spy;

    [SetUp]
    public void SetUp()
    {
        for (int i = 0; i < IntroKeys.Length; i++)
        {
            savedIntro[IntroKeys[i]] = PlayerPrefs.HasKey(IntroKeys[i]) ? PlayerPrefs.GetInt(IntroKeys[i]) : -1;
            PlayerPrefs.SetInt(IntroKeys[i], 1);
        }

        played.Clear();
        spy = cue => played.Add(cue);
        GameType("AudioManager").GetEvent("CuePlayed", BindingFlags.Public | BindingFlags.Static).AddEventHandler(null, spy);
    }

    [TearDown]
    public void TearDown()
    {
        GameType("AudioManager").GetEvent("CuePlayed", BindingFlags.Public | BindingFlags.Static).RemoveEventHandler(null, spy);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        foreach (KeyValuePair<string, int> pair in savedIntro)
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

    int Owls()
    {
        int n = 0;
        for (int i = 0; i < played.Count; i++)
        {
            if (played[i] == Owl)
            {
                n++;
            }
        }

        return n;
    }

    static void Configure(Component extras, float first, float min, float max)
    {
        extras.GetType().GetMethod("Configure").Invoke(extras, new object[] { first, min, max });
    }

    static Component Extras()
    {
        return (Component)UnityEngine.Object.FindAnyObjectByType(GameType("AmbienceExtras"));
    }

    static float Field(Component extras, string name)
    {
        return (float)extras.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(extras);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator OwlPlaysOnIsland2()
    {
        yield return Load("Island2");
        Component extras = Extras();
        Assert.IsNotNull(extras, "Island2 carries the ambience extras");
        Assert.GreaterOrEqual(Field(extras, "firstDelay"), 8f, "first call at least 8 s after load");
        Assert.AreEqual(12f, Field(extras, "minInterval"), 0.001f);
        Assert.AreEqual(30f, Field(extras, "maxInterval"), 0.001f);
        Assert.AreEqual(0, Owls(), "no owl right after load");

        Configure(extras, 0.2f, 0.2f, 0.3f);
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Owls() < 2 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.GreaterOrEqual(Owls(), 2, "the owl calls repeatedly on Island2");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator OwlNeverPlaysOnIsland1()
    {
        yield return Load("Island1");
        Assert.IsNull(Extras(), "Island1 has no ambience extras");
        // Even a component forced onto Island1 stays quiet: the owl belongs to Island2.
        GameObject host = new GameObject("ForcedExtras");
        Component extras = host.AddComponent(GameType("AmbienceExtras"));
        Configure(extras, 0.1f, 0.1f, 0.2f);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.AreEqual(0, Owls(), "no owl on Island1");
        UnityEngine.Object.Destroy(host);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator OwlWaitsWhilePausedAndStopsWhenRoundIsOver()
    {
        yield return Load("Island2");
        Component extras = Extras();
        Assert.IsNotNull(extras);
        object manager = GameType("GameManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Type gm = manager.GetType();
        gm.GetMethod("Pause").Invoke(manager, null);
        Assert.IsTrue((bool)gm.GetProperty("IsPaused").GetValue(manager), "paused");
        Configure(extras, 0.1f, 0.1f, 0.2f);
        yield return new WaitForSecondsRealtime(1f);
        Assert.AreEqual(0, Owls(), "no owl while paused");

        gm.GetMethod("Resume").Invoke(manager, null);
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Owls() < 1 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.GreaterOrEqual(Owls(), 1, "the owl resumes after the pause");

        gm.GetMethod("Lose").Invoke(manager, null);
        Assert.IsTrue((bool)gm.GetProperty("IsRoundOver").GetValue(manager) || (bool)gm.GetProperty("IsDying").GetValue(manager), "round ending");
        played.Clear();
        yield return new WaitForSecondsRealtime(1f);
        Assert.AreEqual(0, Owls(), "no owl after the round is over");
    }
}
}
