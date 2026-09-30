using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Opt-in only. A player or editor launched without -lkperf never starts this probe.
// -lkscene=Island1|Island2 loads that scene. -lkseconds sets the walk sample (default 20).
// -lkshot=<path> writes one PNG of a fixed pose. -lkview=spawn|beacon picks the pose.
// -lkreport=<path> writes the result. -lkvsync is forced off for the run and is not saved.
[DefaultExecutionOrder(-500)]
public class LK_PerfProbe : MonoBehaviour
{
    const string Arg = "-lkperf";
    const float WarmupSeconds = 3f;

    readonly List<float> samples = new List<float>(65536);
    readonly List<long> triangleSamples = new List<long>(65536);
    readonly FrameTiming[] timings = new FrameTiming[1];
    readonly List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>(512);

    ProfilerRecorder gcAlloc;
    ProfilerRecorder triangles;
    ProfilerRecorder setPass;
    ProfilerRecorder shadowCasters;
    ProfilerRecorder gpuTime;
    ProfilerRecorder[] batchRecorders = new ProfilerRecorder[4];
    int batchRecorderCount;

    double sampleSum;
    float sampleMin = float.MaxValue;
    float sampleMax;
    double gcSum;
    int gcFrames;
    long gcMin = long.MaxValue;
    long gcMax;
    double cpuSum;
    int cpuFrames;
    double gpuSum;
    int gpuFrames;
    int gpuZeroFrames;
    double gpuRecorderSum;
    int gpuRecorderFrames;
    double triangleSum;
    double setPassSum;
    double batchSum;
    double shadowSum;
    int counterFrames;

