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
// The Island 5 finale cinematic, live. Game types are reached by reflection (this assembly can't reference Assembly-CSharp).
// The finale runs on unscaled time; FinaleSequence.TestSpeed (8) makes the 52 s sequence take 6.5 s.
public class FinaleSequenceTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    static readonly string[] IntKeys =
    {
        "LanternKeeperIntro_island1", "LanternKeeperIntro_island2", "LanternKeeperIntro_island3", "LanternKeeperIntro_island4", "LanternKeeperIntro_island5",
        "LanternKeeperWon_island1", "LanternKeeperWon_island2", "LanternKeeperWon_island3", "LanternKeeperWon_island4", "LanternKeeperWon_island5"
    };
    static readonly string[] FloatKeys = { "LanternKeeperBest_island1", "LanternKeeperBest_island2", "LanternKeeperBest_island3", "LanternKeeperBest_island4", "LanternKeeperBest_island5" };

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
    int savedGraphics;
    float previousScale;
    int wonCount;
    float wonAt = -1f;

    public void OnWon()
    {
        wonCount++;
        wonAt = Time.realtimeSinceStartup;
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

    static void SetTestSpeed(float speed)
    {
        FieldInfo field = GameType("FinaleSequence").GetField("TestSpeed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "FinaleSequence.TestSpeed");
        field.SetValue(null, speed);
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
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.SetInt("LanternKeeperIntro_island" + i, 1);
        }

        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        previousScale = Time.timeScale;
        wonCount = 0;
        wonAt = -1f;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = previousScale;
        SetTestSpeed(1f);
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

        yield return null;
        yield return null;
    }

    static void Refuel()
    {
        Call(Find("Lantern"), "AddFuel", 100f);
    }

    static void LightAllBeacons()
    {
        Transform root = GameObject.Find("Beacons").transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Refuel();
            Assert.IsTrue((bool)Call(root.GetChild(i).GetComponent(GameType("Beacon")), "TryLight"), "beacon " + i);
        }
    }

    // Lights the eight beacons (which opens the summit gate) and then the great lamp.
    static void LightTheLamp()
    {
        LightAllBeacons();
        Component gate = (Component)Find("SummitGate");
        Assert.IsTrue((bool)Get(gate, "Open"), "the summit gate is open");
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

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleRunsToCompletion()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Component manager = (Component)Find("GameManager");
        Assert.IsTrue((bool)Get(manager, "HasFinale"), "Island 5 has the finale");
        Subscribe(manager);
        Component finale = (Component)Find("FinaleSequence");
        LightTheLamp();
        float start = Time.realtimeSinceStartup;
        List<int> phases = new List<int>();
        float limit = 52f / 8f + 1f;
        while (wonCount == 0 && Time.realtimeSinceStartup - start < limit + 3f)
        {
            int phase = Convert.ToInt32(Get(finale, "Phase"));
            if (phases.Count == 0 || phases[phases.Count - 1] != phase)
            {
                phases.Add(phase);
            }

            yield return null;
        }

        yield return null;
        yield return null;
        Assert.AreEqual(1, wonCount, "WonGame fires once");
        Assert.LessOrEqual(wonAt - start, limit, "WonGame within (52 / 8) + 1 s of unscaled time");
        Assert.IsTrue((bool)Get(manager, "Won"));
        Assert.IsFalse((bool)Get(manager, "DawnPlaying"), "the dawn flag is released");
        Assert.GreaterOrEqual(phases.Count, 4, "phases seen: " + string.Join(",", phases));
        for (int i = 1; i < phases.Count; i++)
        {
            Assert.Greater(phases[i], phases[i - 1], "phases in order: " + string.Join(",", phases));
        }

        Assert.AreEqual(0, phases[0], "starts at Ignition");
        Assert.IsTrue(phases.Contains(1) && phases.Contains(2) && phases.Contains(3), "BeamAndStorm, Islands and Sunrise were all seen: " + string.Join(",", phases));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleIgnoresPauseAndTimeScale()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Component manager = (Component)Find("GameManager");
        Subscribe(manager);
        LightTheLamp();
        Time.timeScale = 0f;
        float start = Time.realtimeSinceStartup;
        int tries = 0;
        while (wonCount == 0 && Time.realtimeSinceStartup - start < 52f / 8f + 4f)
        {
            Call(manager, "Pause");
            Assert.IsFalse((bool)Get(manager, "IsPaused"), "pause has no effect during the finale");
            tries++;
            yield return null;
        }

        yield return null;
        Assert.Greater(tries, 5);
        Assert.AreEqual(1, wonCount, "the finale ran to the end at timeScale 0");
        Assert.LessOrEqual(wonAt - start, 52f / 8f + 1f);
    }

    static IEnumerator ShadersFor(GraphicsLevelValue level)
    {
        PlayerPrefs.SetInt(GraphicsKey, (int)level);
        yield return Load("Island5");
        string[] roots = { "GreatLamp", "Finale" };
        int checkedRenderers = 0;
        bool storm = false;
        bool islands = false;
        for (int r = 0; r < roots.Length; r++)
        {
            GameObject root = GameObject.Find(roots[r]);
            Assert.IsNotNull(root, roots[r]);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer && renderers[i].sharedMaterial == null)
                {
                    continue;
                }

                Material material = renderers[i].sharedMaterial;
                Assert.IsNotNull(material, renderers[i].name + " has no material");
                Assert.IsNotNull(material.shader, renderers[i].name + " has no shader");
                Assert.IsTrue(material.shader.isSupported, renderers[i].name + " shader " + material.shader.name + " is not supported on " + level);
                Assert.AreNotEqual("Hidden/InternalErrorShader", material.shader.name, renderers[i].name);
                checkedRenderers++;
                storm |= renderers[i].GetComponentInParent(GameType("StormRing")) != null;
                islands |= renderers[i].GetComponentInParent(GameType("FinaleIslands")) != null;
            }
        }

        Assert.Greater(checkedRenderers, 10, "renderers checked on " + level);
        Assert.IsTrue(storm, "StormRing renderers exist");
        Assert.IsTrue(islands, "FinaleIslands renderers exist");
    }

    enum GraphicsLevelValue { Low = 0, Ultra = 3 }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleObjectsHaveShaders()
    {
        yield return ShadersFor(GraphicsLevelValue.Low);
        yield return ShadersFor(GraphicsLevelValue.Ultra);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator StormRingHasHalfTheCardsOnLow()
    {
        PlayerPrefs.SetInt(GraphicsKey, 3);
        yield return Load("Island5");
        int full = (int)Get(Find("StormRing"), "ActiveCards");
        PlayerPrefs.SetInt(GraphicsKey, 0);
        yield return Load("Island5");
        int low = (int)Get(Find("StormRing"), "ActiveCards");
        Assert.Greater(full, 0);
        Assert.AreEqual(full / 2, low, "half as many cards on Low");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleSilencesTheNearMoth()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Component lantern = (Component)Find("Lantern");
        Component spawner = (Component)Find("MothSpawner");
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Gameplay/Moth.prefab");
        Assert.IsNotNull(prefab, "Moth prefab");
        GameObject moth = UnityEngine.Object.Instantiate(prefab, lantern.transform.position + new Vector3(2f, 0.5f, 0f), Quaternion.identity);
        float waited = 0f;
        AudioSource whisper = (AudioSource)Get(spawner, "ProximitySource");
        while ((float)Get(lantern, "ProximityDim") <= 0.05f && waited < 3f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        Assert.Greater((float)Get(lantern, "ProximityDim"), 0.05f, "the moth near the keeper dims the lantern before the finale");
        Assert.IsTrue(whisper != null && whisper.isPlaying, "and the whisper plays");
        LightTheLamp();
        waited = 0f;
        while (waited < 1f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        Assert.AreEqual(0f, (float)Get(lantern, "ProximityDim"), 1e-4f, "the finale clears the proximity dim");
        Assert.IsFalse(whisper.isPlaying, "and stops the moth whisper");
        if (moth != null)
        {
            UnityEngine.Object.Destroy(moth);
        }
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator IslandsOneToFourWinUnchanged()
    {
        yield return Load("Island1");
        Component manager = (Component)Find("GameManager");
        Assert.IsFalse((bool)Get(manager, "HasFinale"), "Island 1 has no finale");
        Assert.IsNull(Find("FinaleSequence"));
        Subscribe(manager);
        Component dawn = (Component)Find("DawnSequence");
        Component director = (Component)Find("MusicDirector");
        int stingers = -1;
        int count = (int)Get(manager, "BeaconsToWin");
        Transform root = GameObject.Find("Beacons").transform;
        for (int i = 0; i < count; i++)
        {
            Refuel();
            Assert.IsTrue((bool)Call(root.GetChild(i).GetComponent(GameType("Beacon")), "TryLight"), "beacon " + i);
            if (i == count - 1)
            {
                // After the last beacon's own stinger: what follows is the win stinger.
                stingers = (int)Get(director, "StingerRequests");
            }

            if (i < count - 1)
            {
                yield return null;
            }
        }

        yield return null;
        Assert.IsTrue((bool)Get(manager, "IsRoundOver"));
        Assert.IsTrue((bool)Get(manager, "DawnPlaying"), "the scaled dawn plays");
        Assert.IsTrue((bool)Get(dawn, "IsPlaying"));
        float waited = 0f;
        while (wonCount == 0 && waited < 20f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        Assert.AreEqual(1, wonCount, "Island 1 still wins after its 6 s dawn");
        Assert.AreEqual(1, (int)Get(director, "StingerRequests") - stingers, "the win stinger still plays on Island 1");
        Assert.GreaterOrEqual(waited, 4.5f, "the dawn kept its 6 s length");
    }
}
}
