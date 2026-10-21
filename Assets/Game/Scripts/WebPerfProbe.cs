#if LK_WEBPROBE && UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Measurement-only probe for the WebGL player, compiled only when the LK_WEBPROBE scripting define is set (the build helper
// passes it as an extra define; it is never in PlayerSettings, so shipping builds do not contain this class).
// URL query: ?scene=Island5 loads that scene after boot; ?bot=1 walks the keeper to the nearest unlit beacon and lights it.
// It logs, once every few seconds, one batched line per hitch frame (over 33 ms) with the frame's context: scene, GC count
// delta, mono heap allocated that frame, lit beacons; plus a periodic summary with the average allocation per frame.
[DefaultExecutionOrder(-100)]
public class WebPerfProbe : MonoBehaviour
{
    const float HitchSeconds = 0.033f;
    const float FlushSeconds = 5f;

    readonly StringBuilder buffer = new StringBuilder(2048);
    string forceScene = "";
    bool bot;
    bool roam;
    int roamIndex;
    bool sceneForced;
    float startTime;
    float flushAt;
    int gcLast;
    long monoLast;
    long allocWindow;
    int framesWindow;
    int hitchWindow;
    int gcWindow;
    float worst;
    PlayerController player;
    Camera cam;
    Beacon target;
    float targetSince;
    Vector3 lastPosition;
    float lastPositionTime;
    float unstickUntil;
    Vector2 unstickDir;
    int litCount;
    Lantern lantern;
    string botNote = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<WebPerfProbe>() != null)
        {
            return;
        }

        GameObject host = new GameObject("WebPerfProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<WebPerfProbe>();
    }

    void Start()
    {
        string url = Application.absoluteURL ?? "";
        forceScene = Query(url, "scene");
        string botArg = Query(url, "bot");
        bot = botArg == "1" || botArg == "roam";
        roam = botArg == "roam";
        startTime = Time.unscaledTime;
        flushAt = startTime + FlushSeconds;
        gcLast = GC.CollectionCount(0);
        monoLast = Profiler.GetMonoUsedSizeLong();
        // ?terrainInst=1 turns the WebTerrainDraw fix off (instanced terrain) so one build serves both sides of the comparison.
        if (Query(url, "terrainInst") == "1")
        {
            WebTerrainDraw.Disabled = true;
            for (int i = 0; i < Terrain.activeTerrains.Length; i++)
            {
                Terrain.activeTerrains[i].drawInstanced = true;
            }
        }

        WebShaderPrewarm.Disabled = Query(url, "prewarm") == "0";
        SceneManager.sceneLoaded += OnSceneLoaded;
        Beacon.Lit += OnBeaconLit;
        Debug.Log("LKP start url=" + url + " scene=" + SceneManager.GetActiveScene().name + " bot=" + bot);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Beacon.Lit -= OnBeaconLit;
    }

    static string Query(string url, string key)
    {
        int q = url.IndexOf('?');
        if (q < 0)
        {
            return "";
        }

        string[] parts = url.Substring(q + 1).Split('&');
        for (int i = 0; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq > 0 && parts[i].Substring(0, eq) == key)
            {
                return parts[i].Substring(eq + 1);
            }
        }

        return "";
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        target = null;
        cam = null;
        lantern = null;
        litCount = 0;
        buffer.Append("LKP scene ").Append(scene.name).Append(" t=").Append((Time.unscaledTime - startTime).ToString("0.00")).Append('\n');
    }

    void OnBeaconLit(Beacon beacon)
    {
        litCount++;
        buffer.Append("LKP lit ").Append(beacon != null ? beacon.name : "?").Append(" t=").Append((Time.unscaledTime - startTime).ToString("0.00")).Append('\n');
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        int gc = GC.CollectionCount(0);
        long mono = Profiler.GetMonoUsedSizeLong();
        long alloc = mono - monoLast;
        framesWindow++;
        if (alloc > 0)
        {
            allocWindow += alloc;
        }

        int gcDelta = gc - gcLast;
        if (gcDelta > 0)
        {
            gcWindow += gcDelta;
        }

        if (dt > worst)
        {
            worst = dt;
        }

        if (dt > HitchSeconds)
        {
            hitchWindow++;
            buffer.Append("LKP hitch dt=").Append((dt * 1000f).ToString("0.0"))
                .Append(" gcDelta=").Append(gcDelta)
                .Append(" allocKB=").Append((alloc / 1024f).ToString("0"))
                .Append(" scene=").Append(SceneManager.GetActiveScene().name)
                .Append(" lit=").Append(litCount)
                .Append(" t=").Append((Time.unscaledTime - startTime).ToString("0.00")).Append('\n');
        }
        else if (gcDelta > 0)
        {
            buffer.Append("LKP gc-quiet gcDelta=").Append(gcDelta).Append(" dt=").Append((dt * 1000f).ToString("0.0")).Append('\n');
        }

        gcLast = gc;
        monoLast = mono;

        if (!sceneForced && forceScene.Length > 0 && Time.unscaledTime - startTime > 1f)
        {
            sceneForced = true;
            if (SceneManager.GetActiveScene().name != forceScene)
            {
                SceneManager.LoadScene(forceScene);
            }
        }

        if (bot)
        {
            BotTick();
        }

        if (Time.unscaledTime >= flushAt)
        {
            flushAt = Time.unscaledTime + FlushSeconds;
            buffer.Append("LKP sum frames=").Append(framesWindow)
                .Append(" hitches=").Append(hitchWindow)
                .Append(" gc=").Append(gcWindow)
                .Append(" avgAllocKB=").Append(framesWindow > 0 ? (allocWindow / 1024f / framesWindow).ToString("0.00") : "0")
                .Append(" worstMs=").Append((worst * 1000f).ToString("0.0"))
                .Append(" monoMB=").Append((mono / 1048576f).ToString("0.0"))
                .Append(" scene=").Append(SceneManager.GetActiveScene().name)
                .Append(" lit=").Append(litCount).Append(" bot=[").Append(botNote).Append("]");
            framesWindow = 0;
            allocWindow = 0;
            hitchWindow = 0;
            gcWindow = 0;
            worst = 0f;
            Debug.Log(buffer.ToString());
            buffer.Length = 0;
        }
    }

    void BotTick()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (!scene.StartsWith("Island", StringComparison.Ordinal))
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager == null || manager.IsPaused || manager.ControlsLocked || manager.IsDying || manager.IsRoundOver)
        {
            PlayerController.ExternalMove = Vector2.zero;
            return;
        }

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (lantern != null)
        {
            // Measurement only: keep the flame alive so the minute is spent playing, not on the lose screen.
            lantern.DrainFrozen = true;
            if (lantern.FuelNormalized < 0.5f)
            {
                lantern.AddFuel(lantern.MaxFuel * 0.5f);
            }
        }

        if (player == null || cam == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (target == null || target.IsLit)
        {
            target = null;
            float best = float.MaxValue;
            for (int i = 0; i < Beacon.All.Count; i++)
            {
                Beacon b = Beacon.All[i];
                if (b != null && !b.IsLit && b.isActiveAndEnabled)
                {
                    float d = (b.transform.position - player.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        target = b;
                    }
                }
            }

            targetSince = now;
        }

        Vector3 flatForward = cam.transform.forward;
        flatForward.y = 0f;
        flatForward.Normalize();
        Vector3 flatRight = cam.transform.right;
        flatRight.y = 0f;
        flatRight.Normalize();

        Vector2 move;
        if (now < unstickUntil)
        {
            move = unstickDir;
        }
        else if (target == null)
        {
            // Nothing left to light: wander in a slow circle.
            float a = now * 0.6f;
            move = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        else
        {
            if (roam && Beacon.All.Count > 0)
            {
                // Roam: visit every beacon in turn without lighting, to keep the camera moving over the whole island.
                Beacon stop = Beacon.All[roamIndex % Beacon.All.Count];
                if ((stop.transform.position - player.transform.position).magnitude < 6f)
                {
                    roamIndex++;
                    stop = Beacon.All[roamIndex % Beacon.All.Count];
                }

                target = stop;
            }

            Vector3 to = target.transform.position - player.transform.position;
            to.y = 0f;
            botNote = target.name + " dist=" + to.magnitude.ToString("0.0") + " pos=" + player.transform.position.ToString("F0");
            if (!roam && to.magnitude < 5.5f)
            {
                target.TryLight();
                PlayerController.ExternalMove = Vector2.zero;
                return;
            }

            Vector3 dir = to.normalized;
            move = new Vector2(Vector3.Dot(dir, flatRight), Vector3.Dot(dir, flatForward));
        }

        if (now - lastPositionTime > 3f)
        {
            Vector3 p = player.transform.position;
            if ((p - lastPosition).magnitude < 1.5f && now >= unstickUntil)
            {
                unstickUntil = now + 1.5f;
                unstickDir = UnityEngine.Random.value > 0.5f ? new Vector2(1f, 0.3f) : new Vector2(-1f, 0.3f);
                PlayerController.ExternalJump = true;
                if (now - targetSince > 25f)
                {
                    target = null;
                }
            }

            lastPosition = p;
            lastPositionTime = now;
        }

        PlayerController.ExternalMove = move;
    }
}
}
#endif
