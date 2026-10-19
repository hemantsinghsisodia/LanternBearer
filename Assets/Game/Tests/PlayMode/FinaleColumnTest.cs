using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The finale's distant beacon columns must read as one shaft from the island to its tip. The sea (Water.shader, Transparent 3000, scaled x8 in the
// finale) used to sort nearer than a column in the same queue from some camera positions and was drawn over its middle. The finale's additive elements
// now draw in queue FinaleInstaller.AfterSeaQueue. Sampled at the island-phase camera times, with a negative control at the old queue. The real path is
// played: all eight beacons, the gate, then GreatLamp.TryLight.
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

    static readonly float[] SampleTimes = { 21f, 22.5f, 24f, 26f, 27f, 28.5f, 30f, 32f, 33f, 34.5f, 36f, 38f, 39f, 40.5f, 42f, 44f };
    // The finale camera times sampled: the island phase, one island and column after the other.
    // Along the first 70% of the shaft's visible span on screen (the tip fades out on purpose) the shaft's contrast with the sky or sea beside it must
    // not fall below this fraction of its strongest contrast. A sea drawn over the column shows up as a stretch where the contrast is nearly nothing
    // (about 0.01 to 0.1 of the maximum); the continuous column stays above 0.25.
    const float MinContrastRatio = 0.15f;
    const int Samples = 28;

    static Renderer FindWater()
    {
        GameObject water = GameObject.Find("Water");
        Assert.IsNotNull(water, "the Island 5 water");
        Renderer best = null;
        foreach (Renderer r in water.GetComponentsInChildren<Renderer>())
        {
            if (best == null || r.bounds.size.sqrMagnitude > best.bounds.size.sqrMagnitude) best = r;
        }

        Assert.IsNotNull(best, "the water renders");
        return best;
    }

    // The tall columns in front of the camera, with both ends well in front of the lens.
    static List<Renderer> VisibleColumns(Camera cam)
    {
        List<Renderer> list = new List<Renderer>();
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.sharedMaterial == null || !r.sharedMaterial.name.StartsWith("FinaleColumn") || r.bounds.size.y < 20f)
            {
                continue;
            }

            Vector3 b = cam.WorldToScreenPoint(new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z));
            Vector3 t = cam.WorldToScreenPoint(new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z));
            Vector3 mid = cam.WorldToScreenPoint(r.bounds.center);
            if (mid.z > 5f && mid.x > 0f && mid.x < Screen.width && (Mathf.Min(b.y, t.y) < Screen.height - 60f && Mathf.Max(b.y, t.y) > 60f))
            {
                list.Add(r);
            }
        }

        return list;
    }

    struct Measurement
    {
        public float median, minRatio, atFraction;
        public string log;
    }

    static Color Pixel(Texture2D shot, int x, int y)
    {
        return shot.GetPixel(Mathf.Clamp(x, 0, shot.width - 1), Mathf.Clamp(y, 0, shot.height - 1));
    }

    static bool Inside(Vector3 p, float margin, Texture2D shot)
    {
        return p.y > 30 && p.y < shot.height - 30 && p.x > margin && p.x < shot.width - margin;
    }

    // Contrast of the shaft's centre line with the background either side of it, sampled along the column's visible span on screen.
    static Measurement Measure(Texture2D shot, Camera cam, Renderer column)
    {
        Bounds bounds = column.bounds;
        Vector3 bottom = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        Vector3 top = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        float dist = Vector3.Distance(cam.transform.position, bounds.center);
        float halfPx = Mathf.Max(3f, 7f * shot.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist));
        float t0 = 0f, t1 = 1f;
        for (int k = 0; k < 200; k++)
        {
            float t = k / 199f;
            if (Inside(Vector3.Lerp(bottom, top, t), halfPx * 3f, shot)) { t0 = t; break; }
        }

        for (int k = 199; k >= 0; k--)
        {
            float t = k / 199f;
            if (Inside(Vector3.Lerp(bottom, top, t), halfPx * 3f, shot)) { t1 = t; break; }
        }

        List<float> contrast = new List<float>();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int off = Mathf.RoundToInt(halfPx * 2.2f);
        for (int i = 0; i < Samples; i++)
        {
            float t = Mathf.Lerp(t0, t1, Mathf.Lerp(0.15f, 0.85f, i / (Samples - 1f)));
            Vector3 p = Vector3.Lerp(bottom, top, t);
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            Color ctr = Color.black, side = Color.black;
            for (int dx = -2; dx <= 2; dx++)
            {
                ctr += Pixel(shot, x + dx, y);
                side += Pixel(shot, x - off + dx, y) + Pixel(shot, x + off + dx, y);
            }

            ctr /= 5f;
            side /= 10f;
            float c = Mathf.Sqrt((ctr.r - side.r) * (ctr.r - side.r) + (ctr.g - side.g) * (ctr.g - side.g) + (ctr.b - side.b) * (ctr.b - side.b));
            contrast.Add(c);
            sb.Append(c.ToString("F2")).Append(' ');
        }

        List<float> sorted = new List<float>(contrast);
        sorted.Sort();
        Measurement m;
        m.median = sorted[sorted.Count / 2];
        float lowest = float.MaxValue;
        for (int i = 0; i < Samples * 7 / 10; i++) lowest = Mathf.Min(lowest, contrast[i]);
        m.minRatio = lowest / Mathf.Max(1e-4f, sorted[sorted.Count - 1]);
        m.atFraction = contrast.IndexOf(sorted[0]) / (Samples - 1f);
        m.log = sb.ToString();
        return m;
    }

    static IEnumerator Capture(Action<Texture2D> done)
    {
        yield return new WaitForEndOfFrame();
        done(ScreenCapture.CaptureScreenshotAsTexture());
    }

    static void Save(Texture2D shot, string name)
    {
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../.superpowers/sdd/2026-10-19-island5-fixes/shots"));
        if (System.IO.Directory.Exists(dir))
        {
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name), shot.EncodeToPNG());
        }
    }

    // Whether the sea is drawn after this column: a higher render queue always draws later; within one queue URP sorts back to front by the camera
    // distance of the bounds centre, so the nearer renderer is drawn last.
    static bool WaterDrawsAfter(Camera cam, Renderer water, Renderer column)
    {
        int wq = water.sharedMaterial.renderQueue, cq = column.sharedMaterial.renderQueue;
        if (wq != cq) return wq > cq;
        return (water.bounds.center - cam.transform.position).sqrMagnitude < (column.bounds.center - cam.transform.position).sqrMagnitude;
    }

    [UnityTest]
    [Timeout(900000)]
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
        Renderer water = FindWater();

        float worstRatio = float.MaxValue;
        for (int s = 0; s < SampleTimes.Length; s++)
        {
            float giveUp = Time.realtimeSinceStartup + 150f;
            while ((float)elapsed.GetValue(finale) < SampleTimes[s] && Time.realtimeSinceStartup < giveUp)
            {
                yield return null;
            }

            Assert.GreaterOrEqual((float)elapsed.GetValue(finale), SampleTimes[s], "the finale reached " + SampleTimes[s] + " s");
            Camera cam = Camera.main;
            Texture2D shot = null;
            yield return Capture(t => shot = t);
            List<Renderer> columns = VisibleColumns(cam);
            Debug.Log("FinaleColumn t=" + SampleTimes[s] + " columns in view " + columns.Count + " cam " + cam.transform.position + " fwd " + cam.transform.forward + " tallColumns " + UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Count(r => r.enabled && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("FinaleColumn")));
            if (columns.Count == 0) { UnityEngine.Object.Destroy(shot); continue; }
            Save(shot, "columnsort_t" + SampleTimes[s].ToString("F0") + "_q" + columns[0].sharedMaterial.renderQueue + ".png");
            foreach (Renderer column in columns)
            {
                Measurement m = Measure(shot, cam, column);
                Debug.Log("FinaleColumn t=" + SampleTimes[s] + " " + column.transform.parent.name + " queue " + column.sharedMaterial.renderQueue + " water " + water.sharedMaterial.renderQueue
                    + " waterAfter=" + WaterDrawsAfter(cam, water, column) + " dWater " + Vector3.Distance(cam.transform.position, water.bounds.center).ToString("F0")
                    + " dCol " + Vector3.Distance(cam.transform.position, column.bounds.center).ToString("F0") + " median " + m.median.ToString("F3")
                    + " minRatio " + m.minRatio.ToString("F2") + " at " + m.atFraction.ToString("F2") + " | " + m.log);
                worstRatio = Mathf.Min(worstRatio, m.minRatio);
            }

            if (s == SampleTimes.Length - 1) Save(shot, "columnsort_after.png");
            UnityEngine.Object.Destroy(shot);
        }

        Assert.Less(worstRatio, float.MaxValue, "at least one column was measured");
        Assert.GreaterOrEqual(worstRatio, MinContrastRatio, "the column has no gap over the sea at any sampled camera time");

        // Negative control, at the last sampled pose (the sea is nearer than the Island 4 column): the old queue 3000 on a private copy of the column
        // material gets the sea drawn over the column, a gap the measurement must catch.
        {
            Camera cam = Camera.main;
            List<Renderer> columns = VisibleColumns(cam);
            Assert.Greater(columns.Count, 0, "a column is in view for the negative control");
            Renderer column = columns[0];
            column.material.renderQueue = 3000;
            yield return null;
            yield return null;
            Texture2D shot = null;
            yield return Capture(t => shot = t);
            columns = VisibleColumns(cam);
            Assert.IsTrue(columns.Contains(column), "the same column is still in view");
            Assert.IsTrue(WaterDrawsAfter(cam, water, column), "at queue 3000 the sea sorts after this column");
            Save(shot, "columnsort_before.png");
            Measurement old = Measure(shot, cam, column);
            UnityEngine.Object.Destroy(shot);
            Assert.Less(old.minRatio, MinContrastRatio, "negative control: the sea drawn over the column leaves a gap (ratio " + old.minRatio + ")");
        }
    }
}
}
