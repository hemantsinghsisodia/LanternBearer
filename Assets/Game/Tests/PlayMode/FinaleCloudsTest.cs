using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The finale's cloud banks and god rays, live (game types by reflection). Outside the finale the gameplay storm ring is exactly as it was.
public class FinaleCloudsTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    int savedGraphics;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static object Get(object target, string member)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            return property.GetValue(target);
        }

        FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, type.Name + "." + member + " not found");
        return field.GetValue(target);
    }

    [SetUp]
    public void SetUp()
    {
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        PlayerPrefs.Save();
        AsyncOperation load = SceneManager.LoadSceneAsync("MainMenu");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
    }

    static IEnumerator Load(int graphics)
    {
        PlayerPrefs.SetInt(GraphicsKey, graphics);
        PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
        AsyncOperation load = SceneManager.LoadSceneAsync("Island5");
        while (!load.isDone)
        {
            yield return null;
        }

        StormSurgeSwitch.DisableFor("Island5");
        yield return null;
        yield return null;
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator CloudLayersFollowThePreset()
    {
        yield return Load(3);
        object full = UnityEngine.Object.FindAnyObjectByType(GameType("FinaleClouds"));
        Assert.IsNotNull(full, "Island 5 has FinaleClouds");
        Assert.AreEqual(3, (int)Get(full, "ActiveLayers"), "three cloud layers on Ultra");
        Assert.IsTrue((bool)Get(full, "RaysEnabled"), "god rays on Ultra");

        yield return Load(0);
        object low = UnityEngine.Object.FindAnyObjectByType(GameType("FinaleClouds"));
        Assert.AreEqual(1, (int)Get(low, "ActiveLayers"), "one cloud layer on Low");
        Assert.IsFalse((bool)Get(low, "RaysEnabled"), "no god rays on Low");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinalePlayLightsTheGreatLamp()
    {
        yield return Load(3);
        Component lamp = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("GreatLamp"));
        Component finale = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("FinaleSequence"));
        Assert.IsFalse((bool)Get(lamp, "Lit"), "the lamp starts dark");
        GameType("FinaleSequence").GetMethod("Play").Invoke(finale, new object[] { null });
        yield return null;
        Assert.IsTrue((bool)Get(lamp, "Lit"), "Play lights the lamp");
        float waited = 0f;
        while (waited < 2.6f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        Component beam = lamp.GetComponentInChildren(GameType("GreatLampBeam"), true);
        Assert.IsTrue((bool)Get(beam, "Running"), "the beam runs");
        Assert.Greater((float)Get(beam, "Intensity01"), 0.9f, "the beam is up after its ramp");
        Transform glassTransform = lamp.transform.Find("BeaconGlass");
        Assert.IsNotNull(glassTransform);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        glassTransform.GetComponent<Renderer>().GetPropertyBlock(block);
        Assert.AreEqual(1f, block.GetFloat("_Glow"), 1e-4f, "the glass glows fully");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator CloudsAreHiddenAndStormRingUnchangedInPlay()
    {
        yield return Load(3);
        object clouds = UnityEngine.Object.FindAnyObjectByType(GameType("FinaleClouds"));
        for (int i = 0; i < 3; i++)
        {
            MeshRenderer layer = (MeshRenderer)GameType("FinaleClouds").GetMethod("LayerRenderer").Invoke(clouds, new object[] { i });
            Assert.IsFalse(layer.enabled, "cloud layer " + i + " is off outside the finale");
        }

        Assert.IsFalse(((MeshRenderer)Get(clouds, "RaysRenderer")).enabled, "the god rays are off outside the finale");
        Assert.IsFalse((bool)Get(clouds, "Shown"));

        object storm = UnityEngine.Object.FindAnyObjectByType(GameType("StormRing"));
        yield return null;
        Assert.AreEqual(1f, (float)Get(storm, "Fade"), 1e-6f, "the gameplay storm ring is not faded");
        Assert.AreEqual(0f, (float)Get(storm, "Open"), 1e-6f, "and not opened");
        Assert.IsTrue((bool)Get(storm, "RendererEnabled"), "and drawn");
        Assert.AreEqual(4 * 48, (int)Get(storm, "ActiveCards"), "with its four layers of cards");
    }
}
}
