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
// Island 5's finale balance, with everything on (the storm surge, the hunting lightning, scarce fuel) on Normal:
//  - SteadyBotReachesLamp: a bot that walks the reachability route at full pace, lights each beacon on arrival, waits for the bridges and dodges
//    nothing (strikes can hit it) reaches the great lamp. The surge never catches it and its fuel never reaches 0.
//  - IdleBotIsCaught: a keeper that lights the first beacon and then waits at the beach is caught by the surge well inside a minute.
// The numbers it measures are logged ("Island5Balance ...") for the task report. Game types are reached by reflection.
public class Island5BalanceTest
{
    const float WaypointRadius = 0.7f;
    const float PlaybackScale = 2f;
    const float FixedFrameSeconds = 1f / 120f;
    const float ArriveRadius = 2f;
    const string DifficultyKey = "LanternKeeperDifficulty";
    static readonly string[] Keys = { "LanternKeeperIntro_island5", "LanternKeeperWon_island5", "LanternKeeperBest_island5", "LanternKeeperLog_island5" };

    readonly List<string> problems = new List<string>();
    float previousScale;
    float previousMaxDelta;
    float previousCaptureDelta;
    bool yawWasExternal;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static void SetStatic(string typeName, string field, object value)
    {
        FieldInfo info = GameType(typeName).GetField(field, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, typeName + "." + field + " not found");
        info.SetValue(null, value);
    }

