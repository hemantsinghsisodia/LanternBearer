using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
public class HUD : MonoBehaviour
{
    const float PromptShakeDuration = 0.3f;

    static readonly Color PromptColor = new Color(1f, 0.9f, 0.7f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.38f, 0.28f, 1f);
    static readonly Color FlashColor = new Color(1f, 0.05f, 0.04f, 1f);
    static readonly Color GhostColor = new Color(0.15f, 0.07f, 0.03f, 0.82f);
    static readonly Color DotLit = new Color(1f, 0.55f, 0.16f, 1f);
    static readonly Color DotDim = new Color(0.28f, 0.3f, 0.34f, 1f);
    static readonly Color SafeStatusColor = new Color(1f, 0.86f, 0.45f);
    static readonly Color DrainStatusColor = new Color(1f, 0.78f, 0.48f);
    static readonly Color IslandLabelColor = new Color(0.86f, 0.76f, 0.58f, 0.85f);
    static readonly Color ShakeColor = new Color(1f, 0.28f, 0.16f, 1f);

    static Sprite whiteSprite;
    static Sprite circleSprite;
    static Sprite glowSprite;
    static TMP_FontAsset hudFont;
    static Material hudFace;

    [SerializeField] Image fuelFill;
    [SerializeField] Image fuelCostGhost;
    [SerializeField] Image fuelGlow;
    [SerializeField] RectTransform fuelMeter;
    [SerializeField] RectTransform beaconDots;
    [SerializeField] TMP_Text promptText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] PauseScreen pauseScreen;
    [SerializeField] GraphicsMenu graphicsMenu;
    [SerializeField] TMP_Text winDetailText;
    [SerializeField] TMP_Text loseDetailText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Image fadeOverlay;
    [SerializeField] Image deathOverlay;
    [SerializeField] TMP_Text penaltyText;
    [SerializeField] Lantern lantern;

    Image[] dots;
    Image[] dotGlows;
    float[] dotFlare;
    bool bound;
    bool missingResolved;
    bool lanternResolved;
    bool promptShaking;
    bool ghostStyled;
    int shownSecond = int.MinValue;
    int promptCost = int.MinValue;
    int paintedLit = int.MinValue;
    int statusCents = int.MinValue;
    int statusMoths = int.MinValue;
    int statusDraining = int.MinValue;
    int statusNear = int.MinValue;
    bool promptAfford;
    bool statusSafe;
    bool statusReady;
    string promptLine;
    bool buttonsWired;
    float shakeRemaining;
    Vector2 promptHome;
    bool promptHomeReady;
    Vector2 penaltyHome;
    bool penaltyHomeReady;
    Coroutine penaltyRoutine;
    bool penaltyGain;
    bool fuelReady;
    float displayedFuel = 1f;
    float fuelVelocity;
    float lastNorm = 1f;
    float gainFlash;
    bool panelsAudited;
    RectTransform tideRoot;
    Image tideFill;
    TMP_Text tideText;
    TMP_Text logText;
    Image logBack;
    Coroutine logRoutine;
    Coroutine tideRoutine;
    TMP_Text tideToast;
    Tide tide;
    bool tideSearched;
    bool tideWasRising;
    bool tideWarned;
    int tideShownSecond = int.MinValue;
    StormHUD storm;
    MothHUD mothFx;
    bool mothSearched;
    bool stormSearched;
    static Sprite vignetteSprite;
    TMP_Text islandLabelText;
    string shownIslandLabel;
    GameObject introCard;
    TMP_Text introTitle;
    TMP_Text introBody;
    TMP_Text introFooter;
    string shownCardKey;

    public float FadeAlpha => fadeOverlay != null ? fadeOverlay.color.a : 0f;
    public float DeathAmount { get; private set; }
    public string StatusLine => statusText != null ? statusText.text : "";
    public string PenaltyLine => penaltyText != null && penaltyText.gameObject.activeInHierarchy ? penaltyText.text : "";

    // Wide enough for "Drain x2.25 · Moths 2/2 • 2 moths on you" at 130% text (about 420 px) with room to spare.
    public const float StatusBoxWidth = 560f;

    void OnEnable()
    {
        Beacon.LightFailed += OnLightFailed;
        BindIfNeeded();
        EnsureFuelGhost();
        EnsureGraphicsSurface();
        WireButtons();
        EnsureWidgets();
        HidePanels();
    }

    void OnDisable()
    {
        Beacon.LightFailed -= OnLightFailed;
        Unbind();
    }

    void Start()
    {
        BindIfNeeded();
        EnsureFuelGhost();
        EnsureGraphicsSurface();
        WireButtons();
        EnsureWidgets();
    }

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (lantern != null && fuelFill != null)
        {
            float target = lantern.FuelNormalized;
            if (!fuelReady)
            {
                displayedFuel = target;
                lastNorm = target;
                fuelVelocity = 0f;
                fuelReady = true;
            }
            else
            {
            displayedFuel = FuelGauge.Step(displayedFuel, target, ref fuelVelocity, 0.3f, Time.deltaTime, 0.0005f);
            if (!Mathf.Approximately(fuelFill.fillAmount, displayedFuel))
            {
                fuelFill.fillAmount = displayedFuel;
            }
            }

            PulseFuel(target);
            TickGainFlash();
        }

        UpdateTide();
        EnsureStorm();
        EnsureMothFx();
        TickDots();

        if (manager == null)
        {
            return;
        }

        EnsureDots(manager.BeaconsToWin);
        PaintDots(manager.LitCount);
        RefreshIslandLabel(manager);

        ShowTimer(manager.Elapsed);

        RefreshPrompt(manager);
        UpdateGhost(manager);
        UpdateIntroCard(manager);
        RefreshStatus();

        if (manager.Won)
        {
            if (winPanel != null && !winPanel.activeSelf)
            {
                ShowWin();
            }
        }
        else if (manager.IsRoundOver && !manager.DawnPlaying && losePanel != null && !losePanel.activeSelf)
        {
            ShowLose();
        }
    }

    void BindIfNeeded()
    {
        ResolveLantern();
        ResolveMissing();
        if (bound || GameManager.Instance == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        manager.BeaconsChanged += OnBeaconsChanged;
        manager.PromptChanged += OnPromptChanged;
        manager.WonGame += ShowWin;
        manager.LostGame += ShowLose;
        manager.TimeChanged += OnTimeChanged;
        if (lantern != null)
        {
            displayedFuel = lantern.FuelNormalized;
            lastNorm = displayedFuel;
            fuelVelocity = 0f;
            fuelReady = true;
            lantern.FuelChanged += OnFuelChanged;
            OnFuelChanged(lantern.FuelNormalized);
        }

        OnBeaconsChanged(manager.LitCount, manager.BeaconsToWin);
        OnPromptChanged();
        OnTimeChanged(manager.Elapsed);
        bound = true;
    }

    void Unbind()
    {
        if (lantern != null)
        {
            lantern.FuelChanged -= OnFuelChanged;
        }

        if (!bound || GameManager.Instance == null)
        {
            bound = false;
            return;
        }

        GameManager manager = GameManager.Instance;
        manager.BeaconsChanged -= OnBeaconsChanged;
        manager.PromptChanged -= OnPromptChanged;
        manager.WonGame -= ShowWin;
        manager.LostGame -= ShowLose;
        manager.TimeChanged -= OnTimeChanged;
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

    void ResolveMissing()
    {
        if (missingResolved)
        {
            return;
        }

        missingResolved = true;
        bool missing = fuelFill == null || promptText == null || timerText == null || statusText == null;
        if (fuelFill == null)
        {
            fuelFill = FindImage("FuelFill");
        }

        if (fuelCostGhost == null)
        {
            fuelCostGhost = FindImage("FuelCostGhost");
        }

        if (fuelMeter == null)
        {
            Transform meter = FindNamed("FuelMeter");
            if (meter != null)
            {
                fuelMeter = meter as RectTransform;
            }
        }

        if (beaconDots == null)
        {
            Transform dotsRoot = FindNamed("BeaconDots");
            if (dotsRoot != null)
            {
                beaconDots = dotsRoot as RectTransform;
            }
        }

        if (promptText == null)
        {
            promptText = FindText("PromptText");
        }

        if (timerText == null)
        {
            timerText = FindText("TimerText");
        }

        if (winPanel == null)
        {
            Transform panel = FindNamed("WinPanel");
            if (panel != null)
            {
                winPanel = panel.gameObject;
            }
        }

        if (losePanel == null)
        {
            Transform panel = FindNamed("LosePanel");
            if (panel != null)
            {
                losePanel = panel.gameObject;
            }
        }

        if (pauseScreen == null)
        {
            pauseScreen = GetComponentInChildren<PauseScreen>(true);
        }

        if (winDetailText == null)
        {
            winDetailText = FindText("WinDetail");
        }

        if (loseDetailText == null)
        {
            loseDetailText = FindText("LoseDetail");
        }

        if (missing)
        {
            Debug.LogWarning("HUD widget references were not fully wired. Resolved by name once.", this);
        }
    }

    void EnsureFuelGhost()
    {
        if (fuelFill == null)
        {
            return;
        }

        PrepareRadial(fuelFill);
        if (fuelCostGhost == null)
        {
            Transform existing = fuelFill.transform.parent != null ? fuelFill.transform.parent.Find("FuelCostGhost") : null;
            if (existing == null)
            {
                existing = fuelFill.transform.Find("FuelCostGhost");
            }

            if (existing != null)
            {
                fuelCostGhost = existing.GetComponent<Image>();
            }
        }

        if (fuelCostGhost == null && fuelMeter != null)
        {
            GameObject ghostObject = new GameObject("FuelCostGhost", typeof(RectTransform), typeof(Image));
            ghostObject.transform.SetParent(fuelMeter, false);
            fuelCostGhost = ghostObject.GetComponent<Image>();
            ghostObject.SetActive(false);
        }

        if (fuelCostGhost != null)
        {
            PrepareRadial(fuelCostGhost);
            fuelCostGhost.color = GhostColor;
            fuelCostGhost.raycastTarget = false;
        }

        if (fuelGlow == null && fuelMeter != null)
        {
            Transform existingGlow = fuelMeter.Find("FuelGlow");
            if (existingGlow != null)
            {
                fuelGlow = existingGlow.GetComponent<Image>();
            }
        }

        if (fuelGlow == null && fuelMeter != null)
        {
            GameObject glowObject = new GameObject("FuelGlow", typeof(RectTransform), typeof(Image));
            glowObject.transform.SetParent(fuelMeter, false);
            glowObject.transform.SetAsFirstSibling();
            fuelGlow = glowObject.GetComponent<Image>();
        }

        if (fuelGlow != null)
        {
            RectTransform glowRect = fuelGlow.rectTransform;
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(150f, 150f);
            fuelGlow.sprite = Glow();
            fuelGlow.raycastTarget = false;
            fuelGlow.color = new Color(1f, 0.55f, 0.16f, 0.45f);
        }
    }

    static void PrepareRadial(Image image)
    {
        image.sprite = Circle();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Bottom;
        image.fillClockwise = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(86f, 86f);
        rect.anchoredPosition = Vector2.zero;
    }

    bool EnsureGraphicsSurface()
    {
        if (graphicsMenu == null)
        {
            graphicsMenu = GetComponent<GraphicsMenu>();
        }

        bool created = false;
        if (FindNamed("GraphicsPanel") == null)
        {
            CreateRuntimeGraphicsPanel();
            created = true;
        }

        if (FindNamed("FpsReadout") == null)
        {
            TMP_Text readout = MakeRuntimeText(transform, "FpsReadout", "0 FPS", 22, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -84f), new Vector2(240f, 36f), new Color(1f, 0.95f, 0.82f), TextAlignmentOptions.MidlineRight);
            readout.rectTransform.pivot = new Vector2(1f, 1f);
            readout.rectTransform.anchoredPosition = new Vector2(-36f, -84f);
            readout.gameObject.SetActive(false);
            created = true;
        }

        if (graphicsMenu == null)
        {
            graphicsMenu = gameObject.AddComponent<GraphicsMenu>();
            created = true;
        }

        return created;
    }

    void CreateRuntimeGraphicsPanel()
    {
        Sprite sprite = PanelSprite();
        GameObject panel = new GameObject("GraphicsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(700f, 560f);
        Image image = panel.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        MakeRuntimeText(rect, "Title", "Graphics", 36, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(640f, 64f), new Color(1f, 0.82f, 0.45f), TextAlignmentOptions.Center);
        MakeRuntimeText(rect, "GraphicsStatus", "Graphics: Medium", 24, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(640f, 40f), new Color(1f, 0.92f, 0.78f), TextAlignmentOptions.Center);
        MakeRuntimeButton(rect, "LowButton", "Low", sprite, new Vector2(0.5f, 0.5f), new Vector2(-243f, 70f), new Vector2(150f, 48f));
        MakeRuntimeButton(rect, "MediumButton", "Medium", sprite, new Vector2(0.5f, 0.5f), new Vector2(-81f, 70f), new Vector2(150f, 48f));
        MakeRuntimeButton(rect, "HighButton", "High", sprite, new Vector2(0.5f, 0.5f), new Vector2(81f, 70f), new Vector2(150f, 48f));
        MakeRuntimeButton(rect, "UltraButton", "Ultra", sprite, new Vector2(0.5f, 0.5f), new Vector2(243f, 70f), new Vector2(150f, 48f));
        MakeRuntimeButton(rect, "VSyncButton", "VSync: Off", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(420f, 48f));
        MakeRuntimeButton(rect, "FpsButton", "FPS counter: Off", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(420f, 48f));
        MakeRuntimeButton(rect, "BackButton", "Back", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, -168f), new Vector2(280f, 48f));
        panel.SetActive(false);
    }

    public bool ConsumePauseBack()
    {
        if (graphicsMenu == null)
        {
            graphicsMenu = GetComponent<GraphicsMenu>();
        }

        if (graphicsMenu != null && graphicsMenu.IsOpen)
        {
            graphicsMenu.Close();
            return true;
        }

        return pauseScreen != null && pauseScreen.ConsumeBack();
    }

    public void EnsureWidgets()
    {
        if (statusText == null)
        {
            statusText = FindText("StatusText");
        }

        if (fadeOverlay == null)
        {
            fadeOverlay = FindImage("FadeOverlay");
        }

        if (deathOverlay == null)
        {
            deathOverlay = FindImage("DeathOverlay");
        }

        if (penaltyText == null)
        {
            penaltyText = FindText("FuelPenalty");
        }

        if (statusText == null)
        {
            statusText = MakeRuntimeText(transform, "StatusText", "Drain x1.00", 20, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -150f), new Vector2(StatusBoxWidth, 72f), new Color(1f, 0.86f, 0.55f), TextAlignmentOptions.TopLeft);
            statusText.rectTransform.pivot = new Vector2(0f, 1f);
        }

        if (fadeOverlay == null)
        {
            fadeOverlay = MakeOverlay("FadeOverlay", null);
        }

        fadeOverlay.sprite = White();
        fadeOverlay.type = Image.Type.Simple;

        if (deathOverlay == null)
        {
            deathOverlay = MakeOverlay("DeathOverlay", VignetteSprite());
        }

        if (penaltyText == null)
        {
            penaltyText = MakeRuntimeText(transform, "FuelPenalty", "-10", 28, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -78f), new Vector2(140f, 40f), new Color(1f, 0.28f, 0.2f, 0f), TextAlignmentOptions.MidlineLeft);
            penaltyText.rectTransform.pivot = new Vector2(0f, 1f);
            penaltyText.gameObject.SetActive(false);
        }
    }

    // Quiet island name at top centre (clear of the timer, fuel gauge and status text), plus a subtitle on each panel.
    void RefreshIslandLabel(GameManager manager)
    {
        string label = manager.IslandLabel;
        if (label == shownIslandLabel)
        {
            return;
        }

        shownIslandLabel = label;
        if (islandLabelText == null)
        {
            islandLabelText = MakeRuntimeText(transform, "IslandLabel", "", 20, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(520f, 32f), IslandLabelColor, TextAlignmentOptions.Center);
        }

        islandLabelText.text = manager.IslandLabelHud;
        islandLabelText.gameObject.SetActive(!string.IsNullOrEmpty(label));
        SetPanelSubtitle(winPanel, label, 78f);
        SetPanelSubtitle(losePanel, label, 78f);
    }

    void SetPanelSubtitle(GameObject panel, string label, float drop)
    {
        if (panel == null)
        {
            return;
        }

        Transform existing = panel.transform.Find("IslandSubtitle");
        TMP_Text subtitle = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (subtitle == null)
        {
            subtitle = MakeRuntimeText(panel.transform, "IslandSubtitle", "", 22, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -drop), new Vector2(480f, 30f), IslandLabelColor, TextAlignmentOptions.Center);
        }

        subtitle.text = GameManager.Instance != null ? GameManager.Instance.IslandLabelHud : "";
        subtitle.gameObject.SetActive(!string.IsNullOrEmpty(label));
    }

    void UpdateTide()
    {
        if (tide == null)
        {
            if (tideSearched)
            {
                return;
            }

            tideSearched = true;
            tide = Tide.Instance != null ? Tide.Instance : FindAnyObjectByType<Tide>();
            if (tide == null)
            {
                return;
            }

            tideWasRising = tide.IsRising;
        }

        EnsureTideGauge();
        float level = tide.Normalized;
        bool rising = tide.IsRising;
        int seconds = Mathf.CeilToInt(tide.SecondsToTurn);
        if (tideFill != null && !Mathf.Approximately(tideFill.fillAmount, level))
        {
            tideFill.fillAmount = level;
        }

        if (tideText != null && (seconds != tideShownSecond || rising != tideWasRising))
        {
            tideShownSecond = seconds;
            tideText.text = (rising ? "^ Rising " : "v Falling ") + seconds + "s";
        }

        if (rising != tideWasRising)
        {
            tideWasRising = rising;
            tideWarned = false;
        }

        if (!tideWarned && seconds <= 5)
        {
            tideWarned = true;
            ShowTideToast("Tide turning");
        }
    }

    // Islands without wind or lightning never get the storm widgets.
    void EnsureStorm()
    {
        if (storm != null || stormSearched)
        {
            return;
        }

        stormSearched = true;
        if (Wind.Instance == null && Lightning.Instance == null)
        {
            return;
        }

        storm = gameObject.AddComponent<StormHUD>();
    }

    // Islands without a moth spawner never get the moth feedback.
    void EnsureMothFx()
    {
        if (mothFx != null || mothSearched || lantern == null || fuelMeter == null || statusText == null)
        {
            return;
        }

        if (MothSpawner.Instance == null)
        {
            return;
        }

        mothSearched = true;
        mothFx = gameObject.AddComponent<MothHUD>();
        mothFx.Bind(lantern, fuelMeter, statusText);
    }

    void EnsureTideGauge()
    {
        if (tideRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("TideGauge", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        tideRoot = root.GetComponent<RectTransform>();
        tideRoot.anchorMin = new Vector2(0f, 1f);
        tideRoot.anchorMax = new Vector2(0f, 1f);
        tideRoot.pivot = new Vector2(0f, 1f);
        tideRoot.anchoredPosition = new Vector2(36f, -228f);
        tideRoot.sizeDelta = new Vector2(260f, 30f);
        TMP_Text label = MakeRuntimeText(tideRoot, "TideLabel", "Tide", 20, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(48f, 28f), new Color(0.62f, 0.86f, 0.9f), TextAlignmentOptions.MidlineLeft);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        GameObject track = new GameObject("TideTrack", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(tideRoot, false);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(0f, 0.5f);
        trackRect.pivot = new Vector2(0f, 0.5f);
        trackRect.anchoredPosition = new Vector2(52f, 0f);
        trackRect.sizeDelta = new Vector2(64f, 10f);
        Image trackImage = track.GetComponent<Image>();
        trackImage.sprite = White();
        trackImage.color = new Color(0.05f, 0.1f, 0.14f, 0.85f);
        trackImage.raycastTarget = false;
        GameObject fill = new GameObject("TideFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(trackRect, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        tideFill = fill.GetComponent<Image>();
        tideFill.sprite = White();
        tideFill.color = new Color(0.4f, 0.78f, 0.88f, 1f);
        tideFill.type = Image.Type.Filled;
        tideFill.fillMethod = Image.FillMethod.Horizontal;
        tideFill.fillAmount = 0.5f;
        tideFill.raycastTarget = false;
        tideText = MakeRuntimeText(tideRoot, "TideText", "^ Rising", 20, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(122f, 0f), new Vector2(140f, 28f), new Color(0.62f, 0.86f, 0.9f), TextAlignmentOptions.MidlineLeft);
        tideText.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    void ShowTideToast(string line)
    {
        if (tideToast == null)
        {
            tideToast = MakeRuntimeText(transform, "TideToast", line, 30, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(520f, 48f), new Color(0.62f, 0.9f, 0.95f, 0f), TextAlignmentOptions.Center);
        }

        if (tideRoutine != null)
        {
            StopCoroutine(tideRoutine);
        }

        tideRoutine = StartCoroutine(FadeToast(tideToast, line, 2.4f, null, () => tideRoutine = null));
    }

    public void ShowLogToast(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        if (logText == null)
        {
            GameObject back = new GameObject("LogToastBack", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(transform, false);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 90f);
            backRect.sizeDelta = new Vector2(860f, 120f);
            logBack = back.GetComponent<Image>();
            logBack.sprite = White();
            logBack.color = new Color(0.03f, 0.04f, 0.07f, 0f);
            logBack.raycastTarget = false;
            logText = MakeRuntimeText(backRect, "LogToast", line, 22, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.96f, 0.9f, 0.76f, 0f), TextAlignmentOptions.Center);
            logText.rectTransform.offsetMin = new Vector2(24f, 10f);
            logText.rectTransform.offsetMax = new Vector2(-24f, -10f);
            logText.textWrappingMode = TextWrappingModes.Normal;
            logText.fontStyle = FontStyles.Italic;
        }

        if (logRoutine != null)
        {
            StopCoroutine(logRoutine);
        }

        logRoutine = StartCoroutine(FadeToast(logText, "Keeper's Log" + System.Environment.NewLine + line, 5f, logBack, () => logRoutine = null));
    }

    IEnumerator FadeToast(TMP_Text text, string line, float duration, Image back, System.Action done)
    {
        text.text = line;
        text.gameObject.SetActive(true);
        if (back != null)
        {
            back.gameObject.SetActive(true);
        }

        Color baseColor = text.color;
        Color backColor = back != null ? back.color : Color.clear;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / 0.4f) * Mathf.Clamp01((duration - elapsed) / 0.6f);
            baseColor.a = alpha;
            text.color = baseColor;
            if (back != null)
            {
                backColor.a = 0.6f * alpha;
                back.color = backColor;
            }

            yield return null;
        }

        baseColor.a = 0f;
        text.color = baseColor;
        text.gameObject.SetActive(false);
        if (back != null)
        {
            backColor.a = 0f;
            back.color = backColor;
            back.gameObject.SetActive(false);
        }

        if (done != null)
        {
            done();
        }
    }

    public void SetFadeAlpha(float alpha)
    {
        EnsureWidgets();
        if (fadeOverlay == null)
        {
            return;
        }

        Color color = Color.black;
        color.a = Mathf.Clamp01(alpha);
        fadeOverlay.color = color;
        fadeOverlay.raycastTarget = false;
        fadeOverlay.enabled = color.a > 0.01f;
    }

    public void SetDeathAmount(float amount)
    {
        EnsureWidgets();
        DeathAmount = Mathf.Clamp01(amount);
        if (deathOverlay == null)
        {
            return;
        }

        deathOverlay.sprite = VignetteSprite();
        deathOverlay.type = Image.Type.Simple;
        deathOverlay.preserveAspect = false;
        Color color = Color.black;
        color.a = DeathAmount;
        deathOverlay.color = color;
        deathOverlay.raycastTarget = false;
        deathOverlay.enabled = DeathAmount > 0.01f;
        if (DeathAmount > 0.45f && fadeOverlay != null)
        {
            float fill = Mathf.InverseLerp(0.45f, 1f, DeathAmount);
            Color black = Color.black;
            black.a = fill;
            fadeOverlay.color = black;
            fadeOverlay.enabled = fill > 0.01f;
        }
    }

    public void ShowFuelPenalty(string label)
    {
        ShowFuelPopup(label, false);
    }

    void ShowFuelPopup(string label, bool gain)
    {
        EnsureWidgets();
        if (penaltyText == null)
        {
            return;
        }

        if (penaltyRoutine != null)
        {
            StopCoroutine(penaltyRoutine);
        }

        penaltyGain = gain;
        penaltyRoutine = StartCoroutine(PenaltyTween(label));
    }

    IEnumerator PenaltyTween(string label)
    {
        penaltyText.gameObject.SetActive(true);
        penaltyText.text = label;
        RectTransform rect = penaltyText.rectTransform;
        if (!penaltyHomeReady)
        {
            penaltyHome = rect.anchoredPosition;
            penaltyHomeReady = true;
        }

        float elapsed = 0f;
        const float duration = 0.9f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float unit = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = penaltyHome + new Vector2(0f, 42f * unit);
            Color color = penaltyGain ? new Color(1f, 0.92f, 0.5f, 1f) : new Color(1f, 0.25f, 0.18f, 1f);
            color.a = 1f - Mathf.Clamp01((unit - 0.4f) / 0.6f);
            penaltyText.color = color;
            yield return null;
        }

        penaltyText.gameObject.SetActive(false);
        penaltyRoutine = null;
    }

    void RefreshStatus()
    {
        if (statusText == null || lantern == null)
        {
            return;
        }

        int cents = Mathf.RoundToInt(lantern.DrainRelative * 100f);
        int moths = Moth.LivingCount();
        int draining = lantern.MothsDraining;
        MothSpawner spawner = MothSpawner.Instance;
        int near = spawner != null ? spawner.NearCount : 0;
        bool safe = lantern.InSafeLight;
        if (statusReady && cents == statusCents && moths == statusMoths && draining == statusDraining && near == statusNear && safe == statusSafe)
        {
            return;
        }

        statusReady = true;
        statusCents = cents;
        statusMoths = moths;
        statusDraining = draining;
        statusNear = near;
        statusSafe = safe;
        string line = DrainReadout.Format(lantern.DrainRelative, draining, near, moths);
        if (safe)
        {
            line += "\nSafe light";
        }

        if (statusText.text != line)
        {
            statusText.text = line;
        }

        // While moths are draining, MothHUD owns the (violet, pulsing) colour.
        Color color = safe ? SafeStatusColor : DrainStatusColor;
        if (draining < 1 && statusText.color != color)
        {
            statusText.color = color;
        }
    }

    // Puts the base status colour back without waiting for the next text change (MothHUD calls this when its violet pulse stops).
    public void RestoreStatusColor()
    {
        if (statusText != null)
        {
            statusText.color = statusSafe ? SafeStatusColor : DrainStatusColor;
        }
    }

    Image MakeOverlay(string name, Sprite sprite)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        overlay.transform.SetAsFirstSibling();
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = overlay.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : White();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        overlay.SetActive(true);
        return image;
    }

    static Sprite VignetteSprite()
    {
        if (vignetteSprite != null)
        {
            return vignetteSprite;
        }

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float alpha = Mathf.SmoothStep(0.02f, 0.7f, radius);
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false);
        vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.name = "DeathVignette";
        return vignetteSprite;
    }

    void WireButtons()
    {
        if (buttonsWired)
        {
            return;
        }

        BindButton(winPanel, "RetryButton", Retry);
        BindButton(winPanel, "NextButton", NextIsland);
        BindButton(winPanel, "MenuButton", GoMenu);
        BindButton(losePanel, "RetryButton", Retry);
        BindButton(losePanel, "NextButton", NextIsland);
        BindButton(losePanel, "MenuButton", GoMenu);
        buttonsWired = true;
        AuditPanels();
        RefreshNextButtons(false);
    }

    void AuditPanels()
    {
        if (panelsAudited)
        {
            return;
        }

        panelsAudited = true;
        AuditPanel(winPanel);
        AuditPanel(losePanel);
    }

    void AuditPanel(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null || panelRect.rect.width < 200f || panelRect.rect.height < 160f)
        {
            Debug.LogWarning("HUD panel layout looks undersized: " + panel.name, panel);
        }
    }

    void BindButton(GameObject panel, string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButton(panel, buttonName);
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    void HidePanels()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }

        if (pauseScreen != null)
        {
            pauseScreen.Hide();
        }

        if (graphicsMenu != null)
        {
            graphicsMenu.DismissQuiet();
        }
    }

    void PulseFuel(float normalized)
    {
        Color warm = new Color(1f, 0.62f, 0.22f, 1f);
        float pulse = 0.2f;
        if (normalized < 0.2f)
        {
            pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            warm = Color.Lerp(warm, new Color(1f, 0.15f, 0.1f, 1f), pulse);
            if (fuelMeter != null)
            {
                float wobble = 1f + Mathf.Sin(Time.time * 18f) * 0.03f;
                fuelMeter.localScale = new Vector3(wobble, wobble, 1f);
            }
        }
        else if (fuelMeter != null)
        {
            fuelMeter.localScale = Vector3.one;
        }

        if (fuelFill.color != warm)
        {
            fuelFill.color = warm;
        }

        if (fuelGlow == null)
        {
            return;
        }

        float glowPulse = normalized < 0.2f ? pulse : 0.35f;
        float alpha = 0.28f + glowPulse * 0.5f + gainFlash;
        float scale = 1f + (normalized < 0.2f ? glowPulse * 0.22f : 0f) + gainFlash * 0.35f;
        Color glow = new Color(1f, 0.62f, 0.22f, Mathf.Clamp01(alpha));
        if (fuelGlow.color != glow)
        {
            fuelGlow.color = glow;
        }

        if (Mathf.Abs(fuelGlow.rectTransform.localScale.x - scale) > 0.01f)
        {
            fuelGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    void TickGainFlash()
    {
        if (gainFlash <= 0f)
        {
            return;
        }

        gainFlash = Mathf.Max(0f, gainFlash - Time.deltaTime * 1.4f);
    }

    void EnsureDots(int count)
    {
        if (beaconDots == null || count < 0)
        {
            return;
        }

        if (dots != null && dots.Length == count)
        {
            return;
        }

        for (int i = beaconDots.childCount - 1; i >= 0; i--)
        {
            Destroy(beaconDots.GetChild(i).gameObject);
        }

        paintedLit = int.MinValue;
        dots = new Image[count];
        dotGlows = new Image[count];
        dotFlare = new float[count];
        for (int i = 0; i < count; i++)
        {
            GameObject dot = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(beaconDots, false);
            RectTransform rect = dot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(18f, 18f);
            Image image = dot.GetComponent<Image>();
            image.sprite = Circle();
            image.raycastTarget = false;
            dots[i] = image;

            GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(dot.transform, false);
            RectTransform glowRect = glow.GetComponent<RectTransform>();
            glowRect.sizeDelta = new Vector2(36f, 36f);
            Image glowImage = glow.GetComponent<Image>();
            glowImage.sprite = Glow();
            glowImage.color = new Color(1f, 0.55f, 0.16f, 0f);
            glowImage.raycastTarget = false;
            dotGlows[i] = glowImage;
        }
    }

    void PaintDots(int lit)
    {
        if (dots == null || lit == paintedLit)
        {
            return;
        }

        int previous = paintedLit;
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
            {
                continue;
            }

            bool on = i < lit;
            dots[i].color = on ? DotLit : DotDim;
            if (dotGlows != null && dotGlows[i] != null)
            {
                dotGlows[i].color = new Color(1f, 0.55f, 0.16f, on ? 0.75f : 0f);
            }

            if (previous >= 0 && on && i >= previous)
            {
                dotFlare[i] = 0.4f;
                dots[i].rectTransform.localScale = new Vector3(1.5f, 1.5f, 1f);
            }
        }

        paintedLit = lit;
    }

    void TickDots()
    {
        if (dotFlare == null)
        {
            return;
        }

        for (int i = 0; i < dotFlare.Length; i++)
        {
            if (dotFlare[i] <= 0f || dots[i] == null)
            {
                continue;
            }

            dotFlare[i] = Mathf.Max(0f, dotFlare[i] - Time.deltaTime);
            float unit = 1f - dotFlare[i] / 0.4f;
            float scale = Mathf.Lerp(1.5f, 1f, unit);
            dots[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            Color flash = Color.Lerp(new Color(1f, 0.95f, 0.7f, 1f), DotLit, unit);
            dots[i].color = flash;
        }
    }

    void OnFuelChanged(float normalized)
    {
        if (!fuelReady)
        {
            displayedFuel = normalized;
            lastNorm = normalized;
            fuelReady = true;
            return;
        }

        if (normalized > lastNorm + 0.08f)
        {
            gainFlash = 0.55f;
            ShowFuelPopup("+20", true);
        }

        lastNorm = normalized;
    }

    void OnBeaconsChanged(int lit, int target)
    {
        EnsureDots(target);
        PaintDots(lit);
    }

    void OnPromptChanged()
    {
        if (GameManager.Instance != null)
        {
            RefreshPrompt(GameManager.Instance);
        }
    }

    void OnTimeChanged(float seconds)
    {
        ShowTimer(seconds);
    }

    void ShowTimer(float seconds)
    {
        if (timerText == null)
        {
            return;
        }

        int whole = seconds < 0f ? -1 : Mathf.FloorToInt(seconds);
        if (whole == shownSecond)
        {
            return;
        }

        shownSecond = whole;
        timerText.text = GameManager.FormatTime(seconds);
    }

    void OnLightFailed()
    {
        shakeRemaining = PromptShakeDuration;
    }

    void RefreshPrompt(GameManager manager)
    {
        if (promptText == null)
        {
            return;
        }

        Beacon nearest = manager.NearestBeacon;
        bool show = manager.ShowInteractPrompt && nearest != null;
        if (promptText.gameObject.activeSelf != show)
        {
            promptText.gameObject.SetActive(show);
        }

        RectTransform rect = promptText.rectTransform;
        if (!promptHomeReady)
        {
            promptHome = rect.anchoredPosition;
            promptHomeReady = true;
        }

        if (!show)
        {
            rect.anchoredPosition = promptHome;
            return;
        }

        if (rect.sizeDelta.x < 520f)
        {
            rect.sizeDelta = new Vector2(640f, Mathf.Max(52f, rect.sizeDelta.y));
        }

        int cost = Mathf.RoundToInt(nearest.FuelCost);
        bool canAfford = lantern != null && lantern.Fuel >= nearest.FuelCost;
        if (promptLine == null || cost != promptCost || canAfford != promptAfford)
        {
            promptCost = cost;
            promptAfford = canAfford;
            promptLine = canAfford
                ? "E  Light beacon (-" + cost + ")"
                : "Not enough light (-" + cost + ")";
            if (promptText.text != promptLine)
            {
                promptText.text = promptLine;
            }
        }

        Color color = canAfford ? PromptColor : WarningColor;
        if (shakeRemaining > 0f)
        {
            shakeRemaining -= Time.deltaTime;
            promptShaking = true;
            float remaining = Mathf.Clamp01(shakeRemaining / PromptShakeDuration);
            float offset = Mathf.Sin(Time.unscaledTime * 52f) * 18f * remaining;
            rect.anchoredPosition = promptHome + new Vector2(offset, 0f);
            float flash = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 36f);
            color = Color.Lerp(FlashColor, ShakeColor, flash);
            promptText.color = color;
        }
        else
        {
            shakeRemaining = 0f;
            if (promptShaking || rect.anchoredPosition != promptHome)
            {
                promptShaking = false;
                rect.anchoredPosition = promptHome;
            }

            if (promptText.color != color)
            {
                promptText.color = color;
            }
        }
    }

    void UpdateGhost(GameManager manager)
    {
        if (fuelCostGhost == null)
        {
            return;
        }

        Beacon nearest = manager.NearestBeacon;
        bool show = manager.ShowInteractPrompt && nearest != null && lantern != null && lantern.MaxFuel > 0.01f;
        float from = 0f;
        float to = 0f;
        if (show)
        {
            float cost = nearest.FuelCost / lantern.MaxFuel;
            to = displayedFuel;
            from = Mathf.Clamp01(displayedFuel - cost);
            show = to - from > 0.01f;
        }

        if (fuelCostGhost.gameObject.activeSelf != show)
        {
            fuelCostGhost.gameObject.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        if (!ghostStyled)
        {
            PrepareRadial(fuelCostGhost);
            fuelCostGhost.color = GhostColor;
            fuelCostGhost.raycastTarget = false;
            ghostStyled = true;
        }

        fuelCostGhost.fillAmount = Mathf.Clamp01(to - from);
        fuelCostGhost.rectTransform.localEulerAngles = new Vector3(0f, 0f, -from * 360f);
        if (fuelCostGhost.transform.parent != null && fuelFill != null && fuelCostGhost.transform.GetSiblingIndex() < fuelFill.transform.GetSiblingIndex())
        {
            fuelCostGhost.transform.SetSiblingIndex(fuelFill.transform.GetSiblingIndex() + 1);
        }
    }

    // The intro card shows on first visit (the game holds itself paused) and again from the pause menu's How to Play button.
    void UpdateIntroCard(GameManager manager)
    {
        bool howToOpen = pauseScreen != null && pauseScreen.HowToOpen;
        bool show = manager.IntroShowing || howToOpen;
        if (!show)
        {
            HideIntroCard();
            return;
        }

        EnsureIntroCard();
        if (introCard == null)
        {
            return;
        }

        string key = manager.LevelId + (manager.IntroShowing ? "|intro" : "|help");
        if (!introCard.activeSelf || key != shownCardKey)
        {
            shownCardKey = key;
            introTitle.text = manager.IslandLabel;
            introBody.text = BuildIntroBody(manager.IntroLines);
            introFooter.text = manager.IntroShowing ? "Press any key to begin" : "Press any key to go back";
            introCard.SetActive(true);
            introCard.transform.SetAsLastSibling();
        }

        if (howToOpen && !manager.IntroShowing && Time.unscaledTime >= pauseScreen.HowToUnlockTime && DismissInputPressed())
        {
            HideIntroCard();
            pauseScreen.CloseHowTo();
        }
    }

    void HideIntroCard()
    {
        if (introCard != null && introCard.activeSelf)
        {
            introCard.SetActive(false);
        }

        shownCardKey = null;
    }

    // Escape and P are left to GameManager, which routes them through ConsumePauseBack. Keep in step with
    // GameManager.AnyInputPressedThisFrame (first-visit card, whose release is deferred to LateUpdate); this one only
    // closes How to Play, which leaves the game paused, so no deferral is needed.
    static bool DismissInputPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboard.escapeKey.wasPressedThisFrame && !keyboard.pKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame);
    }

    static string BuildIntroBody(string[] lines)
    {
        if (lines == null)
        {
            return "";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append("<indent=1.3em><line-indent=-1.3em>\u2022  ").Append(lines[i]).Append("</line-indent></indent>");
        }

        return builder.ToString();
    }

    void EnsureIntroCard()
    {
        if (introCard != null)
        {
            return;
        }

        Sprite sprite = PanelSprite();
        introCard = new GameObject("IntroCard", typeof(RectTransform), typeof(Image));
        introCard.transform.SetParent(transform, false);
        RectTransform rect = introCard.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(780f, 520f);
        Image image = introCard.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);

        introTitle = MakeRuntimeText(rect, "Title", "", 36, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(700f, 56f), new Color(1f, 0.82f, 0.45f), TextAlignmentOptions.Center);
        introBody = MakeRuntimeText(rect, "Body", "", 28, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(680f, 350f), new Color(0.96f, 0.93f, 0.86f), TextAlignmentOptions.TopLeft);
        introBody.richText = true;
        introBody.textWrappingMode = TextWrappingModes.Normal;
        introBody.overflowMode = TextOverflowModes.Overflow;
        introBody.enableAutoSizing = true;
        introBody.fontSizeMin = 16f;
        introBody.fontSizeMax = 28f;
        introBody.paragraphSpacing = 10f;
        introFooter = MakeRuntimeText(rect, "Footer", "", 22, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(560f, 36f), IslandLabelColor, TextAlignmentOptions.Center);
        introCard.SetActive(false);
    }

    void ShowWin()
    {
        if (graphicsMenu != null)
        {
            graphicsMenu.DismissQuiet();
        }

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }

        if (pauseScreen != null)
        {
            pauseScreen.Hide();
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        RefreshNextButtons(true);
        if (winDetailText != null && GameManager.Instance != null)
        {
            float best = GameManager.Instance.BestTime;
            winDetailText.text = "Time " + GameManager.FormatTime(GameManager.Instance.Elapsed)
                + "\nBest " + GameManager.FormatTime(best);
        }
    }

    void ShowLose()
    {
        if (GameManager.Instance != null && (GameManager.Instance.Won || GameManager.Instance.DawnPlaying))
        {
            return;
        }

        if (graphicsMenu != null)
        {
            graphicsMenu.DismissQuiet();
        }

        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }

        if (pauseScreen != null)
        {
            pauseScreen.Hide();
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        RefreshNextButtons(false);
        if (loseDetailText != null)
        {
            loseDetailText.text = "The lantern went out.";
        }
    }

    void RefreshNextButtons(bool wonRound)
    {
        string next = GameManager.Instance != null ? GameManager.Instance.NextLevelScene : "";
        bool show = wonRound && !string.IsNullOrEmpty(next);
        SetButtonActive(winPanel, "NextButton", show);
        SetButtonActive(losePanel, "NextButton", false);
    }

    void Retry()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void NextIsland()
    {
        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.NextLevelScene))
        {
            return;
        }

        if (!GameManager.Instance.Won)
        {
            return;
        }

        string next = GameManager.Instance.NextLevelScene;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(next);
    }

    void GoMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GoToMenu();
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("MainMenu");
    }

    void SetButtonActive(GameObject panel, string buttonName, bool active)
    {
        Button button = FindButton(panel, buttonName);
        if (button != null)
        {
            button.gameObject.SetActive(active);
        }
    }

    Button FindButton(GameObject panel, string buttonName)
    {
        if (panel == null)
        {
            return null;
        }

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == buttonName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    Transform FindNamed(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i];
            }
        }

        return null;
    }

    TMP_Text FindText(string objectName)
    {
        Transform found = FindNamed(objectName);
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<TMP_Text>();
    }

    Image FindImage(string objectName)
    {
        Transform found = FindNamed(objectName);
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<Image>();
    }

    internal static TMP_Text MakeRuntimeText(Transform parent, string name, string value, int size, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 box, Color color, TextAlignmentOptions alignment)
    {
        EnsureFont();
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        Style(text, size, color, alignment);
        text.text = value;
        TextScaler.Notify(text);
        return text;
    }

    static void MakeRuntimeButton(Transform parent, string name, string label, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.14f, 0.16f, 0.2f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.55f, 0.25f, 1f);
        colors.pressedColor = new Color(1f, 0.7f, 0.3f, 1f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.6f);
        button.colors = colors;
        MakeRuntimeText(rect, "Label", label, 22, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.96f, 0.93f, 0.86f, 1f), TextAlignmentOptions.Center);
        RectTransform labelRect = rect.Find("Label") as RectTransform;
        if (labelRect != null)
        {
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }

    static void EnsureFont()
    {
        if (hudFont != null)
        {
            return;
        }

        hudFont = TMP_Settings.defaultFontAsset;
        if (hudFont == null)
        {
            hudFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        hudFace = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");
    }

    static void Style(TMP_Text text, int size, Color color, TextAlignmentOptions alignment)
    {
        EnsureFont();
        text.font = hudFont;
        if (hudFace != null)
        {
            text.fontSharedMaterial = hudFace;
        }

        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = false;
    }

    Sprite PanelSprite()
    {
        if (winPanel != null)
        {
            Image image = winPanel.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                return image.sprite;
            }
        }

        if (fuelFill != null && fuelFill.sprite != null)
        {
            return fuelFill.sprite;
        }

        return White();
    }

    internal static Sprite White()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "HUDWhite";
        return whiteSprite;
    }

    internal static Sprite Circle()
    {
        if (circleSprite != null)
        {
            return circleSprite;
        }

        circleSprite = Disc(64, 0.46f, 0.02f, "HUDCircle");
        return circleSprite;
    }

    static Sprite Glow()
    {
        if (glowSprite != null)
        {
            return glowSprite;
        }

        glowSprite = Disc(64, 0.08f, 0.48f, "HUDGlow");
        return glowSprite;
    }

    static Sprite Disc(int size, float solid, float feather, string spriteName)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - radius) / radius;
                float dy = (y + 0.5f - radius) / radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.InverseLerp(solid, solid + feather, dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = spriteName;
        return sprite;
    }
}
}
