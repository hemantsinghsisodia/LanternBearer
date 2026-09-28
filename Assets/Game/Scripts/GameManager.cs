using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const string BestTimeKey = "LanternKeeperBestTime";

    [SerializeField] int beaconsToWin = 5;
    [SerializeField] Lantern lantern;

    int litCount;
    int nearbyUnlit;
    float elapsed;
    bool roundOver;
    bool won;

    public int LitCount => litCount;
    public int BeaconsToWin => beaconsToWin;
    public bool ShowInteractPrompt => nearbyUnlit > 0 && !roundOver;
    public bool IsRoundOver => roundOver;
    public float Elapsed => elapsed;
    public bool Won => won;

    public event Action<int, int> BeaconsChanged;
    public event Action PromptChanged;
    public event Action WonGame;
    public event Action LostGame;
    public event Action<float> TimeChanged;

    public float BestTime
    {
        get
        {
            if (!PlayerPrefs.HasKey(BestTimeKey))
            {
                return -1f;
            }

            return PlayerPrefs.GetFloat(BestTimeKey);
        }
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BindLantern();
        RaiseBeacons();
        RaiseTime();
    }

    void OnDestroy()
    {
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

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (!roundOver && lantern != null && lantern.Fuel <= 0f)
        {
            OnFuelDepleted();
        }

        if (roundOver)
        {
            return;
        }

        elapsed += Time.deltaTime;
        RaiseTime();
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

        nearbyUnlit += nearby ? 1 : -1;
        if (nearbyUnlit < 0)
        {
            nearbyUnlit = 0;
        }

        if (PromptChanged != null)
        {
            PromptChanged.Invoke();
        }
    }

    public void RegisterBeaconLit(Beacon beacon)
    {
        if (beacon == null || roundOver)
        {
            return;
        }

        litCount++;
        RaiseBeacons();
        if (litCount >= beaconsToWin)
        {
            Win();
        }
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

    void Win()
    {
        roundOver = true;
        won = true;

        float previousBest = BestTime;
        if (previousBest < 0f || elapsed < previousBest)
        {
            PlayerPrefs.SetFloat(BestTimeKey, elapsed);
            PlayerPrefs.Save();
        }

        if (WonGame != null)
        {
            WonGame.Invoke();
        }
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return minutes.ToString("00") + ":" + secs.ToString("00");
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
