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
// The finale's distant beacon columns must read as one shaft from the island to its tip. Pure additive amber on the pale sunrise sky washes out to a faint
// pink-white while it stays strong amber over the dark sea, so the column looked cut at the horizon. The column material blends with premultiplied alpha
// (_AlphaMix 1) so both backgrounds get the same amber. The real path is played: all eight beacons, the gate, then GreatLamp.TryLight.
public class FinaleColumnTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    const float SampleTime = 28f;
    // Largest step across the horizon, in the shaft interior: brightness (mean of r, g, b) and warmth ((r - b) / (r + g + b)).
    const float MaxBrightnessStep = 0.07f;
    const float MaxWarmthStep = 0.09f;
    static readonly string[] IntKeys =
    {
        "LanternKeeperIntro_island5", "LanternKeeperWon_island5", "LanternKeeperLit_island5"
    };
    const string BestKey = "LanternKeeperBest_island5";

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    float savedBest;
    int savedGraphics;
    float previousCapture;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static object Find(string typeName)
    {
        return UnityEngine.Object.FindAnyObjectByType(GameType(typeName));
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
        previousCapture = Time.captureDeltaTime;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.captureDeltaTime = previousCapture;
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

    struct Step
    {
        public float brightness, warmth;
    }

    static Color Mean(Color[] row, int from, int to)
    {
        Color sum = Color.black;
        int n = 0;
        for (int i = Mathf.Max(0, from); i < Mathf.Min(row.Length, to); i++)
        {
            sum += row[i];
            n++;
        }

        return n > 0 ? sum / n : Color.black;
    }

    // Brightness and warmth of the shaft interior, averaged over bands just above and just below the horizon, along the column's screen-space centre line
    // (offset a little to one side of the hot central streak, still well inside the shaft). Returns |above - below|.
    static Step Measure(Texture2D shot, Camera cam, Renderer column)
    {
        int w = shot.width, h = shot.height;
        // The horizon: the strongest brightness change down a column of pixels at the far left, between 20% and 80% of the height.
        int horizon = -1;
        float strongest = 0f;
        for (int y = (int)(h * 0.2f); y < (int)(h * 0.8f); y++)
        {
            Color a = shot.GetPixel(4, y + 2), b = shot.GetPixel(4, y - 2);
            float g = Mathf.Abs(a.r + a.g + a.b - b.r - b.g - b.b);
            if (g > strongest)
            {
                strongest = g;
                horizon = y;
            }
        }

        Assert.Greater(strongest, 0.1f, "a horizon is visible at the left edge of the frame");

        Bounds bounds = column.bounds;
        Vector3 bottom = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        Vector3 top = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        float scale = h / 2160f;
        int bandNear = Mathf.RoundToInt(25f * scale), bandFar = Mathf.RoundToInt(50f * scale);
        Assert.Greater(top.y, horizon + bandFar + 5, "the column reaches above the horizon in frame");
        Assert.Less(bottom.y, horizon - bandFar - 5, "and starts below it");

        Color above = Color.black, below = Color.black;
        int count = 0;
        for (int d = bandNear; d <= bandFar; d += 5)
        {
            for (int side = 0; side < 2; side++)
            {
                int y = side == 0 ? horizon + d : horizon - d;
                float t = Mathf.InverseLerp(bottom.y, top.y, y);
                float cx = Mathf.Lerp(bottom.x, top.x, t);
                int from = Mathf.RoundToInt(cx + w * 0.003f), to = Mathf.RoundToInt(cx + w * 0.008f);
                Color mean = Mean(shot.GetPixels(0, y, w, 1), from, to);
                if (side == 0) above += mean; else below += mean;
            }

            count++;
        }

        above /= count;
        below /= count;
        Step step;
        step.brightness = Mathf.Abs((above.r + above.g + above.b) / 3f - (below.r + below.g + below.b) / 3f);
        step.warmth = Mathf.Abs((above.r - above.b) / (above.r + above.g + above.b) - (below.r - below.b) / (below.r + below.g + below.b));
        Debug.Log("FinaleColumn horizon " + horizon + " brightnessStep " + step.brightness.ToString("F3") + " warmthStep " + step.warmth.ToString("F3"));
        return step;
    }

    static IEnumerator Capture(Action<Texture2D> done)
    {
        yield return new WaitForEndOfFrame();
        done(ScreenCapture.CaptureScreenshotAsTexture());
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator FinaleColumnIsContinuous()
    {
        PlayerPrefs.SetInt(GraphicsKey, 3);
        PlayerPrefs.SetInt("LanternKeeperIntro_island5", 1);
        AsyncOperation load = SceneManager.LoadSceneAsync("Island5");
        while (!load.isDone)
        {
            yield return null;
        }

        StormSurgeSwitch.DisableFor("Island5");
        yield return null;
        yield return null;

        Component finale = (Component)Find("FinaleSequence");
        Component lantern = (Component)Find("Lantern");
        MethodInfo addFuel = lantern.GetType().GetMethod("AddFuel");
        addFuel.Invoke(lantern, new object[] { 100f });
        Transform beacons = GameObject.Find("Beacons").transform;
        for (int i = 0; i < beacons.childCount; i++)
        {
            Component beacon = beacons.GetChild(i).GetComponent(GameType("Beacon"));
            Assert.IsTrue((bool)beacon.GetType().GetMethod("TryLight").Invoke(beacon, null), "beacon " + i);
            yield return null;
        }

        float waited = 0f;
        while (waited < 5f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        addFuel.Invoke(lantern, new object[] { 100f });
        Component lamp = (Component)Find("GreatLamp");
        Time.captureDeltaTime = 1f / 30f;
        Assert.IsTrue((bool)lamp.GetType().GetMethod("TryLight").Invoke(lamp, null), "the lamp lights");
        PropertyInfo elapsed = finale.GetType().GetProperty("Elapsed");
        float giveUp = Time.realtimeSinceStartup + 150f;
        while ((float)elapsed.GetValue(finale) < SampleTime && Time.realtimeSinceStartup < giveUp)
        {
            yield return null;
        }

        Assert.GreaterOrEqual((float)elapsed.GetValue(finale), SampleTime, "the finale reached the island phase");

        // Freeze the frame: the finale clock crawls and the camera stops, so the columns stand still while the variants are rendered.
        Time.captureDeltaTime = 0.0005f;
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
        {
            if (behaviour.GetType().Name == "FinaleCamera")
            {
                behaviour.enabled = false;
            }
        }

        yield return null;
        yield return null;

        Camera cam = Camera.main;
        Renderer column = null;
        float best = float.MaxValue;
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.sharedMaterial == null || !r.sharedMaterial.name.StartsWith("FinaleColumn"))
            {
                continue;
            }

            Vector3 p = cam.WorldToScreenPoint(r.bounds.center);
            if (p.z > 0f && Mathf.Abs(p.x - Screen.width / 2f) < best)
            {
                best = Mathf.Abs(p.x - Screen.width / 2f);
                column = r;
            }
        }

        Assert.IsNotNull(column, "a finale column is up at " + SampleTime + " s");
        Assert.AreEqual(1f, column.sharedMaterial.GetFloat("_AlphaMix"), 1e-4f, "the column material blends with premultiplied alpha");

        Texture2D fixedShot = null;
        yield return Capture(t => fixedShot = t);
        Step fixedStep = Measure(fixedShot, cam, column);
        UnityEngine.Object.Destroy(fixedShot);

        // Negative control: the old look (purely additive, alpha 0.5) on a private material instance steps across the horizon by more than the limit.
        Material old = column.material;
        old.SetFloat("_AlphaMix", 0f);
        Color baseColor = old.GetColor("_BaseColor");
        baseColor.a = 0.5f;
        old.SetColor("_BaseColor", baseColor);
        yield return null;
        yield return null;
        Texture2D oldShot = null;
        yield return Capture(t => oldShot = t);
        Step oldStep = Measure(oldShot, cam, column);
        UnityEngine.Object.Destroy(oldShot);

        Assert.Less(fixedStep.brightness, MaxBrightnessStep, "the column's brightness is continuous across the horizon");
        Assert.Less(fixedStep.warmth, MaxWarmthStep, "and so is its colour");
        Assert.IsTrue(oldStep.brightness >= MaxBrightnessStep || oldStep.warmth >= MaxWarmthStep,
            "negative control: the old additive column breaks at the horizon (brightness " + oldStep.brightness + ", warmth " + oldStep.warmth + ")");
    }
}
}
