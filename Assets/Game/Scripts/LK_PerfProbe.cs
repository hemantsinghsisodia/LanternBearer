using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// Opt-in only. A player or editor launched without -lkperf never starts this probe.
public class LK_PerfProbe : MonoBehaviour
{
    const string Arg = "-lkperf";
    const float WarmupSeconds = 3f;
    const float SampleSeconds = 30f;

    FrameTiming[] timings;
    float[] samples;
    int sampleCount;
    float elapsed;
    bool sampling;
    bool started;
    ProfilerRecorder gcAlloc;
    ProfilerRecorder drawCalls;
    ProfilerRecorder setPass;
    ProfilerRecorder triangles;
    double gcSum;
    int gcFrames;
    long gcMin = long.MaxValue;
    long gcMax;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled())
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<LK_PerfProbe>() != null)
        {
            return;
        }

        GameObject host = new GameObject("LK_PerfProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<LK_PerfProbe>();
    }

    static bool Enabled()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == Arg)
            {
                return true;
            }
        }

        return false;
    }

    void Start()
    {
        timings = new FrameTiming[1];
        samples = new float[4096];
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        if (SceneManager.GetActiveScene().name != "Island1")
        {
            SceneManager.LoadScene("Island1");
        }
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != "Island1")
        {
            return;
        }

        PlayerController.ExternalMove = new Vector2(0f, 1f);
        PlayerController.ExternalSprint = false;
        elapsed += Time.unscaledDeltaTime;
        if (!started)
        {
            if (elapsed < WarmupSeconds)
            {
                return;
            }

            started = true;
            sampling = true;
            elapsed = 0f;
            return;
        }

        if (!sampling)
        {
            return;
        }

        RecordFrame();
        if (elapsed < SampleSeconds)
        {
            return;
        }

        sampling = false;
        PlayerController.ExternalMove = Vector2.zero;
        WriteReport();
        Application.Quit();
    }

    void RecordFrame()
    {
        if (sampleCount >= samples.Length)
        {
            return;
        }

        samples[sampleCount] = Time.unscaledDeltaTime * 1000f;
        sampleCount++;
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
    }

    void WriteReport()
    {
        float avg = 0f;
        float min = float.MaxValue;
        float max = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float value = samples[i];
            avg += value;
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        if (sampleCount > 0)
        {
            avg /= sampleCount;
        }

        float p95 = Percentile(0.95f);
        double gcAvg = gcFrames > 0 ? gcSum / gcFrames : 0d;
        uint timingCount = FrameTimingManager.GetLatestTimings(1, timings);
        double cpuMs = timingCount > 0 ? timings[0].cpuFrameTime : 0d;
        double gpuMs = timingCount > 0 ? timings[0].gpuFrameTime : 0d;
        string folder = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(folder, "lkperf.txt");
        string report = "LK_PerfProbe Island1 samples=" + sampleCount
            + " avgMs=" + avg.ToString("0.00")
            + " minMs=" + min.ToString("0.00")
            + " maxMs=" + max.ToString("0.00")
            + " p95Ms=" + p95.ToString("0.00")
            + " fps=" + (avg > 0.01f ? (1000f / avg).ToString("0.0") : "0")
            + " gcAvgBytes=" + gcAvg.ToString("0")
            + " gcMin=" + (gcFrames > 0 ? gcMin.ToString() : "n/a")
            + " gcMax=" + gcMax
            + " cpuFrameMs=" + cpuMs.ToString("0.00")
            + " gpuFrameMs=" + gpuMs.ToString("0.00")
            + " drawCalls=" + Last(drawCalls)
            + " setPass=" + Last(setPass)
            + " triangles=" + Last(triangles);
        File.WriteAllText(path, report);
        Debug.Log(report);
    }

    float Percentile(float rank)
    {
        if (sampleCount <= 0)
        {
            return 0f;
        }

        float[] copy = new float[sampleCount];
        Array.Copy(samples, copy, sampleCount);
        Array.Sort(copy);
        int index = Mathf.Clamp(Mathf.CeilToInt(rank * sampleCount) - 1, 0, sampleCount - 1);
        return copy[index];
    }

    static long Last(ProfilerRecorder recorder)
    {
        return recorder.Valid ? recorder.LastValue : -1;
    }

    void OnDestroy()
    {
        if (gcAlloc.Valid)
        {
            gcAlloc.Dispose();
        }

        if (drawCalls.Valid)
        {
            drawCalls.Dispose();
        }

        if (setPass.Valid)
        {
            setPass.Dispose();
        }

        if (triangles.Valid)
        {
            triangles.Dispose();
        }
    }
}
}
