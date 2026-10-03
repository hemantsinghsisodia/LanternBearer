using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Captures the "before" look set: fixed world shots and UI screens on Low and Ultra.
// A state machine on EditorApplication.update. Its whole state lives in SessionState, so it resumes after the domain reload
// that each play-mode entry causes, and every exit (done or failed) goes through LookCaptureState.Restore.
[InitializeOnLoad]
public static class LookBaselineCapture
{
    const string JobKey = "LookBaselineCapture.Job";
    const int SettleFrames = 60;
    const int ShotFrames = 5;
    const int WarmFrames = 60;
    const int MeasureFrames = 300;
    const int ScreenFrames = 8;
    const int SpawnWaitFrames = 3600;
    const int JpegQuality = 92;
    const int Width = 1920;
    const int Height = 1080;
    const string MenuScene = "Assets/Game/Scenes/MainMenu.unity";

    static readonly GraphicsLevel[] Presets = { GraphicsLevel.Low, GraphicsLevel.Ultra };
    static readonly string[] MenuScreens = { "mainmenu", "journal" };

    enum Phase
    {
        OpenScene,
        WaitPlay,
        SetPreset,
        Wait,
        ShotPrepare,
        ShotRender,
        FrameWarm,
        FrameMeasure,
        ScreenAct,
        ScreenGrab,
        ScreenSave,
        ScreenNext,
        ExitPlay,
        WaitStop,
        Finish
    }

    [Serializable]
    class Job
    {
        public string outputDir;
        public string snapshot;
        public string before;
        public int session;
        public int preset;
        public int shot;
        public int screen;
        public int phase;
        public int next;
        public int waitLeft;
        public int measured;
        public double measureSum;
        public int searchFrames;
        public bool failed;
        public string message;
        public string screenSize;
        public List<string> rows = new List<string>();
    }

    // Sessions 0..3 are the islands, 4 the main menu, 5 the island 1 HUD and pause.
    const int MenuSession = 4;
    const int HudSession = 5;
    const int SessionCount = 6;

    static Job job;
    static LookShotList shotList;
    static int lastFrame = -1;
    static bool hooked;
    static bool started;
    static float savedFov;

    static LookBaselineCapture()
    {
        if (SessionState.GetString(JobKey, "") != "")
        {
            Hook();
        }
    }

