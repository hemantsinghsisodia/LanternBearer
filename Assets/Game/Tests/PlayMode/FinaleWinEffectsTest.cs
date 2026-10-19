using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
// What the finale does to the screen and the mix once the great lamp is lit on Island 5, played through the real path (all eight beacons, the gate, then
// GreatLamp.TryLight): the gameplay HUD fades out and stays hidden through the credits, and the low-fuel effects fade out and stay off.
public class FinaleWinEffectsTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    static readonly string[] IntKeys = { "LanternKeeperIntro_island5", "LanternKeeperWon_island5", "LanternKeeperLit_island5" };
    const string BestKey = "LanternKeeperBest_island5";

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    float savedBest;
    int savedGraphics;
    float previousSpeed = 1f;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp") ?? Type.GetType("LanternKeeper." + name + ", LanternKeeper.Hud");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static object Find(string typeName)
    {
        return UnityEngine.Object.FindAnyObjectByType(GameType(typeName), FindObjectsInactive.Include);
    }

    static object Get(object target, string member)
    {
        PropertyInfo property = target.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property, target.GetType().Name + "." + member + " not found");
        return property.GetValue(target);
    }

    static FieldInfo SpeedField()
    {
        FieldInfo field = GameType("FinaleSequence").GetField("TestSpeed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "FinaleSequence.TestSpeed");
        return field;
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
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SpeedField().SetValue(null, previousSpeed);
        LitKeyGuard.Restore();
        foreach (KeyValuePair<string, int> pair in savedInts)
        {
            if (pair.Value < 0) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetInt(pair.Key, pair.Value);
        }

        if (float.IsNaN(savedBest)) PlayerPrefs.DeleteKey(BestKey); else PlayerPrefs.SetFloat(BestKey, savedBest);
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        PlayerPrefs.Save();
        AsyncOperation load = SceneManager.LoadSceneAsync("MainMenu");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        float waited = 0f;
        while (waited < seconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // Loads Island 5, lights every beacon, and leaves the lamp ready to light. Returns the lantern and the great lamp.
    IEnumerator Prepare(Action<object, object> done)
    {
        PlayerPrefs.SetInt(GraphicsKey, 0);
        PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
        previousSpeed = (float)SpeedField().GetValue(null);
        SpeedField().SetValue(null, 8f);
        AsyncOperation load = SceneManager.LoadSceneAsync("Island5");
        while (!load.isDone)
        {
            yield return null;
        }

        StormSurgeSwitch.DisableFor("Island5");
        yield return null;
        yield return null;
        object lantern = Find("Lantern");
        MethodInfo addFuel = lantern.GetType().GetMethod("AddFuel");
        addFuel.Invoke(lantern, new object[] { 100f });
        Transform beacons = GameObject.Find("Beacons").transform;
        for (int i = 0; i < beacons.childCount; i++)
        {
            Component beacon = beacons.GetChild(i).GetComponent(GameType("Beacon"));
            Assert.IsTrue((bool)beacon.GetType().GetMethod("TryLight").Invoke(beacon, null), "beacon " + i);
            yield return null;
        }

        yield return Wait(5f);
        done(lantern, Find("GreatLamp"));
    }

    // Both effects, measured on the player-facing state: every visible graphic under GameHud and the prompt.
    static int VisibleGameHudGraphics(Transform gameHud)
    {
        int visible = 0;
        foreach (Graphic graphic in gameHud.GetComponentsInChildren<Graphic>(false))
        {
            if (graphic.canvasRenderer.cull || !graphic.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (graphic.canvasRenderer.GetInheritedAlpha() > 0.01f && graphic.color.a > 0.01f)
            {
                visible++;
            }
        }

        return visible;
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator HudHiddenDuringFinale()
    {
        object lantern = null, lamp = null;
        yield return Prepare((l, g) => { lantern = l; lamp = g; });
        object hud = Find("HUD");
        Transform gameHud = ((Component)hud).transform.Find("GameHud");
        Assert.IsNotNull(gameHud, "the GameHud cluster");
        Assert.AreEqual(1f, (float)Get(hud, "GameHudAlpha"), 1e-4f, "the HUD shows during play");
        Assert.Greater(VisibleGameHudGraphics(gameHud), 3, "the cluster, timer and label are on screen before the lamp is lit");

        Assert.IsTrue((bool)lamp.GetType().GetMethod("TryLight").Invoke(lamp, null), "the lamp lights");
        yield return null;
        yield return null;
        float early = (float)Get(hud, "GameHudAlpha");
        Assert.Greater(early, 0.2f, "the HUD fades, it does not vanish at once");
        Assert.Less(early, 1f, "and it has started to fade");

        yield return Wait(0.8f);
        Assert.AreEqual(0f, (float)Get(hud, "GameHudAlpha"), 1e-4f, "the HUD is gone half a second after the finale starts");
        Assert.AreEqual(0, VisibleGameHudGraphics(gameHud), "no cluster, timer, island label, chip or toast graphic is visible during the finale");
        TMP_Text prompt = ((Component)hud).transform.Find("PromptText") != null ? ((Component)hud).transform.Find("PromptText").GetComponent<TMP_Text>() : null;
        Assert.IsTrue(prompt == null || !prompt.gameObject.activeInHierarchy, "the interact prompt is hidden");

        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../.superpowers/sdd/2026-10-19-island5-fixes/shots"));
        if (System.IO.Directory.Exists(dir)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "finale_nohud.png"), shot.EncodeToPNG());
        UnityEngine.Object.Destroy(shot);

        // Through to the credits: still hidden.
        object credits = Find("CreditsRoll");
        float giveUp = Time.realtimeSinceStartup + 60f;
        while (!(bool)Get(credits, "Started") && Time.realtimeSinceStartup < giveUp)
        {
            yield return null;
        }

        Assert.IsTrue((bool)Get(credits, "Started"), "the credits start");
        yield return Wait(0.5f);
        Assert.AreEqual(0f, (float)Get(hud, "GameHudAlpha"), 1e-4f, "the HUD stays hidden through the credits");
        Assert.AreEqual(0, VisibleGameHudGraphics(gameHud), "and none of it shows");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator LowFuelFxStopsOnWin()
    {
        object lantern = null, lamp = null;
        yield return Prepare((l, g) => { lantern = l; lamp = g; });
        float max = (float)Get(lantern, "MaxFuel");
        float fuel = (float)Get(lantern, "Fuel");
        lantern.GetType().GetProperty("DrainFrozen").SetValue(lantern, true);
        Assert.IsTrue((bool)lantern.GetType().GetMethod("TrySpend").Invoke(lantern, new object[] { fuel - max * 0.1f }), "the lantern is run down to 10%");
        object fx = Find("LowFuelFX");
        object audio = GameType("AudioManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Assert.IsNotNull(audio, "the audio manager");
        yield return Wait(1f);
        Assert.AreEqual(0.1f, (float)Get(lantern, "FuelNormalized"), 0.01f, "fuel is at 10%");
        Assert.IsTrue((bool)Get(fx, "VignetteOverridden"), "before the win the low-fuel vignette runs");
        Assert.IsTrue((bool)Get(fx, "SaturationOverridden"), "and the screen is desaturated");
        Assert.IsTrue((bool)Get(audio, "HeartbeatPlaying"), "and the heartbeat plays");

        Assert.IsTrue((bool)lamp.GetType().GetMethod("TryLight").Invoke(lamp, null), "the lamp lights at 10% fuel");
        yield return Wait(1f);
        Assert.AreEqual(1f, (float)Get(fx, "WinCalm"), 1e-4f, "the low-fuel effects have faded out");
        Assert.AreEqual(0f, (float)Get(fx, "EdgePulseAmount"), 1e-4f, "no edge pulse");
        Assert.IsFalse((bool)Get(fx, "VignetteOverridden"), "the vignette is back to the look volume's calm value");
        Assert.IsFalse((bool)Get(fx, "SaturationOverridden"), "the colour is back to the look volume's grade");
        Assert.IsFalse((bool)Get(audio, "HeartbeatPlaying"), "the heartbeat is not playing");

        // And they stay off: a late storm or moth pulse and a still-low lantern do not bring them back.
        fx.GetType().GetMethod("PulseEdge", new[] { typeof(float) }).Invoke(fx, new object[] { 0.8f });
        yield return Wait(0.5f);
        Assert.AreEqual(0f, (float)Get(fx, "EdgePulseAmount"), 1e-4f, "a late pulse is ignored");
        Assert.IsFalse((bool)Get(fx, "VignetteOverridden"), "the vignette stays calm");
        Assert.IsFalse((bool)Get(audio, "HeartbeatPlaying"), "the heartbeat stays off");
    }
}
}
