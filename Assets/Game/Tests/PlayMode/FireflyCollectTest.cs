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
// Collecting a firefly on Island1: fuel on the trigger frame, the motes stream into the lantern, two linger, then everything
// hides until Respawn fades the swarm back in. Game types are reached by reflection (this assembly cannot reference Assembly-CSharp).
public class FireflyCollectTest
{
    const string IntroKey = "LanternKeeperIntro_island1";
    const string GraphicsKey = "LanternKeeperGraphics";
    const float Near = 0.4f;

    bool hadIntroKey;
    int previousIntro;
    int previousGraphics;
    bool previousReduce;
    readonly List<string> errors = new List<string>();

    [SetUp]
    public void SetUp()
    {
        hadIntroKey = PlayerPrefs.HasKey(IntroKey);
        previousIntro = PlayerPrefs.GetInt(IntroKey, 0);
        previousGraphics = PlayerPrefs.GetInt(GraphicsKey, 2);
        PlayerPrefs.SetInt(IntroKey, 1);
        previousReduce = UserSettings.ReduceFlashing;
        errors.Clear();
        Application.logMessageReceived += OnLog;
    }

    [TearDown]
    public void TearDown()
    {
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        UserSettings.ReduceFlashing = previousReduce;
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

    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition);
        }
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

    static T Get<T>(object target, string property)
    {
        return (T)target.GetType().GetProperty(property).GetValue(target);
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

    static IEnumerator Wait(float seconds)
    {
        float start = Time.time;
        while (Time.time - start < seconds)
        {
            yield return null;
        }
    }

    sealed class Scene
    {
        public Component lantern;
        public Component flame;
        public Collider player;
        public Component[] fireflies;
    }

    // Loads Island1 at Ultra, drains the lantern to 30%, and finds the player, lantern, flame and fireflies.
    static IEnumerator Setup(Scene scene)
    {
        yield return LoadIsland1();
        SetQuality(3);
        yield return null;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Assert.IsNotNull(player, "player");
        scene.player = player.GetComponent<Collider>();
        Assert.IsNotNull(scene.player, "player collider");
        scene.lantern = player.GetComponentInChildren(GameType("Lantern"));
        Assert.IsNotNull(scene.lantern, "lantern");
        scene.flame = scene.lantern.GetComponent(GameType("LanternFlame"));
        Assert.IsNotNull(scene.flame, "LanternFlame");
        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(GameType("Firefly"), FindObjectsSortMode.None);
        scene.fireflies = new Component[found.Length];
        for (int i = 0; i < found.Length; i++)
        {
            scene.fireflies[i] = (Component)found[i];
        }

        Assert.GreaterOrEqual(scene.fireflies.Length, 2, "at least two fireflies on Island1");
        float max = Get<float>(scene.lantern, "MaxFuel");
        float fuel = Get<float>(scene.lantern, "Fuel");
        MethodInfo spend = GameType("Lantern").GetMethod("TrySpend");
        spend.Invoke(scene.lantern, new object[] { fuel - max * 0.3f });
        Assert.AreEqual(max * 0.3f, Get<float>(scene.lantern, "Fuel"), 0.5f, "fuel at 30%");
    }

    static void Collect(Component firefly, Collider player)
    {
        firefly.GetType().GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(firefly, new object[] { player });
    }

    static Component SwarmOf(Component firefly)
    {
        return firefly.GetComponentInChildren(GameType("FireflySwarm"), true);
    }

    static float Refill(Component firefly)
    {
        return (float)firefly.GetType().GetField("refillAmount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(firefly);
    }

    static List<Renderer> EnabledMotes(Component swarm)
    {
        Transform[] motes = Get<Transform[]>(swarm, "Motes");
        List<Renderer> enabled = new List<Renderer>();
        for (int i = 0; i < motes.Length; i++)
        {
            Renderer renderer = motes[i].GetComponent<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                enabled.Add(renderer);
            }
        }

        return enabled;
    }

    static Transform GlowPoint(Scene scene)
    {
        return Get<Transform>(scene.flame, "GlowPoint");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator CollectAddsFuelSameFrame()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        Component firefly = scene.fireflies[0];
        float before = Get<float>(scene.lantern, "Fuel");
        Collect(firefly, scene.player);
        float after = Get<float>(scene.lantern, "Fuel");
        Assert.AreEqual(Refill(firefly), after - before, 0.001f, "the fuel delta on the trigger frame is the refill amount");
        Assert.AreEqual(20f, Refill(firefly), 0.001f);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MotesReachLanternByHalfSecond()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        Component swarm = SwarmOf(scene.fireflies[0]);
        Collect(scene.fireflies[0], scene.player);
        Assert.IsTrue(Get<bool>(swarm, "Streaming"), "streaming right after collect");
        // Poll until the streaming motes are hidden (the stream ends at 0.5 s), capped at 0.8 s.
        float start = Time.time;
        yield return Wait(0.65f);
        while (EnabledMotes(swarm).Count > 2 && Time.time - start < 0.8f)
        {
            yield return null;
        }

        List<Renderer> lingering = EnabledMotes(swarm);
        Assert.AreEqual(2, lingering.Count, "two motes linger on Ultra, the rest are hidden");
        Vector3 glow = GlowPoint(scene).position;
        for (int i = 0; i < lingering.Count; i++)
        {
            Assert.Less(Vector3.Distance(lingering[i].transform.position, glow), Near, "lingering mote near the flame");
        }

        Assert.AreEqual(0, errors.Count, string.Join("; ", errors));
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator OneMoteLingersOnLow()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        SetQuality(0);
        yield return null;
        Component swarm = SwarmOf(scene.fireflies[0]);
        Collect(scene.fireflies[0], scene.player);
        float start = Time.time;
        yield return Wait(0.65f);
        while (EnabledMotes(swarm).Count > 1 && Time.time - start < 0.8f)
        {
            yield return null;
        }

        Assert.AreEqual(1, EnabledMotes(swarm).Count, "one mote lingers on Low");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator StreamFollowsMovingLantern()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        Component swarm = SwarmOf(scene.fireflies[0]);
        Collect(scene.fireflies[0], scene.player);
        yield return Wait(0.15f);
        CharacterController controller = scene.player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        scene.player.transform.position += new Vector3(3f, 0f, 0f);
        if (controller != null)
        {
            controller.enabled = true;
        }

        yield return Wait(0.4f);
        List<Renderer> lingering = EnabledMotes(swarm);
        Assert.AreEqual(2, lingering.Count, "two motes linger");
        Vector3 glow = GlowPoint(scene).position;
        for (int i = 0; i < lingering.Count; i++)
        {
            Assert.Less(Vector3.Distance(lingering[i].transform.position, glow), Near, "lingering mote follows the moved lantern");
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator HiddenUntilRespawn()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        Component firefly = scene.fireflies[0];
        Component swarm = SwarmOf(firefly);
        Collect(firefly, scene.player);
        // The stream ends at 2 s. Allow a few slow editor frames of slack, then check the end state.
        float start = Time.time;
        yield return Wait(1.9f);
        Assert.IsTrue(Get<bool>(swarm, "Streaming"), "still lingering at 1.9 s");
        while (Get<bool>(swarm, "Streaming") && Time.time - start < 2.3f)
        {
            yield return null;
        }

        Assert.AreEqual(0, EnabledMotes(swarm).Count, "no mote renderer is enabled after the linger");
        Assert.IsFalse(Get<bool>(swarm, "Streaming"), "stream finished");
        Assert.IsFalse(firefly.GetComponent<Collider>().enabled, "collider disabled while collected");

        swarm.GetType().GetMethod("Respawn").Invoke(swarm, null);
        yield return Wait(1.05f);
        Assert.AreEqual(Get<int>(swarm, "VisibleMotes"), EnabledMotes(swarm).Count, "all visible motes are shown after the fade-in");
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer renderer in EnabledMotes(swarm))
        {
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(1f, block.GetFloat("_Fade"), 0.001f, "fade-in complete");
            Assert.AreEqual(0f, block.GetFloat("_Stream"), 0.001f, "drifting again");
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator LingerSurvivesLanternDestroyed()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        Component swarm = SwarmOf(scene.fireflies[0]);
        Collect(scene.fireflies[0], scene.player);
        yield return Wait(0.6f);
        errors.Clear();
        UnityEngine.Object.Destroy(scene.lantern.gameObject);
        yield return Wait(1.6f);
        Assert.AreEqual(0, errors.Count, "no errors after the lantern is destroyed: " + string.Join("; ", errors));
        Assert.IsFalse(Get<bool>(swarm, "Streaming"), "stream finished");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator TwoCollectsStreamIndependently()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        float before = Get<float>(scene.lantern, "Fuel");
        Collect(scene.fireflies[0], scene.player);
        yield return null;
        Collect(scene.fireflies[1], scene.player);
        float after = Get<float>(scene.lantern, "Fuel");
        Assert.AreEqual(Refill(scene.fireflies[0]) + Refill(scene.fireflies[1]), after - before, 1f, "fuel rises by both refills");
        Assert.IsTrue(Get<bool>(SwarmOf(scene.fireflies[0]), "Streaming"), "first swarm streams");
        Assert.IsTrue(Get<bool>(SwarmOf(scene.fireflies[1]), "Streaming"), "second swarm streams");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator KickHalvedWithReduceFlashing()
    {
        Scene scene = new Scene();
        yield return Setup(scene);
        UserSettings.ReduceFlashing = true;
        float reducedPeak = 0f;
        Collect(scene.fireflies[0], scene.player);
        float start = Time.time;
        while (Time.time - start < 0.9f)
        {
            reducedPeak = Mathf.Max(reducedPeak, Get<float>(scene.flame, "KickLevel"));
            yield return null;
        }

        yield return Wait(1.3f);
        UserSettings.ReduceFlashing = false;
        float normalPeak = 0f;
        Collect(scene.fireflies[1], scene.player);
        start = Time.time;
        while (Time.time - start < 0.9f)
        {
            normalPeak = Mathf.Max(normalPeak, Get<float>(scene.flame, "KickLevel"));
            yield return null;
        }

        Assert.LessOrEqual(reducedPeak, 0.5f + 0.001f, "Reduce flashing caps the boost at 0.5");
        Assert.Greater(reducedPeak, 0.2f, "the boost still happens");
        Assert.Greater(normalPeak, 0.7f, "normal boost is near 1");
        Assert.LessOrEqual(normalPeak, 1.001f);
        Assert.Greater(normalPeak, reducedPeak * 1.5f, "the boost is clearly larger without Reduce flashing");
    }
}
}
