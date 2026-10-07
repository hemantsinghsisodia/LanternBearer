using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
// Coordinator for the in-game HUD. It reads GameManager, Lantern and the player and pushes plain values into the
// components of the GameHud prefab (Scripts/Hud). Prompt, tide, storm and the fade overlays live in small helpers.
public class HUD : MonoBehaviour
{
    [SerializeField] FuelGaugeWidget fuelGauge;
    [SerializeField] BeaconRoofs roofs;
    [SerializeField] DrainIcons drainIcons;
    [SerializeField] TimerLabel timerLabel;
    [SerializeField] IslandLabel islandLabel;
    [SerializeField] TideChip tideChip;
    [SerializeField] StormChip stormChip;
    [SerializeField] IntroCard introCard;
    [SerializeField] ResultPanel resultPanel;
    [SerializeField] Toasts toasts;
    [SerializeField] TMP_Text promptText;
    [SerializeField] PauseScreen pauseScreen;
    [SerializeField] Image fadeOverlay;
    [SerializeField] Image deathOverlay;
    [SerializeField] Lantern lantern;

    readonly ScreenOverlays overlays = new ScreenOverlays();
    readonly TideFeed tideFeed = new TideFeed();
    readonly StormFeed stormFeed = new StormFeed();
    readonly IntroFeed introFeed = new IntroFeed();
    HudPrompt prompt;
    PlayerController player;
    bool bound;
    bool lanternBound;
    bool buttonsBound;
    bool missingResolved;
    bool lanternResolved;
    int shownSecond = int.MinValue;
    int shownTotal = int.MinValue;
    int shownLit = int.MinValue;
    string shownIslandLabel;

    public float FadeAlpha => overlays.FadeAlpha;
    public float DeathAmount => overlays.DeathAmount;

    void OnEnable()
    {
        Beacon.LightFailed += OnLightFailed;
        BindIfNeeded();
        EnsureWidgets();
        if (resultPanel != null)
        {
            resultPanel.Hide();
        }

        if (pauseScreen != null)
        {
            pauseScreen.Hide();
        }
    }

    void OnDisable()
    {
        Beacon.LightFailed -= OnLightFailed;
        Unbind();
    }

    void Start()
    {
        BindIfNeeded();
        EnsureWidgets();
        if (timerLabel != null)
        {
            timerLabel.SetFormatter(GameManager.FormatTime);
        }
    }

    void Update()
    {
        UpdateFuelAndDrain();
        tideFeed.Update(tideChip, toasts);
        stormFeed.Update(stormChip, gameObject);

        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        UpdateRoofs(manager);
        RefreshIslandLabel(manager);
        ShowTimer(manager.Elapsed);
        prompt.Refresh(manager, lantern);
        introFeed.Update(introCard, pauseScreen, manager);
        if (resultPanel == null)
        {
            return;
        }

        if (manager.Won && !resultPanel.IsShowing)
        {
            ShowWin();
        }
        else if (manager.IsRoundOver && !manager.DawnPlaying && !resultPanel.IsShowing)
        {
            ShowLose();
        }

        if (resultPanel.IsShowing)
        {
            GraphicsMenu.HandleTab(resultPanel.TabOrder);
        }
    }

    void UpdateFuelAndDrain()
    {
        if (lantern == null)
        {
            return;
        }

        if (fuelGauge != null)
        {
            fuelGauge.Show(lantern.FuelNormalized, lantern.MaxFuel, UserSettings.ReduceFlashing);
        }

        if (drainIcons == null)
        {
            return;
        }

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        HudMath.DrainState state = new HudMath.DrainState();
        state.MothsDraining = lantern.MothsDraining;
        state.InSafeLight = lantern.InSafeLight;
        state.Sprinting = player != null && player.IsSprinting;
        state.DrainRelative = lantern.DrainRelative;
        drainIcons.Show(HudMath.DrainIcons(state));
    }

    void UpdateRoofs(GameManager manager)
    {
        if (roofs == null || (manager.BeaconsToWin == shownTotal && manager.LitCount == shownLit))
        {
            return;
        }

        shownTotal = manager.BeaconsToWin;
        shownLit = manager.LitCount;
        roofs.Show(shownTotal, shownLit);
    }

