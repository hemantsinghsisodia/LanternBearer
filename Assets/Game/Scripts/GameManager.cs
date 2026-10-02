using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] int beaconsToWin = 5;
    [SerializeField] Lantern lantern;
    [SerializeField] string levelId = "island1";
    [SerializeField] string islandLabel = "";
    [SerializeField] string nextLevelScene = "";
    [SerializeField] string[] logEntries = new string[0];
    [SerializeField] string[] introLines = new string[0];
    [SerializeField] float deathSeconds = 1.5f;
    [SerializeField] float restartGrace = 0.5f;
    [SerializeField] HUD hud;
    [SerializeField] DawnSequence dawn;

    readonly HashSet<Beacon> nearbyBeacons = new HashSet<Beacon>();
    readonly List<Beacon> nearbyScratch = new List<Beacon>();

    int litCount;
    float elapsed;
    bool roundOver;
    bool won;
    bool paused;
    bool dying;
    bool controlsLocked;
    bool introShowing;
    float introDismissUnlock;
    bool refsResolved;
    float restartUnlockTime = float.PositiveInfinity;
    int raisedSecond = int.MinValue;
    Beacon nearestBeacon;

    public int LitCount => litCount;
    public int BeaconsToWin => beaconsToWin;
    public Beacon NearestBeacon => nearestBeacon;
    public bool ShowInteractPrompt => nearestBeacon != null && !roundOver && !paused;
    public bool IsRoundOver => roundOver;
    public bool IsPaused => paused;
    public bool IsDying => dying;
    public bool ControlsLocked => controlsLocked;
    // True while the first-visit intro card holds the game paused. The card reuses Pause(), so the pause panel is hidden by the HUD.
    public bool IntroShowing => introShowing;
    public string[] IntroLines => introLines;
    public float DeathStartedUnscaled { get; private set; }
    public float LoseShownUnscaled { get; private set; }
    public bool RestartAllowed => roundOver && !paused && !DawnPlaying && !dying && Time.unscaledTime >= restartUnlockTime;
    public bool DawnPlaying { get; private set; }
    public float Elapsed => elapsed;
    public bool Won => won;
    public string LevelId => string.IsNullOrEmpty(levelId) ? SceneManager.GetActiveScene().name : levelId;
    public string NextLevelScene => nextLevelScene;
    // Em-dash form, e.g. "Island 4 <em dash> The Storm Cape". HUD uses IslandLabels.ToHud for the middle-dot form.
    public string IslandLabel => islandLabel;
    public string IslandLabelHud => IslandLabels.ToHud(islandLabel);

    public event Action<int, int> BeaconsChanged;
    public event Action PromptChanged;
    public event Action WonGame;
    public event Action LostGame;
    public event Action<float> TimeChanged;

    public float BestTime
    {
        get { return GameSettings.GetBestTime(LevelId); }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameManager destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RestoreTime();
    }

    void Start()
    {
        ResolveSceneRefs();
        BindLantern();
        RaiseBeacons();
        RaiseTime();
        BeginIntroIfUnseen();
    }

    void BeginIntroIfUnseen()
    {
        if (introLines == null || introLines.Length == 0 || GameSettings.HasSeenIntro(LevelId))
        {
            return;
        }

        introShowing = true;
        introDismissUnlock = Time.unscaledTime + 0.25f;
        Pause();
    }

    // Marks the intro seen and releases the pause it took.
    public void EndIntro()
    {
        if (!introShowing)
        {
            return;
        }

        introShowing = false;
        GameSettings.MarkIntroSeen(LevelId);
        Resume();
    }

    static bool AnyInputPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
    }

    void OnDestroy()
    {
        RestoreTime();
        if (lantern != null)
        {
            lantern.FuelDepleted -= OnFuelDepleted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        HandleKeys();
        if (paused)
        {
            return;
        }

        if (!roundOver && !dying && lantern != null && lantern.Fuel <= 0f && !lantern.DrainFrozen)
        {
            Lose();
        }

        if (roundOver)
        {
            return;
        }

        elapsed += Time.deltaTime;
        RaiseTime();
    }

    void LateUpdate()
    {
        ReconcileNearby();
        SelectNearest();
    }

    void HandleKeys()
    {
        if (introShowing)
        {
            // Any key or click dismisses the card; the short unlock stops the click that loaded the scene from skipping it.
            if (Time.unscaledTime >= introDismissUnlock && AnyInputPressedThisFrame())
            {
                EndIntro();
            }

            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool pauseKey = keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame;
        if (pauseKey)
        {
            if (paused)
            {
                if (hud != null && hud.ConsumePauseBack())
                {
                    return;
                }

                Resume();
            }
            else if (!roundOver && !DawnPlaying && !dying)
            {
                Pause();
            }
        }

        if (RestartAllowed && keyboard.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }
    }

    public void SetControlsLocked(bool locked)
    {
        controlsLocked = locked;
    }

    public bool DebugPressRestart()
    {
        if (!RestartAllowed)
        {
            return false;
        }

        RestartLevel();
        return true;
    }

    public void Pause()
    {
        if (paused || roundOver || DawnPlaying || dying)
        {
            return;
        }

        paused = true;
        Time.timeScale = 0f;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionMix(true, "pause");
        }

        AudioListener.pause = true;
    }

    public void Resume()
    {
        if (!paused)
        {
            return;
        }

        RestoreTime();
    }

    public void RestartLevel()
    {
        RestoreTime();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMenu()
    {
        RestoreTime();
        SceneManager.LoadScene("MainMenu");
    }

    void RestoreTime()
    {
        paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionMix(false, "resume");
        }
    }

    void ResolveSceneRefs()
    {
        if (refsResolved)
        {
            return;
        }

        bool missing = lantern == null || hud == null || dawn == null;
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (hud == null)
        {
            hud = FindAnyObjectByType<HUD>();
        }

        if (dawn == null)
        {
            dawn = FindAnyObjectByType<DawnSequence>();
        }

        refsResolved = true;
        if (missing)
        {
            Debug.LogWarning("GameManager references were not fully wired. Resolved once.", this);
        }
    }

    void BindLantern()
    {
        if (lantern != null)
        {
            lantern.FuelDepleted -= OnFuelDepleted;
            lantern.FuelDepleted += OnFuelDepleted;
        }
    }

    public void SetBeaconNearby(Beacon beacon, bool nearby)
    {
        if (beacon == null)
        {
            return;
        }

        bool changed;
        if (nearby && IsNearby(beacon))
        {
            changed = nearbyBeacons.Add(beacon);
        }
        else
        {
            changed = nearbyBeacons.Remove(beacon);
        }

        if (changed)
        {
            SelectNearest();
        }
    }

    public void RegisterBeaconLit(Beacon beacon)
    {
        if (beacon == null || roundOver)
        {
            return;
        }

        nearbyBeacons.Remove(beacon);
        SelectNearest();
        litCount++;
        RaiseBeacons();
        ShowLogEntry();
        if (CanWin())
        {
            Win();
        }
    }

    void ShowLogEntry()
    {
        int index = litCount - 1;
        if (logEntries == null || index < 0 || index >= logEntries.Length || string.IsNullOrEmpty(logEntries[index]))
        {
            return;
        }

        GameSettings.RecordLogRead(LevelId, litCount);
        if (hud != null)
        {
            hud.ShowLogToast(logEntries[index]);
        }
    }

    bool CanWin()
    {
        if (roundOver || dying || won)
        {
            return false;
        }

        if (lantern != null && lantern.Fuel <= 0f)
        {
            return false;
        }

        return litCount >= beaconsToWin;
    }

    public void Lose()
    {
        OnFuelDepleted();
    }

    void OnFuelDepleted()
    {
        if (roundOver || dying || won)
        {
            return;
        }

        dying = true;
        controlsLocked = true;
        DeathStartedUnscaled = Time.unscaledTime;
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        ResolveSceneRefs();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionMix(true, "loss");
            AudioManager.Instance.PlayDying();
        }

        float duration = Mathf.Max(0.2f, deathSeconds);
        float elapsedDeath = 0f;
        while (elapsedDeath < duration)
        {
            elapsedDeath += Time.unscaledDeltaTime;
            float unit = Mathf.Clamp01(elapsedDeath / duration);
            if (hud != null)
            {
                hud.SetDeathAmount(unit);
            }

            if (lantern != null)
            {
                if (unit < 0.62f)
                {
                    float flicker = Mathf.Abs(Mathf.Sin(elapsedDeath * 28f));
                    float amp = Mathf.Lerp(3.2f, 0.2f, unit / 0.62f);
                    lantern.SetDeathIntensity(flicker * amp);
                }
                else
                {
                    float fade = 1f - Mathf.InverseLerp(0.62f, 1f, unit);
                    lantern.SetDeathIntensity(fade * 0.35f);
                }
            }

            yield return null;
        }

        if (lantern != null)
        {
            lantern.SetDeathIntensity(0f);
        }

        if (hud != null)
        {
            hud.SetDeathAmount(1f);
        }

        roundOver = true;
        dying = false;
        controlsLocked = false;
        LoseShownUnscaled = Time.unscaledTime;
        restartUnlockTime = LoseShownUnscaled + restartGrace;
        if (LostGame != null)
        {
            LostGame.Invoke();
        }
    }

    public void Win()
    {
        if (roundOver || dying)
        {
            return;
        }

        if (lantern != null && lantern.Fuel <= 0f)
        {
            return;
        }

        roundOver = true;
        GameSettings.TryRecordBest(LevelId, elapsed);
        GameSettings.MarkWon(LevelId);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionMix(true, "win");
        }

        ResolveSceneRefs();
        if (dawn != null)
        {
            DawnPlaying = true;
            dawn.Play(FinishWin);
            return;
        }

        FinishWin();
    }

    void FinishWin()
    {
        DawnPlaying = false;
        won = true;
        if (WonGame != null)
        {
            WonGame.Invoke();
        }
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f)
        {
            return "--:--";
        }

        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return minutes.ToString("00") + ":" + secs.ToString("00");
    }

    void ReconcileNearby()
    {
        nearbyScratch.Clear();
        foreach (Beacon beacon in nearbyBeacons)
        {
            if (!IsNearby(beacon))
            {
                nearbyScratch.Add(beacon);
            }
        }

        for (int i = 0; i < nearbyScratch.Count; i++)
        {
            nearbyBeacons.Remove(nearbyScratch[i]);
        }

        List<Beacon> all = Beacon.All;
        for (int i = 0; i < all.Count; i++)
        {
            Beacon beacon = all[i];
            if (IsNearby(beacon))
            {
                nearbyBeacons.Add(beacon);
            }
        }
    }

    void SelectNearest()
    {
        Beacon best = null;
        float bestDistance = float.MaxValue;
        foreach (Beacon beacon in nearbyBeacons)
        {
            if (!IsNearby(beacon))
            {
                continue;
            }

            float distance = beacon.DistanceToPlayer;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = beacon;
            }
        }

        if (best == nearestBeacon)
        {
            return;
        }

        nearestBeacon = best;
        if (PromptChanged != null)
        {
            PromptChanged.Invoke();
        }
    }

    static bool IsNearby(Beacon beacon)
    {
        return beacon != null && beacon.IsPlayerInRange;
    }

    void RaiseBeacons()
    {
        if (BeaconsChanged != null)
        {
            BeaconsChanged.Invoke(litCount, beaconsToWin);
        }
    }

    void RaiseTime()
    {
        int whole = elapsed < 0f ? -1 : Mathf.FloorToInt(elapsed);
        if (whole == raisedSecond)
        {
            return;
        }

        raisedSecond = whole;
        if (TimeChanged != null)
        {
            TimeChanged.Invoke(elapsed);
        }
    }
}
}
