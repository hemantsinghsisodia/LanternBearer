using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The Island 5 finale cinematic, live. Game types are reached by reflection (this assembly can't reference Assembly-CSharp).
// The finale runs on unscaled time; FinaleSequence.TestSpeed (8) makes the 66 s sequence take 8.25 s.
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
        LitKeyGuard.Restore();
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

        StormSurgeSwitch.DisableFor(scene);
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

    // The real E-press path (TryLight -> WinNow -> FinaleSequence.Play -> EnsureLit) plays exactly one ignite cue for the great lamp, and a
    // second press is refused without another cue.
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator LightingTheLampPlaysExactlyOneIgniteCue()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        LightAllBeacons();
        Refuel();
        int ignites = 0;
        Action<string> spy = cue =>
        {
            if (cue == "Beacon.Ignite")
            {
                ignites++;
            }
        };
        EventInfo cuePlayed = GameType("AudioManager").GetEvent("CuePlayed", BindingFlags.Public | BindingFlags.Static);
        cuePlayed.AddEventHandler(null, spy);
        try
        {
            Component lamp = (Component)Find("GreatLamp");
            Assert.IsTrue((bool)Call(lamp, "TryLight"), "the first press lights the lamp");
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Assert.IsFalse((bool)Call(lamp, "TryLight"), "a second press is refused");
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
        }
        finally
        {
            cuePlayed.RemoveEventHandler(null, spy);
        }

        Assert.AreEqual(1, ignites, "one ignite cue for the lamp");
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
        float limit = 66f / 8f + 1f;
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
        Assert.LessOrEqual(wonAt - start, limit, "WonGame within (66 / 8) + 1 s of unscaled time");
        Assert.IsTrue((bool)Get(manager, "Won"));
        Assert.IsFalse((bool)Get(manager, "DawnPlaying"), "the dawn flag is released");
        Assert.GreaterOrEqual(phases.Count, 4, "phases seen: " + string.Join(",", phases));
        for (int i = 1; i < phases.Count; i++)
        {
            Assert.Greater(phases[i], phases[i - 1], "phases in order: " + string.Join(",", phases));
        }

        Assert.AreEqual(0, phases[0], "starts at Ignition");
        Assert.IsTrue(phases.Contains(1) && phases.Contains(2) && phases.Contains(3) && phases.Contains(4), "BeamAndStorm, Islands, Sunrise and Hold were all seen: " + string.Join(",", phases));
        Assert.GreaterOrEqual(wonAt - start, 66f / 8f - 0.5f, "the credits start at 66 s (at TestSpeed 8), not before");
        Assert.AreEqual(66f, (float)Get(finale, "Elapsed"), 0.01f, "the finale clock ends at 66 s");
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
        while (wonCount == 0 && Time.realtimeSinceStartup - start < 66f / 8f + 4f)
        {
            Call(manager, "Pause");
            Assert.IsFalse((bool)Get(manager, "IsPaused"), "pause has no effect during the finale");
            tries++;
            yield return null;
        }

        yield return null;
        Assert.Greater(tries, 5);
        Assert.AreEqual(1, wonCount, "the finale ran to the end at timeScale 0");
        Assert.LessOrEqual(wonAt - start, 66f / 8f + 1f);
    }

    // The cinematic flight on the real camera: the lamp is in the frame at the checkpoints, each island is passed 150-220 m away at its flare
    // time, the pitch is never steeper than 20 degrees down, the lens stays the gameplay one and the far plane is raised.
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FlightFramesTheLampAndPassesEachIsland()
    {
        yield return Load("Island5");
        Camera cam = Camera.main;
        Assert.IsNotNull(cam);
        float fov = cam.fieldOfView;
        LightTheLamp();
        Component finale = (Component)Find("FinaleSequence");
        Component finaleCamera = finale.GetComponent(GameType("FinaleCamera"));
        Transform flame = (Transform)Get(Find("GreatLamp"), "Flame");
        Component islands = finale.GetComponentInChildren(GameType("FinaleIslands"), true);
        float minY = -Mathf.Sin(20f * Mathf.Deg2Rad) - 0.001f;

        float[] checkpoints = { 3f, 15f, 50f, 64f };
        for (int i = 0; i < checkpoints.Length; i++)
        {
            Call(finaleCamera, "Place", checkpoints[i]);
            Vector3 view = cam.WorldToViewportPoint(flame.position);
            Assert.Greater(view.z, 0f, "the lamp is in front of the camera at " + checkpoints[i] + " s");
            Assert.That(view.x, Is.InRange(0.03f, 0.97f), "the lamp is inside the frame (x) at " + checkpoints[i] + " s");
            Assert.That(view.y, Is.InRange(0.03f, 0.97f), "the lamp is inside the frame (y) at " + checkpoints[i] + " s");
        }

        for (float t = 0f; t <= 66f; t += 0.5f)
        {
            Call(finaleCamera, "Place", t);
            Assert.GreaterOrEqual(cam.transform.forward.y, minY, "pitch no steeper than -20 degrees at " + t + " s");
        }

        for (int i = 0; i < 4; i++)
        {
            float t = 21f + 6f * i;
            Call(finaleCamera, "Place", t);
            object entry = Call(islands, "EntryAt", i);
            Transform root = (Transform)entry.GetType().GetField("root").GetValue(entry);
            Vector3 flareLocal = (Vector3)entry.GetType().GetField("flareLocal").GetValue(entry);
            Vector3 flare = root.TransformPoint(flareLocal);
            // FinaleFlightPath.FlarePoint is a measured constant (relative to the lamp base): it must still match the installed island, or the flight misses it.
            Vector3 lampBase = flame.position - Vector3.up * 11.9f;
            Assert.Less(Vector3.Distance(flare - lampBase, FinaleFlightPath.FlarePoint(i)), 6f, "FlightPath flare point " + (i + 1) + " matches the installed island: installed " + (flare - lampBase) + " vs path " + FinaleFlightPath.FlarePoint(i));
            float distance = Vector3.Distance(cam.transform.position, flare);
            Assert.That(distance, Is.InRange(150f, 220f), "island " + (i + 1) + " is passed at a pleasing distance at " + t + " s");
            Vector3 view = cam.WorldToViewportPoint(flare);
            Assert.That(view.x, Is.InRange(0.2f, 0.8f), "island " + (i + 1) + " is centred-ish at " + t + " s");
            Assert.That(view.y, Is.InRange(0.03f, 0.97f), "and its beacon is in the frame");
        }

        Assert.AreEqual(fov, cam.fieldOfView, 0.001f, "the gameplay lens");
        yield return null;
        Assert.GreaterOrEqual(cam.farClipPlane, 4000f, "the far plane is raised for the whole finale");
    }

    // The far sea is one solid gradient to the horizon: its material is opaque out there (nothing shows through: not the sky, not the sea floor edge,
    // not the sun before it rises) and a column of pixels from the sea up to the horizon has no hard step in it.
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FarSeaIsOneSolidGradientToTheHorizon()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Camera cam = Camera.main;
        LightTheLamp();
        Component finale = (Component)Find("FinaleSequence");
        float start = Time.realtimeSinceStartup;
        while ((float)Get(finale, "Elapsed") < 44f && Time.realtimeSinceStartup - start < 20f)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        Renderer water = GameObject.Find("Water").GetComponent<Renderer>();
        Assert.IsTrue(water.material.HasProperty("_FarSolid"), "the water shader has the far-solid switch");

        // Pixel check at the 44 s pose: the sun is still below the horizon, so nothing bright may show under the horizon line.
        // (The cloud banks and the island miniatures are switched off for the capture: only the sea, the sky and the sun are being judged.)
        finale.GetComponentInChildren(GameType("FinaleClouds"), true).gameObject.SetActive(false);
        finale.GetComponentInChildren(GameType("FinaleIslands"), true).gameObject.SetActive(false);
        float worst, maxLuma;
        MeasureSeaBelowHorizon(cam, out worst, out maxLuma);
        Assert.Less(worst, 0.07f, "no hard line in the sea below the horizon (largest step between rows)");
        Assert.Less(maxLuma, 0.5f, "the sea is not lit up by anything showing through it");

        // Negative control: the same pose with the far-solid switch off must look different (a hard edge or a lit-up far sea), otherwise the
        // check above would pass for any water and prove nothing.
        float solid = water.material.GetFloat("_FarSolid");
        water.material.SetFloat("_FarSolid", 0f);
        float controlWorst, controlLuma;
        MeasureSeaBelowHorizon(cam, out controlWorst, out controlLuma);
        water.material.SetFloat("_FarSolid", solid);
        Debug.Log("FarSea control: solid worst " + worst.ToString("F4") + " luma " + maxLuma.ToString("F3") + "; control worst " + controlWorst.ToString("F4") + " luma " + controlLuma.ToString("F3"));
        Assert.IsTrue(controlWorst > worst + 0.005f || controlLuma > maxLuma + 0.01f, "with _FarSolid = 0 the far sea looks different, so the check can fail");
    }

    // Renders the camera and returns the largest luma step between neighbouring rows, and the brightest luma, in three columns from 20 to 100 rows
    // above the horizon line (the sea just below the horizon, in the view's own orientation).
    static void MeasureSeaBelowHorizon(Camera cam, out float worst, out float maxLuma)
    {
        RenderTexture rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = previous;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Vector3 flat = cam.transform.forward;
        flat.y = 0f;
        flat.Normalize();
        Vector3 horizon = cam.WorldToViewportPoint(cam.transform.position + flat * 3500f);
        int horizonRow = Mathf.RoundToInt(horizon.y * 540f);
        Assert.Greater(horizonRow, 40, "the horizon is in the frame");
        worst = 0f;
        maxLuma = 0f;
        for (int column = 0; column < 3; column++)
        {
            int x = Mathf.RoundToInt(960f * (0.15f + 0.35f * column));
            float prev = -1f;
            for (int row = horizonRow - 20; row > Mathf.Max(4, horizonRow - 100); row -= 1)
            {
                Color c = tex.GetPixel(x, row);
                float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                maxLuma = Mathf.Max(maxLuma, luma);
                if (prev >= 0f)
                {
                    worst = Mathf.Max(worst, Mathf.Abs(luma - prev));
                }

                prev = luma;
            }
        }

        UnityEngine.Object.Destroy(tex);
        rt.Release();
        UnityEngine.Object.Destroy(rt);
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

    const string FinaleGradeName = "LookVolume_finale";

    // Islands 1-4 never get the finale grade: no volume of that name or profile exists, enabled or not.
    static void AssertNoFinaleGrade(string when)
    {
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            Assert.AreNotEqual(FinaleGradeName, volume.name, "no finale grade volume " + when);
            Assert.IsFalse(volume.sharedProfile != null && volume.sharedProfile.name == FinaleGradeName, "no finale grade profile " + when);
        }
    }

    // The Island 5 finale grade (warm balance, lifted shadows, mild bloom, a slight vignette) is weight 0 until the sunrise and reaches its target at the end.
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator FinaleGradeBlendsInOverTheSunrise()
    {
        yield return Load("Island5");
        SetTestSpeed(8f);
        Component manager = (Component)Find("GameManager");
        Subscribe(manager);
        Component finale = (Component)Find("FinaleSequence");
        Volume grade = (Volume)Get(finale, "FinaleGrade");
        Assert.IsNotNull(grade, "Island 5 has the finale grade volume");
        Assert.AreEqual(FinaleGradeName, grade.name);
        Assert.AreEqual(0f, grade.weight, 1e-6f, "weight 0 before the finale");
        Assert.IsFalse(grade.enabled, "the grade volume is disabled (out of the volume stack) before the finale");
        Assert.IsTrue(grade.isGlobal);
        VolumeProfile profile = grade.sharedProfile;
        Assert.IsNotNull(profile);
        UnityEngine.Rendering.Universal.WhiteBalance balance;
        UnityEngine.Rendering.Universal.Bloom bloom;
        UnityEngine.Rendering.Universal.Vignette vignette;
        Assert.IsTrue(profile.TryGet(out balance) && balance.temperature.value > 0f, "warm white balance");
        Assert.IsTrue(profile.TryGet(out bloom) && Mathf.Abs(bloom.threshold.value - 1f) < 0.01f && Mathf.Abs(bloom.intensity.value - 0.6f) < 0.01f, "mild bloom: threshold 1, intensity 0.6");
        Assert.IsTrue(profile.TryGet(out vignette) && vignette.intensity.value > 0f, "a slight vignette");
        float target = Convert.ToSingle(GameType("FinaleSequence").GetField("GradeTarget").GetRawConstantValue());
        LightTheLamp();
        float maxBefore = 0f;
        bool enabledBefore = false;
        float maxAny = 0f;
        float start = Time.realtimeSinceStartup;
        while (wonCount == 0 && Time.realtimeSinceStartup - start < 66f / 8f + 4f)
        {
            float t = (float)Get(finale, "Elapsed");
            if (t < 42f)
            {
                maxBefore = Mathf.Max(maxBefore, grade.weight);
                enabledBefore |= grade.enabled;
            }

            maxAny = Mathf.Max(maxAny, grade.weight);
            yield return null;
        }

        yield return null;
        Assert.AreEqual(1, wonCount, "the finale ran");
        Assert.AreEqual(0f, maxBefore, 1e-6f, "the grade stays at weight 0 until the sunrise");
        Assert.IsFalse(enabledBefore, "and stays disabled until the sunrise blend starts");
        Assert.IsTrue(grade.enabled, "the volume is enabled once the sunrise blends it in");
        Assert.AreEqual(target, grade.weight, 0.01f, "the grade reaches its target at the end of the sunrise");
        Assert.LessOrEqual(maxAny, target + 1e-4f);
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator IslandsOneToFourWinUnchanged()
    {
        yield return Load("Island1");
        Component manager = (Component)Find("GameManager");
        Assert.IsFalse((bool)Get(manager, "HasFinale"), "Island 1 has no finale");
        Assert.IsNull(Find("FinaleSequence"));
        AssertNoFinaleGrade("before the win");
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
                // Beacons play no stinger: what follows is the win stinger.
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
        AssertNoFinaleGrade("after the win");
        Assert.AreEqual(1, (int)Get(director, "StingerRequests") - stingers, "the win stinger still plays on Island 1");
        Assert.GreaterOrEqual(waited, 4.5f, "the dawn kept its 6 s length");
    }
}
}
