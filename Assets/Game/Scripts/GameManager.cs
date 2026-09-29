using System;
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
    [SerializeField] string nextLevelScene = "";

    readonly HashSet<Beacon> nearbyBeacons = new HashSet<Beacon>();
    readonly List<Beacon> nearbyScratch = new List<Beacon>();

    int litCount;
    float elapsed;
    bool roundOver;
    bool won;
    bool paused;
    Beacon nearestBeacon;

    public int LitCount => litCount;
    public int BeaconsToWin => beaconsToWin;
    public Beacon NearestBeacon => nearestBeacon;
    public bool ShowInteractPrompt => nearestBeacon != null && !roundOver && !paused;
    public bool IsRoundOver => roundOver;
    public bool IsPaused => paused;
    public bool DawnPlaying { get; private set; }
    public float Elapsed => elapsed;
    public bool Won => won;
    public string LevelId => string.IsNullOrEmpty(levelId) ? SceneManager.GetActiveScene().name : levelId;
    public string NextLevelScene => nextLevelScene;

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
        Instance = this;
        RestoreTime();
    }

    void Start()
    {
        BindLantern();
        RaiseBeacons();
        RaiseTime();
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
        if (Instance != this)
        {
            Instance = this;
            BindLantern();
        }

        HandleKeys();
        if (paused)
        {
            return;
        }

        if (!roundOver && lantern != null && lantern.Fuel <= 0f)
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
                Resume();
            }
            else if (!roundOver && !DawnPlaying)
            {
                Pause();
            }
        }

        if (!paused && roundOver && !DawnPlaying && keyboard.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }
    }

    public void Pause()
    {
        if (paused || roundOver || DawnPlaying)
        {
            return;
        }

        paused = true;
        Time.timeScale = 0f;
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
    }

    void BindLantern()
    {
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

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
        if (litCount >= beaconsToWin)
        {
            Win();
        }
    }

    public void Lose()
    {
        OnFuelDepleted();
    }

    void OnFuelDepleted()
    {
        if (roundOver)
        {
            return;
        }

        roundOver = true;
        if (LostGame != null)
        {
            LostGame.Invoke();
        }
    }

    public void Win()
    {
        if (roundOver)
        {
            return;
        }

        roundOver = true;
        GameSettings.TryRecordBest(LevelId, elapsed);
        GameSettings.MarkWon(LevelId);

        DawnSequence dawn = FindAnyObjectByType<DawnSequence>();
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
        if (TimeChanged != null)
        {
            TimeChanged.Invoke(elapsed);
        }
    }
}
}
