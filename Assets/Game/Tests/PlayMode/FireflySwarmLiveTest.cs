using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The firefly swarm on Island1: the motes blink, the Glow light breathes, and Low hides the extra motes.
// Game types are reached by reflection (this assembly cannot reference Assembly-CSharp).
public class FireflySwarmLiveTest
{
    const string IntroKey = "LanternKeeperIntro_island1";
    const string GraphicsKey = "LanternKeeperGraphics";

    bool hadIntroKey;
    int previousIntro;
    int previousGraphics;

    [SetUp]
    public void SetUp()
    {
        hadIntroKey = PlayerPrefs.HasKey(IntroKey);
        previousIntro = PlayerPrefs.GetInt(IntroKey, 0);
        previousGraphics = PlayerPrefs.GetInt(GraphicsKey, 2);
        PlayerPrefs.SetInt(IntroKey, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        SetQuality(previousGraphics);
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

    static void SetQuality(int level)
    {
        Type quality = GameType("GraphicsQuality");
        Type levelType = GameType("GraphicsLevel");
        quality.GetMethod("Set").Invoke(null, new object[] { Enum.ToObject(levelType, level) });
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

    static Component FirstSwarm()
    {
        UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(GameType("FireflySwarm"), FindObjectsInactive.Include);
        Assert.IsNotNull(found, "a FireflySwarm on Island1");
        return (Component)found;
    }

    static T Get<T>(object target, string property)
    {
        return (T)target.GetType().GetProperty(property).GetValue(target);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator SwarmBlinksAndLightBreathes()
    {
        yield return LoadIsland1();
        Component swarm = FirstSwarm();
        Light glow = swarm.transform.parent.Find("Glow").GetComponent<Light>();
        Assert.IsNotNull(glow, "Glow light");
        yield return null;

        HashSet<float> fractions = new HashSet<float>();
        float minIntensity = float.MaxValue;
        float maxIntensity = 0f;
        float elapsed = 0f;
        while (elapsed < 3f)
        {
            fractions.Add(Get<float>(swarm, "LitFraction"));
            minIntensity = Mathf.Min(minIntensity, glow.intensity);
            maxIntensity = Mathf.Max(maxIntensity, glow.intensity);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.GreaterOrEqual(fractions.Count, 2, "LitFraction takes at least two distinct values over 3 s");
        Assert.Greater(maxIntensity - minIntensity, 0.001f, "the Glow intensity varies");
        // The prefab base is 1.3: the breathing factor stays within 0.6 to 1.0 of it.
        float baseIntensity = 1.3f;
        Assert.GreaterOrEqual(minIntensity, baseIntensity * 0.6f - 0.01f);
        Assert.LessOrEqual(maxIntensity, baseIntensity + 0.01f);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator LowPresetHidesExtraMotes()
    {
        yield return LoadIsland1();
        Component swarm = FirstSwarm();
        SetQuality(3);
        yield return null;
        Assert.AreEqual(6, Get<int>(swarm, "VisibleMotes"), "Ultra shows six motes");

        SetQuality(0);
        yield return null;
        yield return null;
        Assert.AreEqual(4, Get<int>(swarm, "VisibleMotes"), "Low shows four motes");
        Assert.AreEqual(1f, Shader.GetGlobalFloat("_LKFireflyLow"), "the Low global is published");

        SetQuality(previousGraphics);
        yield return null;
        Assert.AreEqual(previousGraphics == 0 ? 4 : 6, Get<int>(swarm, "VisibleMotes"), "restored");
    }
}
}