    [MenuItem("Lantern Keeper/Look/Capture Look Baseline")]
    public static void CaptureFromMenu()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        Run(Path.Combine(root, "docs/look/baseline/" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    public static void Run(string outputDir)
    {
        if (SessionState.GetString(JobKey, "") != "")
        {
            Debug.LogError("Look capture: a run is already in progress.");
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Look capture: leave play mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        for (int i = 0; i < LookShotGenerator.LevelIds.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<LookShotList>(LookShotGenerator.AssetPath(LookShotGenerator.LevelIds[i])) == null)
            {
                Debug.LogError("Look capture: missing shot list for " + LookShotGenerator.LevelIds[i] + ". Run Generate Shot Lists first.");
                return;
            }
        }

        Directory.CreateDirectory(outputDir);
        LookCaptureState state = LookCaptureState.Snapshot();
        Job fresh = new Job();
        fresh.outputDir = outputDir;
        fresh.before = state.Describe();

        LookGameView.Show();
        int index;
        state.gameViewSizeAdded = LookGameView.EnsureFixed1080(out index);
        LookGameView.Select(index);
        fresh.snapshot = JsonUtility.ToJson(state);
        fresh.phase = (int)Phase.OpenScene;
        Debug.Log("Look capture BEFORE: " + fresh.before);
        Save(fresh);
        Hook();
    }

    static void Hook()
    {
        if (hooked)
        {
            return;
        }

        hooked = true;
        EditorApplication.update += Update;
    }

    static void Save(Job value)
    {
        job = value;
        SessionState.SetString(JobKey, JsonUtility.ToJson(value));
    }

    static void Save()
    {
        SessionState.SetString(JobKey, JsonUtility.ToJson(job));
    }

    static void Update()
    {
        if (job == null)
        {
            string json = SessionState.GetString(JobKey, "");
            if (json == "")
            {
                EditorApplication.update -= Update;
                hooked = false;
                return;
            }

            job = JsonUtility.FromJson<Job>(json);
        }

        try
        {
            Step();
        }
        catch (Exception exception)
        {
            Debug.LogError("Look capture failed: " + exception);
            Abort(exception.Message);
        }
    }

    static void Abort(string message)
    {
        job.failed = true;
        job.message = message;
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.ExitPlaymode();
            job.phase = (int)Phase.WaitStop;
            job.session = SessionCount;
        }
        else
        {
            job.phase = (int)Phase.Finish;
        }

        Save();
    }

    static bool Transitioning
    {
        get { return EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating; }
    }

    static void Go(Phase phase)
    {
        job.phase = (int)phase;
        Save();
    }

    static void WaitThen(int frames, Phase next)
    {
        job.waitLeft = frames;
        job.next = (int)next;
        lastFrame = Time.frameCount;
        Go(Phase.Wait);
    }

    // True once per rendered game frame.
    static bool NewFrame()
    {
        if (Time.frameCount == lastFrame)
        {
            return false;
        }

        lastFrame = Time.frameCount;
        return true;
    }

    static string LevelId(int session)
    {
        return session < LookShotGenerator.LevelIds.Length ? LookShotGenerator.LevelIds[session] : "island1";
    }

    static void Step()
    {
        Phase phase = (Phase)job.phase;
        switch (phase)
        {
            case Phase.OpenScene:
                OpenSession();
                break;
            case Phase.WaitPlay:
                if (!EditorApplication.isPlaying || Transitioning)
                {
                    return;
                }

                Application.runInBackground = true;
                job.preset = 0;
                job.shot = 0;
                job.screen = 0;
                job.searchFrames = 0;
                Save();
                Go(Phase.SetPreset);
                break;
            case Phase.SetPreset:
                GraphicsQuality.Set(Presets[job.preset]);
                job.shot = 0;
                job.screen = 0;
                job.searchFrames = 0;
                shotList = null;
                lastFrame = Time.frameCount;
                if (job.session < LookShotGenerator.LevelIds.Length)
                {
                    WaitThen(SettleFrames, Phase.ShotPrepare);
                }
                else
                {
                    WaitThen(SettleFrames, Phase.ScreenAct);
                }

                break;
            case Phase.Wait:
                if (NewFrame())
                {
                    job.waitLeft--;
                    if (job.waitLeft <= 0)
                    {
                        Go((Phase)job.next);
                    }
                }

                break;
            case Phase.ShotPrepare:
                PrepareShot();
                break;
            case Phase.ShotRender:
                RenderShot();
                break;
            case Phase.FrameWarm:
                RestoreGameplayCamera();
                job.measured = 0;
                job.measureSum = 0.0;
                WaitThen(WarmFrames, Phase.FrameMeasure);
                break;
            case Phase.FrameMeasure:
                if (NewFrame())
                {
                    job.measureSum += Time.unscaledDeltaTime;
                    job.measured++;
                    if (job.measured >= MeasureFrames)
                    {
                        FinishMeasure();
                    }
                    else
                    {
                        Save();
                    }
                }

                break;
            case Phase.ScreenAct:
                ActOnScreen();
                break;
            case Phase.ScreenGrab:
                BeginGrab();
                break;
            case Phase.ScreenSave:
                SaveScreen();
                break;
            case Phase.ScreenNext:
                NextScreen();
                break;
            case Phase.ExitPlay:
                EditorApplication.ExitPlaymode();
                Go(Phase.WaitStop);
                break;
            case Phase.WaitStop:
                if (EditorApplication.isPlaying || Transitioning)
                {
                    return;
                }

                job.session++;
                Save();
                Go(job.session >= SessionCount || job.failed ? Phase.Finish : Phase.OpenScene);
                break;
            case Phase.Finish:
                Complete();
                break;
        }
    }

    static void OpenSession()
    {
        if (EditorApplication.isPlaying || Transitioning)
        {
            return;
        }

        string levelId = LevelId(job.session);
        PlayerPrefs.SetInt(LookCaptureState.IntroKey(levelId), 1);
        PlayerPrefs.Save();
        string scene = job.session == MenuSession ? MenuScene : LookShotGenerator.ScenePath(levelId);
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        Application.runInBackground = true;
        Go(Phase.WaitPlay);
        EditorApplication.EnterPlaymode();
    }

    static LookShotList List()
    {
        if (shotList == null)
        {
            shotList = AssetDatabase.LoadAssetAtPath<LookShotList>(LookShotGenerator.AssetPath(LevelId(job.session)));
        }

        return shotList;
    }

    static Camera MainCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            throw new InvalidOperationException("No main camera in " + LevelId(job.session));
        }