    static object Member(object target, string name)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
        {
            return property.GetValue(target);
        }

        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, type.Name + "." + name + " not found");
        return field.GetValue(target);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, target.GetType().Name + "." + name + " not found");
        return method.Invoke(target, args);
    }

    [SetUp]
    public void SetUp()
    {
        LitKeyGuard.Save();
        problems.Clear();
        savedDifficulty = PlayerPrefs.HasKey(DifficultyKey) ? PlayerPrefs.GetInt(DifficultyKey) : -1;
        savedIntro = PlayerPrefs.HasKey(Keys[0]) ? PlayerPrefs.GetInt(Keys[0]) : -1;
        hadWon = PlayerPrefs.HasKey(Keys[1]);
        hadBest = PlayerPrefs.HasKey(Keys[2]);
        hadLog = PlayerPrefs.HasKey(Keys[3]);
        PlayerPrefs.SetInt(DifficultyKey, 1);
        PlayerPrefs.SetInt(Keys[0], 1);
        Application.logMessageReceived += OnLog;
        previousScale = Time.timeScale;
        previousMaxDelta = Time.maximumDeltaTime;
        previousCaptureDelta = Time.captureDeltaTime;
        yawWasExternal = (bool)GameType("CameraFollow").GetField("UseExternalYaw", BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    int savedDifficulty;
    int savedIntro;
    bool hadWon;
    bool hadBest;
    bool hadLog;

    [TearDown]
    public void TearDown()
    {
        LitKeyGuard.Restore();
        Application.logMessageReceived -= OnLog;
        if (savedDifficulty < 0)
        {
            PlayerPrefs.DeleteKey(DifficultyKey);
        }
        else
        {
            PlayerPrefs.SetInt(DifficultyKey, savedDifficulty);
        }

        if (savedIntro < 0)
        {
            PlayerPrefs.DeleteKey(Keys[0]);
        }
        else
        {
            PlayerPrefs.SetInt(Keys[0], savedIntro);
        }

        if (!hadWon)
        {
            PlayerPrefs.DeleteKey(Keys[1]);
        }

        if (!hadBest)
        {
            PlayerPrefs.DeleteKey(Keys[2]);
        }

        if (!hadLog)
        {
            PlayerPrefs.DeleteKey(Keys[3]);
        }

        PlayerPrefs.Save();
        Time.timeScale = previousScale;
        Time.maximumDeltaTime = previousMaxDelta;
        Time.captureDeltaTime = previousCaptureDelta;
        SetStatic("PlayerController", "ExternalMove", Vector2.zero);
        SetStatic("CameraFollow", "UseExternalYaw", yawWasExternal);
    }

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            problems.Add("[" + type + "] " + message);
        }
    }

    static IEnumerator Load()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("Island5");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return null;
    }

    static int NearestOnTrail(List<Vector3> points, Vector3 target, int from)
    {
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = Mathf.Max(0, from); i < points.Count; i++)
        {
            float flat = Vector2.Distance(new Vector2(points[i].x, points[i].z), new Vector2(target.x, target.z));
            float score = flat + Mathf.Abs(points[i].y - target.y) * 0.25f;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SteadyBotReachesLamp()
    {
        yield return Load();
        Component player = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("PlayerController"));
        object lantern = UnityEngine.Object.FindAnyObjectByType(GameType("Lantern"));
        object manager = UnityEngine.Object.FindAnyObjectByType(GameType("GameManager"));
        object lamp = UnityEngine.Object.FindAnyObjectByType(GameType("GreatLamp"));
        object surge = UnityEngine.Object.FindAnyObjectByType(GameType("StormSurge"));
        Assert.IsNotNull(player);
        Assert.IsNotNull(lantern);
        Assert.IsNotNull(lamp);
        Assert.IsNotNull(surge, "Island 5 has the storm surge");
        Transform beaconsRoot = GameObject.Find("Beacons").transform;
        Assert.AreEqual(8, beaconsRoot.childCount);
        GameObject guide = GameObject.Find("TrailGuide");
        Assert.IsNotNull(guide, "TrailGuide");

        Component[] bridges = new Component[4];
        for (int g = 0; g < 4; g++)
        {
            bridges[g] = GameObject.Find("LightBridge_Gap" + g).GetComponent(GameType("LightBridge"));
        }

        List<Vector3> trail = new List<Vector3>();
        foreach (Transform point in guide.transform.GetChild(0))
        {
            trail.Add(point.position);
        }

        List<Vector3> route = new List<Vector3>();
        int[] bridgeFirst = new int[4];
        int[] bridgeLast = new int[4];
        int cursor = 0;
        for (int g = 0; g < 4; g++)
        {
            Vector3[] points = (Vector3[])Member(bridges[g], "Points");
            if (g < 3)
            {
                int startIndex = NearestOnTrail(trail, points[0], cursor);
                int endIndex = NearestOnTrail(trail, points[points.Length - 1], startIndex + 1);
                for (int i = cursor; i < startIndex; i++)
                {
                    route.Add(trail[i]);
                }

                cursor = endIndex + 1;
            }
            else
            {
                for (int i = cursor; i < trail.Count; i++)
                {
                    route.Add(trail[i]);
                }
            }

            bridgeFirst[g] = route.Count;
            route.AddRange(points);
            bridgeLast[g] = route.Count - 1;
        }

        Vector3 lampPosition = ((Component)lamp).transform.position;
        route.Add(lampPosition);
        float length = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            length += Vector3.Distance(route[i - 1], route[i]);
        }

        SetStatic("CameraFollow", "ExternalYaw", 0f);
        SetStatic("CameraFollow", "UseExternalYaw", true);
        Time.maximumDeltaTime = 0.05f;
        Time.captureDeltaTime = FixedFrameSeconds;
        Time.timeScale = PlaybackScale;

        float limit = length / 3f + 180f;
        float elapsed = 0f;
        int waypoint = 1;
        bool[] lit = new bool[8];
        float holdSince = -1f;
        float firstBeaconAt = -1f;
        float minGap = 999f;
        float minGapAt = 0f;
        float minGapHeight = 0f;
        float tightestOpen = 999f;
        float sea = GameObject.Find("Water").transform.position.y;
        float minFuel = float.MaxValue;
        bool caughtEver = false;
        bool atLamp = false;
        string error = null;
        float maxFuel = (float)Member(lantern, "MaxFuel");
        while (elapsed < limit && error == null)
        {
            float fuel = (float)Member(lantern, "Fuel");
            minFuel = Mathf.Min(minFuel, fuel);
            if ((bool)Member(surge, "Started") && !(bool)Member(surge, "Stopped"))
            {
                float gap = (float)Member(surge, "Gap");
                if (gap < 900f)
                {
                    if (gap < minGap)
                    {
                        minGap = gap;
                        minGapAt = elapsed;
                        minGapHeight = player.transform.position.y - sea;
                    }
                }

                if (gap < 900f && player.transform.position.y - sea > 10f)
                {
                    tightestOpen = Mathf.Min(tightestOpen, gap);
                }

                caughtEver |= (bool)Member(surge, "IsCaught");
            }

            Vector3 position = player.transform.position;
            Vector2 flat = new Vector2(position.x, position.z);
            if ((bool)Member(manager, "IsDying") || (bool)Member(manager, "IsRoundOver"))
            {
                error = "the round ended before the lamp at " + position.ToString("F1") + ", fuel " + fuel.ToString("F1") + ", surge gap " + minGap.ToString("F1") + ", caught " + caughtEver;
                break;
            }

            for (int b = 0; b < 8; b++)
            {
                if (lit[b])
                {
                    continue;
                }

                Transform beacon = beaconsRoot.GetChild(b);
                if (Vector2.Distance(flat, new Vector2(beacon.position.x, beacon.position.z)) < ArriveRadius)
                {
                    if (!(bool)Invoke(beacon.GetComponent(GameType("Beacon")), "TryLight"))
                    {
                        error = "beacon " + (b + 1) + " could not be lit at " + position.ToString("F1") + ", fuel " + fuel.ToString("F1");
                        break;
                    }

                    lit[b] = true;
                    if (firstBeaconAt < 0f)
                    {
                        firstBeaconAt = elapsed;
                    }
                }
            }

            if (error != null)
            {
                break;
            }

            int gate = -1;
            for (int g = 0; g < 4; g++)
            {
                if (waypoint >= bridgeFirst[g] && waypoint <= bridgeLast[g] && !(bool)Member(bridges[g], "Walkable"))
                {
                    gate = g;
                    break;
                }
            }

            if (gate >= 0)
            {
                object near = Member(bridges[gate], "NearBeacon");
                if (near != null && !(bool)Member(near, "IsLit"))
                {
                    Invoke(near, "TryLight");
                }

                if (holdSince < 0f)
                {
                    holdSince = elapsed;
                }

                if (elapsed - holdSince > (gate == 3 ? 45f : 12f))
                {
                    error = "bridge " + gate + " did not become walkable";
                    break;
                }

                SetStatic("PlayerController", "ExternalMove", Vector2.zero);
                yield return null;
                elapsed += Time.deltaTime;
                continue;
            }

            holdSince = -1f;
            if (waypoint == route.Count - 1)
            {
                if (Vector3.Distance(position, lampPosition) <= 3f)
                {
                    atLamp = true;
                    break;
                }
            }
            else
            {
                while (waypoint < route.Count - 1 && Vector2.Distance(flat, new Vector2(route[waypoint].x, route[waypoint].z)) < WaypointRadius)
                {
                    waypoint++;
                }
            }

            Vector3 target = route[waypoint];
            Vector2 direction = new Vector2(target.x - position.x, target.z - position.z);
            SetStatic("PlayerController", "ExternalMove", direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero);
            yield return null;
            elapsed += Time.deltaTime;
        }

        SetStatic("PlayerController", "ExternalMove", Vector2.zero);
        float lampAt = elapsed;
        object hunter = UnityEngine.Object.FindAnyObjectByType(GameType("LightningHunter"), FindObjectsInactive.Include);
        int strikes = hunter != null ? (int)Member(hunter, "Strikes") : -1;
        int hits = hunter != null ? (int)Member(hunter, "Hits") : -1;
        int skipped = hunter != null ? (int)Member(hunter, "Skipped") : -1;
        string highRing = GameType("RingZones").GetProperty("Reached", BindingFlags.Public | BindingFlags.Static).GetValue(null).ToString();
        float fuelAtLamp = (float)Member(lantern, "Fuel");
        string summary = "Island5Balance SteadyBot: lamp reached at " + lampAt.ToString("F1") + " s (" + (lampAt - firstBeaconAt).ToString("F1") + " s after the first beacon), "
            + "min surge gap " + minGap.ToString("F1") + " m (at " + minGapAt.ToString("F1") + " s, keeper " + minGapHeight.ToString("F1") + " m up), tightest above 10 m up " + tightestOpen.ToString("F1") + " m, min fuel " + minFuel.ToString("F1") + "/" + maxFuel.ToString("F0") + " (at the lamp " + fuelAtLamp.ToString("F1") + "), "
            + strikes + " strikes, " + hits + " hits, " + skipped + " dropped for shelter, high ring reached " + highRing + ", caught " + caughtEver + ", route " + length.ToString("F1") + " m";
        Debug.Log(summary);
        Assert.IsNull(error, error);
        Assert.IsTrue(atLamp, "the bot did not reach the lamp in " + limit.ToString("F0") + " s. " + summary);
        Assert.IsFalse(caughtEver, "the surge caught the bot. " + summary);
        Assert.Greater(minGap, 0f, summary);
        Assert.Greater(minFuel, 0f, "the bot's fuel ran out. " + summary);
        Assert.IsTrue((bool)Invoke(lamp, "TryLight"), "the lamp refused to light. " + summary);
        Assert.IsEmpty(problems, "errors were logged: " + string.Join(" | ", problems));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator IdleBotIsCaught()
    {
        yield return Load();
        object surge = UnityEngine.Object.FindAnyObjectByType(GameType("StormSurge"));
        object lantern = UnityEngine.Object.FindAnyObjectByType(GameType("Lantern"));
        object manager = UnityEngine.Object.FindAnyObjectByType(GameType("GameManager"));
        Assert.IsNotNull(surge);
        Time.maximumDeltaTime = 0.05f;
        Time.captureDeltaTime = FixedFrameSeconds;
        Time.timeScale = PlaybackScale;
        Transform first = GameObject.Find("Beacons").transform.GetChild(0);
        Assert.IsTrue((bool)Invoke(first.GetComponent(GameType("Beacon")), "TryLight"), "the first beacon lights");

        float waited = 0f;
        float caughtAt = -1f;
        while (waited < 60f)
        {
            yield return null;
            waited += Time.deltaTime;
            if ((bool)Member(surge, "IsCaught"))
            {
                caughtAt = waited;
                break;
            }

            if ((bool)Member(manager, "IsDying") || (bool)Member(manager, "IsRoundOver"))
            {
                break;
            }
        }

        Debug.Log("Island5Balance IdleBot: caught at " + caughtAt.ToString("F1") + " s after the first beacon, fuel " + ((float)Member(lantern, "Fuel")).ToString("F1"));
        Assert.Greater(caughtAt, 0f, "the surge never caught a keeper waiting at the beach");
        Assert.Less(caughtAt, 60f);
        Assert.IsEmpty(problems, "errors were logged: " + string.Join(" | ", problems));
    }
}
}
