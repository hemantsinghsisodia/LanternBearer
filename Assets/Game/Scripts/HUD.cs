using System.Collections;
using TMPro;
using UnityEngine;
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
    [SerializeField] GameObject pausePanel;
    [SerializeField] GraphicsMenu graphicsMenu;
    [SerializeField] Button graphicsButton;
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
    readonly Button[] pauseTab = new Button[4];
    static Sprite vignetteSprite;

    public float FadeAlpha => fadeOverlay != null ? fadeOverlay.color.a : 0f;
    public float DeathAmount { get; private set; }
    public string StatusLine => statusText != null ? statusText.text : "";
    public string PenaltyLine => penaltyText != null && penaltyText.gameObject.activeInHierarchy ? penaltyText.text : "";

    void OnEnable()
    {
        Beacon.LightFailed += OnLightFailed;
        BindIfNeeded();
        EnsureFuelGhost();
        EnsurePausePanel();
        EnsureWidgets();
        WireButtons();
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
        EnsurePausePanel();
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
            float smoothed = Mathf.SmoothDamp(displayedFuel, target, ref fuelVelocity, 0.3f);
            if (Mathf.Abs(smoothed - displayedFuel) > 0.0005f)
            {
                displayedFuel = smoothed;
                fuelFill.fillAmount = displayedFuel;
            }
            else
            {
                displayedFuel = target;
                fuelVelocity = 0f;
            }
            }

            PulseFuel(target);
            TickGainFlash();
        }

        TickDots();

        if (manager == null)
        {
            return;
        }

        EnsureDots(manager.BeaconsToWin);
        PaintDots(manager.LitCount);

        ShowTimer(manager.Elapsed);

        RefreshPrompt(manager);
        UpdateGhost(manager);
        UpdatePausePanel(manager);
        RefreshStatus();
        if (pausePanel != null && pausePanel.activeSelf && (graphicsMenu == null || !graphicsMenu.IsOpen))
        {
            GraphicsMenu.HandleTab(pauseTab);
        }

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

        if (pausePanel == null)
        {
            Transform panel = FindNamed("PausePanel");
            if (panel != null)
            {
                pausePanel = panel.gameObject;
            }
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

    void EnsurePausePanel()
    {
        if (pausePanel == null)
        {
            Transform existing = FindNamed("PausePanel");
            if (existing != null)
            {
                pausePanel = existing.gameObject;
            }
        }

        if (pausePanel == null)
        {
            Sprite sprite = PanelSprite();
            GameObject panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            panel.transform.SetAsLastSibling();
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 540f);
            Image image = panel.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

            MakeRuntimeText(rect, "Title", "Paused", 36, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 64f), new Color(1f, 0.82f, 0.45f), TextAlignmentOptions.Center);
            MakeRuntimeButton(rect, "ResumeButton", "Resume", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, 108f), new Vector2(300f, 52f));
            MakeRuntimeButton(rect, "RestartButton", "Restart", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(300f, 52f));
            MakeRuntimeButton(rect, "GraphicsButton", "Graphics", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(300f, 52f));
            MakeRuntimeButton(rect, "MenuButton", "Main Menu", sprite, new Vector2(0.5f, 0.5f), new Vector2(0f, -108f), new Vector2(300f, 52f));
            panel.SetActive(false);
            pausePanel = panel;
            buttonsWired = false;
        }

        if (EnsureGraphicsButton())
        {
            buttonsWired = false;
        }

        if (EnsureGraphicsSurface())
        {
            buttonsWired = false;
        }

        WireButtons();
    }

    bool EnsureGraphicsButton()
    {
        if (pausePanel == null)
        {
            return false;
        }

        Transform existing = pausePanel.transform.Find("GraphicsButton");
        if (existing != null)
        {
            if (graphicsButton == null)
            {
                graphicsButton = existing.GetComponent<Button>();
            }

            return false;
        }

        RectTransform panelRect = pausePanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(560f, 540f);
        }

        MovePauseButton("ResumeButton", new Vector2(0f, 108f));
        MovePauseButton("RestartButton", new Vector2(0f, 36f));
        MovePauseButton("MenuButton", new Vector2(0f, -108f));
        MakeRuntimeButton(pausePanel.transform, "GraphicsButton", "Graphics", PanelSprite(), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(300f, 52f));
        graphicsButton = pausePanel.transform.Find("GraphicsButton").GetComponent<Button>();
        return true;
    }

    void MovePauseButton(string buttonName, Vector2 position)
    {
        Transform child = pausePanel.transform.Find(buttonName);
        if (child == null)
        {
            return;
        }

        RectTransform rect = child as RectTransform;
        if (rect != null)
        {
            rect.anchoredPosition = position;
        }
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

        if (graphicsMenu == null || !graphicsMenu.IsOpen)
        {
            return false;
        }

        graphicsMenu.Close();
        return true;
    }

    void OpenGraphics()
    {
        if (graphicsMenu == null)
        {
            graphicsMenu = GetComponent<GraphicsMenu>();
        }

        if (graphicsMenu == null)
        {
            return;
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        graphicsMenu.Open(ShowPauseButtons);
    }

    void ShowPauseButtons()
    {
        GameManager manager = GameManager.Instance;
        if (pausePanel == null || manager == null || !manager.IsPaused)
        {
            return;
        }

        pausePanel.SetActive(true);
        pausePanel.transform.SetAsLastSibling();
        GraphicsMenu.Select(FindButton(pausePanel, "ResumeButton"));
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
            statusText = MakeRuntimeText(transform, "StatusText", "Drain x1.00", 20, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -150f), new Vector2(360f, 72f), new Color(1f, 0.86f, 0.55f), TextAlignmentOptions.TopLeft);
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

        int cents = Mathf.RoundToInt(lantern.EscalationMultiplier * 100f);
        int moths = Moth.LivingCount();
        bool safe = lantern.InSafeLight;
        if (statusReady && cents == statusCents && moths == statusMoths && safe == statusSafe)
        {
            return;
        }

        statusReady = true;
        statusCents = cents;
        statusMoths = moths;
        statusSafe = safe;
        string line = "Drain x" + lantern.EscalationMultiplier.ToString("0.00") + "   Moths " + moths;
        if (safe)
        {
            line += "\nSafe light";
        }

        if (statusText.text != line)
        {
            statusText.text = line;
        }

        Color color = safe ? SafeStatusColor : DrainStatusColor;
        if (statusText.color != color)
        {
            statusText.color = color;
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
        BindButton(pausePanel, "ResumeButton", ResumeGame);
        BindButton(pausePanel, "RestartButton", Retry);
        BindButton(pausePanel, "GraphicsButton", OpenGraphics);
        BindButton(pausePanel, "MenuButton", GoMenu);
        CachePauseTab();
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
        AuditPanel(pausePanel);
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

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
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

    void CachePauseTab()
    {
        pauseTab[0] = FindButton(pausePanel, "ResumeButton");
        pauseTab[1] = FindButton(pausePanel, "RestartButton");
        pauseTab[2] = graphicsButton != null ? graphicsButton : FindButton(pausePanel, "GraphicsButton");
        pauseTab[3] = FindButton(pausePanel, "MenuButton");
        LinkPauseTab();
    }

    void LinkPauseTab()
    {
        Button previous = null;
        for (int i = 0; i < pauseTab.Length; i++)
        {
            Button button = pauseTab[i];
            if (button == null)
            {
                continue;
            }

            ColorBlock colors = button.colors;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = previous;
            navigation.selectOnDown = null;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            button.navigation = navigation;
            if (previous != null)
            {
                Navigation above = previous.navigation;
                above.selectOnDown = button;
                previous.navigation = above;
            }

            previous = button;
        }
    }

    void UpdatePausePanel(GameManager manager)
    {
        if (pausePanel == null)
        {
            return;
        }

        bool graphicsOpen = graphicsMenu != null && graphicsMenu.IsOpen;
        bool show = manager.IsPaused && !graphicsOpen;
        if (pausePanel.activeSelf != show)
        {
            pausePanel.SetActive(show);
            if (show)
            {
                pausePanel.transform.SetAsLastSibling();
                GraphicsMenu.Select(pauseTab[0] != null ? pauseTab[0] : FindButton(pausePanel, "ResumeButton"));
            }
        }
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

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
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

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
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

    void ResumeGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Resume();
        }
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

    static TMP_Text MakeRuntimeText(Transform parent, string name, string value, int size, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 box, Color color, TextAlignmentOptions alignment)
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

    static Sprite White()
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

    static Sprite Circle()
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
