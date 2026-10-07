using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// While moths drain the lantern, LowFuelFX pulses the screen edge in drain violet about once a second.
// Game types are reached by reflection (this assembly can't reference Assembly-CSharp).
public class MothPulseTest
{
    // Mirrors GameSettings.IntroKey; the intro is marked seen so Island1 runs instead of holding the first-visit card.
    const string IntroKey = "LanternKeeperIntro_island1";

    bool hadIntroKey;
    int previousIntro;

    [SetUp]
    public void SetUp()
    {
        hadIntroKey = PlayerPrefs.HasKey(IntroKey);
        previousIntro = PlayerPrefs.GetInt(IntroKey, 0);
        PlayerPrefs.SetInt(IntroKey, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (hadIntroKey)
        {
            PlayerPrefs.SetInt(IntroKey, previousIntro);
        }
        else
        {
            PlayerPrefs.DeleteKey(IntroKey);
        }
        PlayerPrefs.Save();
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

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static UnityEngine.Object Find(string typeName)
    {
        UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(GameType(typeName), FindObjectsInactive.Include);
        Assert.IsNotNull(found, typeName + " in scene");
        return found;
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
    public IEnumerator MothDrainPulsesScreenEdge()
    {
        yield return LoadIsland1();
        UnityEngine.Object fx = Find("LowFuelFX");
        UnityEngine.Object lantern = Find("Lantern");
        // The island's own moths would drain on their own; remove them so only the test moth counts.
        UnityEngine.Object[] wild = UnityEngine.Object.FindObjectsByType(GameType("Moth"), FindObjectsSortMode.None);
        for (int i = 0; i < wild.Length; i++)
        {
            UnityEngine.Object.Destroy(((Component)wild[i]).gameObject);
        }
        yield return Frames(3);
        GameObject holder = new GameObject("TestMoth");
        Component moth = holder.AddComponent(GameType("Moth"));
        ((Behaviour)moth).enabled = false; // the moth's own AI would clear the modifier; this one only has to count
        int before = Get<int>(fx, "MothPulsesRequested");
        Assert.AreEqual(0, before, "no pulse before a moth drains");
        Call(lantern, "SetDrainModifier", moth, 2f);
        float peak = 0f;
        float waited = 0f;
        while (waited < 2.5f)
        {
            waited += Time.deltaTime;
            peak = Mathf.Max(peak, Get<float>(fx, "EdgePulseAmount"));
            yield return null;
        }
        int during = Get<int>(fx, "MothPulsesRequested");
        Assert.GreaterOrEqual(during, 2, "about one pulse a second while a moth drains");
        Assert.Greater(peak, 0.1f, "the edge pulse is requested");

        UnityEngine.Object.Destroy(holder);
        yield return Frames(5);
        int settled = Get<int>(fx, "MothPulsesRequested");
        waited = 0f;
        while (waited < 2.5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        Assert.AreEqual(0, Get<int>(lantern, "MothsDraining"), "no moth drains once the test moth is gone");
        Assert.AreEqual(settled, Get<int>(fx, "MothPulsesRequested"), "no new pulse once the moth is gone");
    }
}
}