        return camera;
    }

    static void PrepareShot()
    {
        LookShotList list = List();
        if (job.shot >= list.shots.Count)
        {
            Go(Phase.FrameWarm);
            return;
        }

        LookShot shot = list.shots[job.shot];
        Camera camera = MainCamera();
        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow != null && follow.enabled)
        {
            savedFov = camera.fieldOfView;
            follow.enabled = false;
        }

        // Frozen props need a fully faded-in model to pose, so wait until one exists.
        Behaviour prop = null;
        if (shot.label == "moth" || shot.label == "shade")
        {
            prop = FindProp(shot.label);
            if (prop == null)
            {
                job.searchFrames++;
                if (job.searchFrames > SpawnWaitFrames)
                {
                    throw new InvalidOperationException("No " + shot.label + " appeared on " + LevelId(job.session));
                }

                return;
            }
        }

        camera.transform.SetPositionAndRotation(shot.position, Quaternion.Euler(shot.euler));
        camera.fieldOfView = shot.fov;
        if (prop != null)
        {
            float ahead = shot.label == "moth" ? 2f : 4f;
            prop.transform.position = camera.transform.position + camera.transform.forward * ahead;
            prop.enabled = false;
        }

        job.searchFrames = 0;
        WaitThen(ShotFrames, Phase.ShotRender);
    }

    static Behaviour FindProp(string label)
    {
        if (label == "moth")
        {
            for (int i = 0; i < Moth.All.Count; i++)
            {
                Moth moth = Moth.All[i];
                if (moth != null && !moth.IsDespawning && moth.Appear >= 0.95f)
                {
                    return moth;
                }
            }

            return null;
        }

        FieldInfo appear = typeof(Shade).GetField("appear", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < Shade.All.Count; i++)
        {
            Shade shade = Shade.All[i];
            if (shade != null && appear != null && (float)appear.GetValue(shade) >= 0.95f)
            {
                return shade;
            }
        }

        return null;
    }

    static void RenderShot()
    {
        LookShot shot = List().shots[job.shot];
        Camera camera = MainCamera();
        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;
        RenderTexture.active = target;
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(target);
        string path = Path.Combine(job.outputDir, LevelId(job.session), Presets[job.preset].ToString().ToLowerInvariant(), shot.label + ".jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, ImageConversion.EncodeToJPG(texture, JpegQuality));
        UnityEngine.Object.DestroyImmediate(texture);

        // Put the frozen prop back to life so the next shots and the frame timing see normal behaviour.
        if (shot.label == "moth")
        {
            ReEnable(typeof(Moth));
        }
        else if (shot.label == "shade")
        {
            ReEnable(typeof(Shade));
        }

        job.shot++;
        Go(Phase.ShotPrepare);
    }

    static void ReEnable(Type type)
    {
        UnityEngine.Object[] all = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Behaviour behaviour = (Behaviour)all[i];
            if (!behaviour.enabled)
            {
                behaviour.enabled = true;
            }
        }
    }

    static void RestoreGameplayCamera()
    {
        Camera camera = MainCamera();
        LookShot spawn = List().shots[0];
        camera.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(spawn.euler));
        if (savedFov > 0f)
        {
            camera.fieldOfView = savedFov;
        }

        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow != null)
        {
            follow.enabled = true;
        }
    }

    static void FinishMeasure()
    {
        double average = job.measureSum / job.measured;
        double ms = average * 1000.0;
        string row = "| " + LevelId(job.session) + " | " + Presets[job.preset] + " | " + ms.ToString("F2", CultureInfo.InvariantCulture) + " | " + (1.0 / average).ToString("F1", CultureInfo.InvariantCulture) + " |";
        job.rows.Add(row);
        NextPreset();
    }

    static void NextPreset()
    {
        job.preset++;
        if (job.preset >= Presets.Length)
        {
            Go(Phase.ExitPlay);
        }
        else
        {
            Go(Phase.SetPreset);
        }
    }

    // Screens come from the Game view, so the overlay UI is included.
    static string ScreenName()
    {
        if (job.session == MenuSession)
        {
            return MenuScreens[job.screen];
        }

        return job.screen == 0 ? "hud" : "pause";
    }

    static int ScreenCount()
    {
        return 2;
    }

    // Screen 1 of the menu and HUD sessions needs a button press or the pause first.
    static void ActOnScreen()
    {
        if (job.screen == 0)
        {
            Go(Phase.ScreenGrab);
            return;
        }

        if (job.session == MenuSession)
        {
            Button log = FindButton("LogButton");
            if (log == null)
            {
                throw new InvalidOperationException("LogButton not found");
            }

            log.onClick.Invoke();
        }
        else
        {
            if (GameManager.Instance == null)
            {
                throw new InvalidOperationException("No GameManager for the pause screen");
            }

            GameManager.Instance.Pause();
            if (!GameManager.Instance.IsPaused)
            {
                throw new InvalidOperationException("Pause did not take");
            }
        }

        // Give the freshly opened panel a few frames to lay out and draw.
        WaitThen(ScreenFrames, Phase.ScreenGrab);
    }

    // ScreenCapture queues the grab for the end of the frame and writes a PNG; the next phase re-encodes it as the JPEG.
    static void BeginGrab()
    {
        string temp = TempShot();
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }

        ScreenCapture.CaptureScreenshot(temp);
        Go(Phase.ScreenSave);
    }

    static string TempShot()
    {
        return Path.Combine(job.outputDir, "_screen.png");
    }

    static void SaveScreen()
    {
        string temp = TempShot();
        if (!File.Exists(temp) || !NewFrame())
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(temp);
        }
        catch (IOException)
        {
            return;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!ImageConversion.LoadImage(texture, bytes))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            return;
        }

        job.screenSize = texture.width + "x" + texture.height;
        string path = Path.Combine(job.outputDir, "screens", Presets[job.preset].ToString().ToLowerInvariant(), ScreenName() + ".jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, ImageConversion.EncodeToJPG(texture, JpegQuality));
        UnityEngine.Object.DestroyImmediate(texture);
        File.Delete(temp);
        Go(Phase.ScreenNext);
    }

    static void NextScreen()
    {
        if (job.session == MenuSession && job.screen == 1)
        {
            Button back = FindButton("LogBackButton");
            if (back != null)
            {
                back.onClick.Invoke();
            }
        }
        else if (job.session == HudSession && job.screen == 1)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Resume();
            }
        }

        job.screen++;
        if (job.screen >= ScreenCount())
        {
            NextPreset();
        }
        else
        {
            Go(Phase.ScreenAct);
        }
    }

    static Button FindButton(string name)
    {
        Button[] all = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
            {
                return all[i];
            }
        }

        return null;
    }

    static void Complete()
    {
        EditorApplication.update -= Update;
        hooked = false;
        LookCaptureState state = JsonUtility.FromJson<LookCaptureState>(job.snapshot);
        state.Restore();
        string after = LookCaptureState.Snapshot().Describe();
        Debug.Log("Look capture AFTER:  " + after);
        WriteFrameTimes(after);
        Debug.Log(job.failed ? "Look capture FAILED and restored: " + job.message : "Look capture done: " + job.outputDir);
        SessionState.EraseString(JobKey);
        job = null;
        AssetDatabase.Refresh();
    }

    static void WriteFrameTimes(string after)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Look baseline frame times");
        text.AppendLine();
        text.AppendLine("Captured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " in the editor (Game view, 1920x1080 fixed size). Average of " + MeasureFrames + " frames after " + WarmFrames + " warm-up frames, at the spawn camera.");
        text.AppendLine("Screens were captured at " + (job.screenSize ?? "n/a") + ".");
        if (job.failed)
        {
            text.AppendLine("RUN FAILED: " + job.message);
        }

        text.AppendLine();
        text.AppendLine("| Island | Preset | Average ms | FPS |");
        text.AppendLine("|---|---|---|---|");
        for (int i = 0; i < job.rows.Count; i++)
        {
            text.AppendLine(job.rows[i]);
        }

        text.AppendLine();
        text.AppendLine("Editor state before the run: `" + job.before + "`");
        text.AppendLine();
        text.AppendLine("Editor state after the run: `" + after + "`");
        File.WriteAllText(Path.Combine(job.outputDir, "frametimes.md"), text.ToString());
    }
}
}