    void BindIfNeeded()
    {
        ResolveLantern();
        ResolveMissing();
        if (!lanternBound && lantern != null && fuelGauge != null)
        {
            lanternBound = true;
            lantern.FuelAdjusted += fuelGauge.Changed;
        }

        if (resultPanel != null && !buttonsBound)
        {
            buttonsBound = true;
            resultPanel.Retry += Retry;
            resultPanel.Next += NextIsland;
            resultPanel.Menu += GoMenu;
        }

        if (bound || GameManager.Instance == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        manager.PromptChanged += OnPromptChanged;
        manager.WonGame += ShowWin;
        manager.LostGame += ShowLose;
        OnPromptChanged();
        bound = true;
    }

    void Unbind()
    {
        if (lanternBound && lantern != null && fuelGauge != null)
        {
            lantern.FuelAdjusted -= fuelGauge.Changed;
        }

        lanternBound = false;
        if (buttonsBound && resultPanel != null)
        {
            resultPanel.Retry -= Retry;
            resultPanel.Next -= NextIsland;
            resultPanel.Menu -= GoMenu;
        }

        buttonsBound = false;
        if (bound && GameManager.Instance != null)
        {
            GameManager manager = GameManager.Instance;
            manager.PromptChanged -= OnPromptChanged;
            manager.WonGame -= ShowWin;
            manager.LostGame -= ShowLose;
        }

        bound = false;
    }

    void ResolveLantern()
    {
        if (lantern != null || lanternResolved)
        {
            return;
        }

        lanternResolved = true;
        lantern = FindAnyObjectByType<Lantern>();
        Debug.LogWarning("HUD lantern was not wired. Resolved once.", this);
    }

    // Safety net for scenes whose references were lost: finds the prefab parts once, by type and name.
    void ResolveMissing()
    {
        if (missingResolved)
        {
            return;
        }

        missingResolved = true;
        if (fuelGauge == null || resultPanel == null)
        {
            fuelGauge = fuelGauge != null ? fuelGauge : GetComponentInChildren<FuelGaugeWidget>(true);
            roofs = roofs != null ? roofs : GetComponentInChildren<BeaconRoofs>(true);
            drainIcons = drainIcons != null ? drainIcons : GetComponentInChildren<DrainIcons>(true);
            timerLabel = timerLabel != null ? timerLabel : GetComponentInChildren<TimerLabel>(true);
            islandLabel = islandLabel != null ? islandLabel : GetComponentInChildren<IslandLabel>(true);
            tideChip = tideChip != null ? tideChip : GetComponentInChildren<TideChip>(true);
            stormChip = stormChip != null ? stormChip : GetComponentInChildren<StormChip>(true);
            introCard = introCard != null ? introCard : GetComponentInChildren<IntroCard>(true);
            resultPanel = resultPanel != null ? resultPanel : GetComponentInChildren<ResultPanel>(true);
            toasts = toasts != null ? toasts : GetComponentInChildren<Toasts>(true);
            Debug.LogWarning("HUD widget references were not wired. Resolved by type once.", this);
        }

        if (promptText == null)
        {
            Transform found = transform.Find("PromptText");
            promptText = found != null ? found.GetComponent<TMP_Text>() : null;
        }

        pauseScreen = pauseScreen != null ? pauseScreen : GetComponentInChildren<PauseScreen>(true);
    }

    public bool ConsumePauseBack()
    {
        return pauseScreen != null && pauseScreen.ConsumeBack();
    }

    public void EnsureWidgets()
    {
        if (prompt == null)
        {
            prompt = new HudPrompt(promptText);
        }

        overlays.Bind(transform, fadeOverlay != null ? fadeOverlay : FindImage("FadeOverlay"), deathOverlay != null ? deathOverlay : FindImage("DeathOverlay"));
    }

    Image FindImage(string objectName)
    {
        Transform found = transform.Find(objectName);
        return found != null ? found.GetComponent<Image>() : null;
    }

    public void SetFadeAlpha(float alpha)
    {
        EnsureWidgets();
        overlays.SetFade(alpha);
    }

    public void SetDeathAmount(float amount)
    {
        EnsureWidgets();
        overlays.SetDeath(amount);
    }

    // Called by GameManager when a Keeper's Log page is picked up.
    public void ShowLogToast(string line)
    {
        if (toasts != null)
        {
            toasts.Show(line, ToastKind.LogPage);
        }
    }

    void OnLightFailed()
    {
        EnsureWidgets();
        prompt.LightFailed();
    }

    void OnPromptChanged()
    {
        EnsureWidgets();
        prompt.Refresh(GameManager.Instance, lantern);
    }

    // Quiet island name at top centre; the result panel repeats it under its title.
    void RefreshIslandLabel(GameManager manager)
    {
        string label = manager.IslandLabel;
        if (label == shownIslandLabel)
        {
            return;
        }

        shownIslandLabel = label;
        if (islandLabel != null)
        {
            islandLabel.Show(string.IsNullOrEmpty(label) ? "" : manager.IslandLabelHud);
        }
    }

    void ShowTimer(float seconds)
    {
        int whole = seconds < 0f ? -1 : Mathf.FloorToInt(seconds);
        if (timerLabel == null || whole == shownSecond)
        {
            return;
        }

        shownSecond = whole;
        timerLabel.Show(seconds);
    }

    // The result panel outranks toasts, the intro card and the pause menu: they all go, and the game cannot pause any more.
    void BeginResult(GameManager manager)
    {
        if (toasts != null)
        {
            toasts.Clear();
        }

        if (introCard != null)
        {
            introCard.Hide();
        }

        if (pauseScreen != null)
        {
            pauseScreen.Hide();
        }

        EnsureWidgets();
        prompt.Hide();
        resultPanel.SetSubtitle(manager.IslandLabelHud);
    }

    void ShowWin()
    {
        GameManager manager = GameManager.Instance;
        if (resultPanel == null || manager == null)
        {
            return;
        }

        BeginResult(manager);
        resultPanel.ShowWin(manager.Elapsed, manager.BestTime, manager.NewBest, manager.LitCount, manager.BeaconsToWin, !string.IsNullOrEmpty(manager.NextLevelScene));
    }

    void ShowLose()
    {
        GameManager manager = GameManager.Instance;
        if (resultPanel == null || manager == null || manager.Won || manager.DawnPlaying)
        {
            return;
        }

        BeginResult(manager);
        resultPanel.ShowLose();
    }

    void Retry()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
        }
    }

    void NextIsland()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || string.IsNullOrEmpty(manager.NextLevelScene) || !manager.Won)
        {
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(manager.NextLevelScene);
    }

    void GoMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GoToMenu();
        }
    }
}
}
