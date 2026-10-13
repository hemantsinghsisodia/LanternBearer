using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The Island 1 win is a pre-dawn (no sun); the Island 5 finale still reaches the full sunrise. Game types by reflection.
public class PreDawnWinTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    static readonly string[] IntKeys =
    {
        "LanternKeeperIntro_island1", "LanternKeeperIntro_island5", "LanternKeeperWon_island1", "LanternKeeperWon_island5"
    };
    static readonly string[] FloatKeys = { "LanternKeeperBest_island1", "LanternKeeperBest_island5" };

    readonly System.Collections.Generic.Dictionary<string, int> savedInts = new System.Collections.Generic.Dictionary<string, int>();
    readonly System.Collections.Generic.Dictionary<string, float> savedFloats = new System.Collections.Generic.Dictionary<string, float>();
    int savedGraphics;
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
        savedFloats.Clear();
        for (int i = 0; i < IntKeys.Length; i++)
        {
            savedInts[IntKeys[i]] = PlayerPrefs.HasKey(IntKeys[i]) ? PlayerPrefs.GetInt(IntKeys[i]) : -1;
        }

        for (int i = 0; i < FloatKeys.Length; i++)
        {
            savedFloats[FloatKeys[i]] = PlayerPrefs.HasKey(FloatKeys[i]) ? PlayerPrefs.GetFloat(FloatKeys[i]) : float.NaN;
        }

        PlayerPrefs.SetInt("LanternKeeperIntro_island1", 1);
        PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        previousScale = Time.timeScale;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        LitKeyGuard.Restore();
        Time.timeScale = previousScale;
        SetTestSpeed(1f);
        foreach (System.Collections.Generic.KeyValuePair<string, int> pair in savedInts)
        {
            if (pair.Value < 0) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetInt(pair.Key, pair.Value);
        }

        foreach (System.Collections.Generic.KeyValuePair<string, float> pair in savedFloats)
        {
            if (float.IsNaN(pair.Value)) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetFloat(pair.Key, pair.Value);
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

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator PreDawnNeverShowsSun()
    {
        yield return Load("Island1");
        Component manager = (Component)Find("GameManager");
        Component dawn = (Component)Find("DawnSequence");
        Assert.AreEqual(0.25f, (float)Get(dawn, "Reach"), 1e-4f, "Island 1 reach");
        Light moon = GameObject.Find("Moonlight").GetComponent<Light>();
        float nightIntensity = moon.intensity;
        Quaternion nightRotation = moon.transform.rotation;
        LightAllBeacons();
        yield return null;
        Assert.IsTrue((bool)Get(dawn, "IsPlaying"), "the dawn plays");
        float maxSun = 0f, maxIntensity = 0f, maxAngle = 0f, maxBlend = 0f;
        float waited = 0f;
        while ((bool)Get(dawn, "IsPlaying") && waited < 20f)
        {
            maxSun = Mathf.Max(maxSun, (float)Get(dawn, "SkySunIntensity"));
            maxIntensity = Mathf.Max(maxIntensity, moon.intensity);
            maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(nightRotation, moon.transform.rotation));
            maxBlend = Mathf.Max(maxBlend, Shader.GetGlobalFloat("_LanternSkyBlend"));
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.AreEqual(0f, maxSun, "no sun in the sky");
        Assert.LessOrEqual(maxIntensity, nightIntensity * 1.3f, "the light never becomes a sun");
        Assert.LessOrEqual(maxAngle, 12f, "the light stays near its night rotation");
        Assert.LessOrEqual(maxBlend, 0.25f + 1e-3f, "the sky never blends past the reach");
        Assert.Greater(maxBlend, 0.2f, "the sky reaches the pre-dawn");
        yield return null;
        yield return null;
        Assert.IsTrue((bool)Get(manager, "IsRoundOver"));
        Assert.IsFalse((bool)Get(manager, "DawnPlaying"));
        ResultPanel result = UnityEngine.Object.FindAnyObjectByType<ResultPanel>(FindObjectsInactive.Include);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsShowing, "the result panel shows");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleStillFullSunrise()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Component dawn = (Component)Find("DawnSequence");
        Assert.AreEqual(1f, (float)Get(dawn, "Reach"), 1e-6f, "Island 5 keeps reach 1");
        Component manager = (Component)Find("GameManager");
        LightAllBeacons();
        Assert.IsTrue((bool)Get((Component)Find("SummitGate"), "Open"));
        Refuel();
        Call(Find("GreatLamp"), "Light");
        float maxSun = 0f, maxBlend = 0f;
        float start = Time.realtimeSinceStartup;
        while (!(bool)Get(manager, "Won") && Time.realtimeSinceStartup - start < 52f / 8f + 4f)
        {
            maxSun = Mathf.Max(maxSun, (float)Get(dawn, "SkySunIntensity"));
            maxBlend = Mathf.Max(maxBlend, Shader.GetGlobalFloat("_LanternSkyBlend"));
            yield return null;
        }

        Assert.IsTrue((bool)Get(manager, "Won"));
        Assert.AreEqual(1f, maxBlend, 0.02f, "the finale sky reaches the full dawn");
        Assert.AreEqual(1f, maxSun, 0.02f, "the finale has the full sun");
    }
}
}