    string sceneName = "Island1";
    string shotPath;
    string reportPath;
    string view = "spawn";
    string statNames = "";
    string gpuRecorderName = "";
    string appliedNote = "";
    float sampleSeconds = 20f;
    float elapsed;
    bool walking;
    bool sampling;
    bool finished;
    bool gpuIsNanoseconds = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!HasFlag(Arg))
        {
            return;
        }

        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        if (UnityEngine.Object.FindAnyObjectByType<LK_PerfProbe>() != null)
        {
            return;
        }

        GameObject host = new GameObject("LK_PerfProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<LK_PerfProbe>();
    }

    void Start()
    {
        string sceneArg = ArgValue("-lkscene");
        if (!string.IsNullOrEmpty(sceneArg))
        {
            sceneName = sceneArg;
        }

        shotPath = ArgValue("-lkshot");
        reportPath = ArgValue("-lkreport");
        string viewArg = ArgValue("-lkview");
        if (!string.IsNullOrEmpty(viewArg))
        {
            view = viewArg;
        }

        string secondsArg = ArgValue("-lkseconds");
        float parsedSeconds;
        if (!string.IsNullOrEmpty(secondsArg) && float.TryParse(secondsArg, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedSeconds))
        {
            sampleSeconds = Mathf.Max(0f, parsedSeconds);
        }

        Time.maximumDeltaTime = 0.05f;
        OpenRecorders();
        string levelName = GraphicsQuality.Current.ToString();
        Debug.Log("LK_PerfProbe start scene=" + sceneName
            + " level=" + levelName
            + " view=" + view
            + " seconds=" + sampleSeconds.ToString("0.0")
            + " vsync=0"
            + " stats=" + statNames);
        StartCoroutine(Run());
    }

    void Update()
    {
        QualitySettings.vSyncCount = 0;
        if (finished || SceneManager.GetActiveScene().name != sceneName)
        {
            return;
        }

        if (walking)
        {
            PlayerController.ExternalMove = new Vector2(0f, 1f);
            PlayerController.ExternalSprint = false;
            CameraFollow.UseExternalYaw = true;
        }

        if (!sampling)
        {
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        RecordFrame();
    }

    IEnumerator Run()
    {
        if (SceneManager.GetActiveScene().name != sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        while (SceneManager.GetActiveScene().name != sceneName)
        {
            yield return null;
        }

        float warm = 0f;
        while (warm < WarmupSeconds)
        {
            warm += Time.unscaledDeltaTime;
            yield return null;
        }

        if (string.Equals(view, "beacon", StringComparison.OrdinalIgnoreCase))
        {
            yield return CaptureBeacon();
        }
        else
        {
            yield return CaptureSpawn();
        }

        CaptureApplied();
        if (sampleSeconds > 0.01f)
        {
            LockWalkYaw();
            elapsed = 0f;
            walking = true;
            sampling = true;
            while (elapsed < sampleSeconds)
            {
                yield return null;
            }

            sampling = false;
            walking = false;
            PlayerController.ExternalMove = Vector2.zero;
        }

        finished = true;
        WriteReport();
        Application.Quit();
    }

    IEnumerator CaptureSpawn()
    {
        PlayerController keeper = FindAnyObjectByType<PlayerController>();
        CameraFollow follow = FindAnyObjectByType<CameraFollow>();
        if (follow != null)
        {
            follow.SnapBehind();
        }

        LockWalkYaw();
        float settle = 0f;
        while (settle < 0.6f)
        {
            settle += Time.unscaledDeltaTime;
            yield return null;
        }

        if (keeper != null)
        {
            CameraFollow.ExternalYaw = keeper.transform.eulerAngles.y;
        }

        yield return new WaitForEndOfFrame();
        SaveShot();
    }

    IEnumerator CaptureBeacon()
    {
        Beacon beacon = null;
        Renderer rock = null;
        ChooseBeaconAndRock(out beacon, out rock);
        bool lit = false;
        if (beacon != null)
        {
            lit = beacon.TryLight();
            if (!lit)
            {
                ForceBeaconLight(beacon);
            }
        }

        float settle = 0f;
        while (settle < 2.4f)
        {
            settle += Time.unscaledDeltaTime;
            yield return null;
        }

        CameraFollow follow = FindAnyObjectByType<CameraFollow>();
        if (follow != null)
        {
            follow.enabled = false;
        }

        Camera camera = Camera.main;
        if (camera != null && beacon != null)
        {
            PlaceBeaconCamera(camera, beacon, rock);
        }

        Debug.Log("LK_PerfProbe beacon=" + (beacon != null ? beacon.name : "none")
            + " lit=" + (beacon != null && (lit || beacon.IsLit))
            + " rock=" + (rock != null ? rock.name : "none"));
        yield return null;
        yield return new WaitForEndOfFrame();
        SaveShot();
    }

    void RecordFrame()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        samples.Add(ms);
        sampleSum += ms;
        if (ms < sampleMin)
        {
            sampleMin = ms;
        }

        if (ms > sampleMax)
        {
            sampleMax = ms;
        }

        if (gcAlloc.Valid)
        {
            long bytes = gcAlloc.LastValue;
            gcSum += bytes;
            gcFrames++;
            if (bytes < gcMin)
            {
                gcMin = bytes;
            }

            if (bytes > gcMax)
            {
                gcMax = bytes;
            }
        }

        FrameTimingManager.CaptureFrameTimings();
        uint timingCount = FrameTimingManager.GetLatestTimings(1, timings);
        if (timingCount > 0)
        {
            double cpu = timings[0].cpuFrameTime;
            double gpu = timings[0].gpuFrameTime;
            if (cpu > 0d)
            {
                cpuSum += cpu;
                cpuFrames++;
            }

            if (gpu > 0d)
            {
                gpuSum += gpu;
                gpuFrames++;
            }
            else
            {
                gpuZeroFrames++;
            }
        }

        if (gpuTime.Valid)
        {
            double recorderMs = gpuTime.LastValueAsDouble;
            if (gpuIsNanoseconds)
            {
                recorderMs /= 1000000d;
            }

            if (recorderMs > 0d)
            {
                gpuRecorderSum += recorderMs;
                gpuRecorderFrames++;
            }
        }

        counterFrames++;
        if (triangles.Valid)
        {
            triangleSum += triangles.LastValue;
            triangleSamples.Add(triangles.LastValue);
        }

        if (setPass.Valid)
        {
            setPassSum += setPass.LastValue;
        }

        if (shadowCasters.Valid)
        {
            shadowSum += shadowCasters.LastValue;
        }

        if (batchRecorderCount > 0)
        {
            long batch = 0;
            for (int i = 0; i < batchRecorderCount; i++)
            {
                if (batchRecorders[i].Valid)
                {
                    batch += batchRecorders[i].LastValue;
                }
            }

            batchSum += batch;
        }
    }

    void WriteReport()
    {
        int count = samples.Count;
        float avg = count > 0 ? (float)(sampleSum / count) : 0f;
        float p95 = Percentile(0.95f);
        if (count == 0)
        {
            sampleMin = 0f;
        }

        double gcAvg = gcFrames > 0 ? gcSum / gcFrames : 0d;
        double cpuMs = cpuFrames > 0 ? cpuSum / cpuFrames : 0d;
        double gpuMs = gpuFrames > 0 ? gpuSum / gpuFrames : 0d;
        double gpuRecMs = gpuRecorderFrames > 0 ? gpuRecorderSum / gpuRecorderFrames : 0d;
        long tri = counterFrames > 0 ? (long)(triangleSum / counterFrames) : -1;
        long pass = counterFrames > 0 ? (long)(setPassSum / counterFrames) : -1;
        long batches = counterFrames > 0 && batchRecorderCount > 0 ? (long)(batchSum / counterFrames) : -1;
        long casters = counterFrames > 0 ? (long)(shadowSum / counterFrames) : -1;
        string levelName = GraphicsQuality.Current.ToString();
        string gpuNote = gpuMs > 0.01d
            ? "gpu-frame-timing"
            : "GPU time read 0; avgMs is vsync-off wall-clock frame time (CPU+GPU bound)";
        string report = "LK_PerfProbe scene=" + sceneName
            + " level=" + levelName
            + " qualityLevel=" + QualitySettings.GetQualityLevel()
            + " view=" + view
            + " res=" + Screen.width + "x" + Screen.height
            + " fullscreen=" + Screen.fullScreen
            + " vsync=" + QualitySettings.vSyncCount
            + " samples=" + count
            + " avgMs=" + avg.ToString("0.00")
            + " minMs=" + sampleMin.ToString("0.00")
            + " maxMs=" + sampleMax.ToString("0.00")
            + " p95Ms=" + p95.ToString("0.00")
            + " fps=" + (avg > 0.01f ? (1000f / avg).ToString("0.0") : "0")
            + " gcAvgBytes=" + gcAvg.ToString("0")
            + " gcMin=" + (gcFrames > 0 ? gcMin.ToString() : "n/a")
            + " gcMax=" + gcMax
            + " cpuFrameMs=" + cpuMs.ToString("0.00")
            + " gpuFrameMs=" + gpuMs.ToString("0.00")
            + " gpuFrames=" + gpuFrames
            + " gpuZeroFrames=" + gpuZeroFrames
            + " gpuRecorderMs=" + gpuRecMs.ToString("0.00")
            + " gpuRecorderFrames=" + gpuRecorderFrames
            + " triangles=" + tri
            + " triMedian=" + MedianLong(triangleSamples)
            + " applied=" + appliedNote
            + " setPass=" + pass
            + " batches=" + batches
            + " shadowCasters=" + casters
            + " stats=" + statNames
            + " gpuStat=" + gpuRecorderName
            + " note=" + gpuNote;
        if (string.IsNullOrEmpty(reportPath))
        {
            string folder = Directory.GetParent(Application.dataPath).FullName;
            reportPath = Path.Combine(folder, "lkperf_" + sceneName + "_" + levelName + ".txt");
        }

        string folderPath = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        File.WriteAllText(reportPath, report);
        Debug.Log(report);
    }

    void CaptureApplied()
    {
        Terrain terrain = FindAnyObjectByType<Terrain>();
        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        appliedNote = "shadowDist=" + QualitySettings.shadowDistance.ToString("0")
            + " lodBias=" + QualitySettings.lodBias.ToString("0.00");
        if (pipeline != null)
        {
            appliedNote += " renderScale=" + pipeline.renderScale.ToString("0.00");
        }

        if (terrain != null)
        {
            appliedNote += " grassDens=" + terrain.detailObjectDensity.ToString("0.00")
                + " grassDist=" + terrain.detailObjectDistance.ToString("0")
                + " pixelErr=" + terrain.heightmapPixelError.ToString("0");
        }
    }

    static long MedianLong(List<long> values)
    {
        int count = values.Count;
        if (count <= 0)
        {
            return -1;
        }

        long[] copy = values.ToArray();
        Array.Sort(copy);
        return copy[count / 2];
    }

    float Percentile(float rank)
    {
        int count = samples.Count;
        if (count <= 0)
        {
            return 0f;
        }

        float[] copy = samples.ToArray();
        Array.Sort(copy);
        int index = Mathf.Clamp(Mathf.CeilToInt(rank * count) - 1, 0, count - 1);
        return copy[index];
    }

    void OpenRecorders()
    {
        handles.Clear();
        ProfilerRecorderHandle.GetAvailable(handles);
        gcAlloc = OpenExact("Memory", "GC Allocated In Frame");
        triangles = OpenExact("Render", "Triangles Count");
        setPass = OpenExact("Render", "SetPass Calls Count");
        shadowCasters = OpenExact("Render", "Shadow Casters Count");
        StringBuilder names = new StringBuilder();
        AppendName(names, "Triangles Count", triangles);
        AppendName(names, "SetPass Calls Count", setPass);
        AppendName(names, "Shadow Casters Count", shadowCasters);
        ProfilerRecorder srpDraws = OpenExact("Render", "SRP Batcher Draw Calls Count");
        ProfilerRecorder batches = OpenExact("Render", "Batches Count");
        if (srpDraws.Valid)
        {
            batchRecorders[0] = srpDraws;
            batchRecorderCount = 1;
            AppendName(names, "SRP Batcher Draw Calls Count", srpDraws);
        }
        else if (batches.Valid)
        {
            batchRecorders[0] = batches;
            batchRecorderCount = 1;
            AppendName(names, "Batches Count", batches);
        }
        else
        {
            AddBatch("Dynamic Batches Count", names);
            AddBatch("Static Batches Count", names);
            AddBatch("Instanced Batches Count", names);
        }

        gpuTime = OpenExact("Render", "GPU Frame Time");
        if (gpuTime.Valid)
        {
            gpuRecorderName = "GPU Frame Time";
            gpuIsNanoseconds = gpuTime.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
        }
        else
        {
            gpuTime = OpenExact("Render", "FrameTime.GPU");
            if (gpuTime.Valid)
            {
                gpuRecorderName = "FrameTime.GPU";
                gpuIsNanoseconds = gpuTime.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
            }
        }

        statNames = names.ToString();
    }

    void AddBatch(string name, StringBuilder names)
    {
        ProfilerRecorder recorder = OpenExact("Render", name);
        if (!recorder.Valid || batchRecorderCount >= batchRecorders.Length)
        {
            return;
        }

        batchRecorders[batchRecorderCount] = recorder;
        batchRecorderCount++;
        AppendName(names, name, recorder);
    }

    ProfilerRecorder OpenExact(string category, string name)
    {
        for (int i = 0; i < handles.Count; i++)
        {
            ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handles[i]);
            if (description.Name == name && description.Category.ToString() == category)
            {
                return ProfilerRecorder.StartNew(description.Category, description.Name);
            }
        }

        return default(ProfilerRecorder);
    }

    static void AppendName(StringBuilder names, string name, ProfilerRecorder recorder)
    {
        if (names.Length > 0)
        {
            names.Append(",");
        }

        names.Append(name);
        names.Append(recorder.Valid ? "=ok" : "=missing");
    }

    void SaveShot()
    {
        if (string.IsNullOrEmpty(shotPath))
        {
            return;
        }

        string folderPath = Path.GetDirectoryName(shotPath);
        if (!string.IsNullOrEmpty(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        if (texture == null)
        {
            Debug.LogError("LK_PerfProbe screenshot failed " + shotPath);
            return;
        }

        byte[] png = texture.EncodeToPNG();
        Destroy(texture);
        File.WriteAllBytes(shotPath, png);
        Debug.Log("LK_PerfProbe shot=" + shotPath + " bytes=" + png.Length + " level=" + GraphicsQuality.Current);
    }

    static void LockWalkYaw()
    {
        CameraFollow.UseExternalYaw = true;
        PlayerController keeper = FindAnyObjectByType<PlayerController>();
        if (keeper != null)
        {
            CameraFollow.ExternalYaw = keeper.transform.eulerAngles.y;
        }
    }

    static void ChooseBeaconAndRock(out Beacon beacon, out Renderer rock)
    {
        beacon = null;
        rock = null;
        Beacon[] beacons = FindObjectsByType<Beacon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float best = float.MaxValue;
        for (int i = 0; i < beacons.Length; i++)
        {
            Beacon candidate = beacons[i];
            if (candidate == null)
            {
                continue;
            }

            Renderer near = NearestRock(renderers, candidate.transform.position, 18f);
            if (near == null)
            {
                if (beacon == null)
                {
                    beacon = candidate;
                }

                continue;
            }

            float distance = Vector3.Distance(candidate.transform.position, near.bounds.center);
            if (distance < best)
            {
                best = distance;
                beacon = candidate;
                rock = near;
            }
        }
    }

    static Renderer NearestRock(Renderer[] renderers, Vector3 origin, float maxDistance)
    {
        Renderer best = null;
        float bestDistance = maxDistance;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !LooksLikeRock(renderer.name))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, renderer.bounds.center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = renderer;
            }
        }

        return best;
    }

    static bool LooksLikeRock(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("boulder") || lower.Contains("rock") || lower.Contains("cliff") || lower.Contains("stone");
    }

    static void PlaceBeaconCamera(Camera camera, Beacon beacon, Renderer rock)
    {
        Vector3 beaconPos = beacon.transform.position;
        Vector3 rockPos = rock != null ? rock.bounds.center : beaconPos + Vector3.forward * 4f;
        Vector3 flat = rockPos - beaconPos;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.04f)
        {
            flat = Vector3.forward;
        }

        flat.Normalize();
        Vector3 cameraPos = rockPos + flat * 3.4f;
        cameraPos.y = rockPos.y + 0.15f;
        float ground = beaconPos.y + 0.45f;
        if (cameraPos.y < ground)
        {
            cameraPos.y = ground;
        }

        Vector3 look = Vector3.Lerp(beaconPos, rockPos, 0.45f) + Vector3.up * 0.35f;
        camera.transform.position = cameraPos;
        camera.transform.rotation = Quaternion.LookRotation((look - cameraPos).normalized, Vector3.up);
    }

    static void ForceBeaconLight(Beacon beacon)
    {
        Light[] lights = beacon.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].name == "BeaconLight")
            {
                lights[i].enabled = true;
                lights[i].intensity = 3.4f;
                lights[i].range = 16f;
            }
        }
    }

    static bool HasFlag(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == key)
            {
                return true;
            }
        }

        return false;
    }

    static string ArgValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        string prefix = key + "=";
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                return arg.Substring(prefix.Length).Trim('"');
            }

            if (arg == key && i + 1 < args.Length)
            {
                return args[i + 1].Trim('"');
            }
        }

        return null;
    }

    void OnDestroy()
    {
        DisposeRecorder(gcAlloc);
        DisposeRecorder(triangles);
        DisposeRecorder(setPass);
        DisposeRecorder(shadowCasters);
        DisposeRecorder(gpuTime);
        for (int i = 0; i < batchRecorderCount; i++)
        {
            DisposeRecorder(batchRecorders[i]);
        }
    }

    static void DisposeRecorder(ProfilerRecorder recorder)
    {
        if (recorder.Valid)
        {
            recorder.Dispose();
        }
    }
}
}
