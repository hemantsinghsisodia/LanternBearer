using System.Collections;
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

    [SerializeField] Image fuelFill;
    [SerializeField] Image fuelCostGhost;
    [SerializeField] RectTransform fuelMeter;
    [SerializeField] RectTransform beaconDots;
    [SerializeField] Text promptText;
    [SerializeField] Text timerText;
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] GameObject pausePanel;
    [SerializeField] Text winDetailText;
    [SerializeField] Text loseDetailText;
    [SerializeField] Text statusText;
    [SerializeField] Image fadeOverlay;
    [SerializeField] Image deathOverlay;
    [SerializeField] Text penaltyText;
    [SerializeField] Lantern lantern;

    Image[] dots;
    bool bound;
    bool missingResolved;
    bool lanternResolved;
    bool promptOverflow;
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
            fuelFill.fillAmount = lantern.FuelNormalized;
            PulseFuel(lantern.FuelNormalized);
        }

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
        if (fuelCostGhost != null || fuelFill == null)
        {
            return;
        }

        Transform existing = fuelFill.transform.Find("FuelCostGhost");
        if (existing != null)
        {
            fuelCostGhost = existing.GetComponent<Image>();
            return;
        }

        GameObject ghostObject = new GameObject("FuelCostGhost", typeof(RectTransform), typeof(Image));
        ghostObject.transform.SetParent(fuelFill.transform, false);
        RectTransform rect = ghostObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.85f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fuelCostGhost = ghostObject.GetComponent<Image>();
        fuelCostGhost.sprite = fuelFill.sprite != null ? fuelFill.sprite : White();
        fuelCostGhost.color = GhostColor;
        fuelCostGhost.raycastTarget = false;
        ghostObject.SetActive(false);
    }

    void EnsurePausePanel()
    {
        if (pausePanel != null)
        {
            return;
        }

        Transform existing = FindNamed("PausePanel");
        if (existing != null)
        {
            pausePanel = existing.gameObject;
            return;
        }

        Font font = BuiltinFont();
        Sprite sprite = PanelSprite();
        GameObject panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panel.transform.SetAsLastSibling();
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(720f, 420f);
        Image image = panel.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        MakeRuntimeText(rect, "Title", "Paused", 36, font, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(500f, 56f), new Color(1f, 0.82f, 0.45f));
        MakeRuntimeButton(rect, "ResumeButton", "Resume", font, sprite, new Vector2(0f, 40f));
        MakeRuntimeButton(rect, "RestartButton", "Restart", font, sprite, new Vector2(0f, -30f));
        MakeRuntimeButton(rect, "MenuButton", "Main Menu", font, sprite, new Vector2(0f, -100f));
        panel.SetActive(false);
        pausePanel = panel;
        buttonsWired = false;
        WireButtons();
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

        Font font = BuiltinFont();
        if (statusText == null)
        {
            statusText = MakeRuntimeText(transform, "StatusText", "Drain x1.00", 20, font, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -168f), new Vector2(280f, 64f), new Color(1f, 0.86f, 0.55f));
            statusText.alignment = TextAnchor.UpperLeft;
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
            penaltyText = MakeRuntimeText(transform, "FuelPenalty", "-10", 28, font, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -70f), new Vector2(120f, 40f), new Color(1f, 0.28f, 0.2f, 0f));
            penaltyText.alignment = TextAnchor.MiddleLeft;
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
        EnsureWidgets();
        if (penaltyText == null)
        {
            return;
        }

        if (penaltyRoutine != null)
        {
            StopCoroutine(penaltyRoutine);
        }

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
            Color color = new Color(1f, 0.25f, 0.18f, 1f);
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
        BindButton(pausePanel, "MenuButton", GoMenu);
        buttonsWired = true;
        FitEndButtons();
        RefreshNextButtons(false);
    }

    void FitEndButtons()
    {
        FitPanel(winPanel);
        FitPanel(losePanel);
    }

    void FitPanel(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null && panelImage.sprite != null)
        {
            panelImage.type = Image.Type.Sliced;
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(720f, 420f);
        }

        FitButton(panel, "RetryButton", new Vector2(-220f, -140f), new Vector2(200f, 52f));
        FitButton(panel, "NextButton", new Vector2(0f, -140f), new Vector2(220f, 52f));
        FitButton(panel, "MenuButton", new Vector2(220f, -140f), new Vector2(200f, 52f));
    }

    void FitButton(GameObject panel, string name, Vector2 position, Vector2 size)
    {
        Button button = FindButton(panel, name);
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        Image image = button.GetComponent<Image>();
        if (image != null && image.sprite != null)
        {
            image.type = Image.Type.Sliced;
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 22;
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
    }

    void PulseFuel(float normalized)
    {
        Color warm = new Color(1f, 0.62f, 0.22f, 1f);
        if (normalized < 0.2f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
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

        fuelFill.color = warm;
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

        dots = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject dot = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(beaconDots, false);
            RectTransform rect = dot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(18f, 18f);
            dots[i] = dot.GetComponent<Image>();
        }
    }

    void PaintDots(int lit)
    {
        if (dots == null || lit == paintedLit)
        {
            return;
        }

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
            {
                continue;
            }

            dots[i].color = i < lit ? DotLit : DotDim;
        }

        paintedLit = lit;
    }

    void OnFuelChanged(float normalized)
    {
        if (fuelFill != null)
        {
            fuelFill.fillAmount = normalized;
        }
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
            rect.sizeDelta = new Vector2(560f, Mathf.Max(48f, rect.sizeDelta.y));
        }

        if (!promptOverflow)
        {
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptOverflow = true;
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
            from = Mathf.Clamp01((lantern.Fuel - nearest.FuelCost) / lantern.MaxFuel);
            to = Mathf.Clamp01(lantern.Fuel / lantern.MaxFuel);
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
            fuelCostGhost.color = GhostColor;
            fuelCostGhost.raycastTarget = false;
            ghostStyled = true;
        }

        RectTransform rect = fuelCostGhost.rectTransform;
        rect.anchorMin = new Vector2(0f, from);
        rect.anchorMax = new Vector2(1f, to);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (rect.parent != null && rect.GetSiblingIndex() != rect.parent.childCount - 1)
        {
            rect.SetAsLastSibling();
        }
    }

    void UpdatePausePanel(GameManager manager)
    {
        if (pausePanel == null)
        {
            return;
        }

        bool show = manager.IsPaused;
        if (pausePanel.activeSelf != show)
        {
            pausePanel.SetActive(show);
            if (show)
            {
                pausePanel.transform.SetAsLastSibling();
            }
        }
    }

    void ShowWin()
    {
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

    Text FindText(string objectName)
    {
        Transform found = FindNamed(objectName);
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<Text>();
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

    static Text MakeRuntimeText(Transform parent, string name, string value, int size, Font font, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 box, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static void MakeRuntimeButton(Transform parent, string name, string label, Font font, Sprite sprite, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(280f, 52f);
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
        MakeRuntimeText(rect, "Label", label, 22, font, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.92f, 0.78f));
        RectTransform labelRect = rect.Find("Label") as RectTransform;
        if (labelRect != null)
        {
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
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

    static Font BuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
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
}
}
