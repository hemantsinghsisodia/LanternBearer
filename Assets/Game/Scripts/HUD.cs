using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
// Coordinator for the in-game HUD. It reads GameManager, Lantern, the player, Tide and the storm sources and pushes plain values
// into the widgets of the GameHud prefab (Scripts/Hud). The intro card, result panels and toasts below are still code-made;
// Phase F2 task 4 replaces them.
public class HUD : MonoBehaviour
{
    const float PromptShakeDuration = 0.3f;

    static readonly Color PromptColor = new Color(1f, 0.9f, 0.7f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.38f, 0.28f, 1f);
    static readonly Color FlashColor = new Color(1f, 0.05f, 0.04f, 1f);
    static readonly Color IslandLabelColor = new Color(0.86f, 0.76f, 0.58f, 0.85f);
    static readonly Color ShakeColor = new Color(1f, 0.28f, 0.16f, 1f);

    static Sprite whiteSprite;
    static Sprite vignetteSprite;
    static TMP_FontAsset hudFont;
    static Material hudFace;

    [SerializeField] FuelGaugeWidget fuelGauge;
    [SerializeField] BeaconRoofs roofs;
    [SerializeField] DrainIcons drainIcons;
    [SerializeField] TimerLabel timerLabel;
    [SerializeField] IslandLabel islandLabel;
    [SerializeField] TideChip tideChip;
    [SerializeField] StormChip stormChip;
    [SerializeField] TMP_Text promptText;
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] PauseScreen pauseScreen;
    [SerializeField] TMP_Text winDetailText;
    [SerializeField] TMP_Text loseDetailText;
    [SerializeField] Image fadeOverlay;
    [SerializeField] Image deathOverlay;
    [SerializeField] Lantern lantern;

    PlayerController player;
    Transform cameraTransform;
    Tide tide;
    bool bound;
    bool lanternBound;
    bool missingResolved;
    bool lanternResolved;
    bool promptShaking;
    bool buttonsWired;
    bool panelsAudited;
    bool tideSearched;
    bool tideWasRising;
    bool tideWarned;
    bool stormSearched;
    bool hasStorm;
    bool promptAfford;
    bool promptHomeReady;
    int shownSecond = int.MinValue;
    int shownTotal = int.MinValue;
    int shownLit = int.MinValue;
    int promptCost = int.MinValue;
    string promptLine;
    string shownIslandLabel;
    string shownCardKey;
    float shakeRemaining;
    Vector2 promptHome;
    TMP_Text logText;
    TMP_Text tideToast;
    Image logBack;
    Coroutine logRoutine;
    Coroutine tideRoutine;
    GameObject introCard;
    TMP_Text introTitle;
    TMP_Text introBody;
    TMP_Text introFooter;

    public float FadeAlpha => fadeOverlay != null ? fadeOverlay.color.a : 0f;
    public float DeathAmount { get; private set; }

    void OnEnable()
    {
        Beacon.LightFailed += OnLightFailed;
        BindIfNeeded();
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
        WireButtons();
        EnsureWidgets();
        if (timerLabel != null)
        {
            timerLabel.SetFormatter(GameManager.FormatTime);
        }
    }

    void Update()
    {
        UpdateFuelAndDrain();
        UpdateTide();
        UpdateStorm();

        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        UpdateRoofs(manager);
        RefreshIslandLabel(manager);
        ShowTimer(manager.Elapsed);
        RefreshPrompt(manager);
        UpdateIntroCard(manager);

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

    // The tide chip (Island 3) and the storm chip (Island 4) share a slot; only the one whose source exists shows.
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

        bool rising = tide.IsRising;
        if (tideChip != null)
        {
            tideChip.Show(true, rising, tide.Normalized);
        }

        if (rising != tideWasRising)
        {
            tideWasRising = rising;
            tideWarned = false;
        }

        if (!tideWarned && Mathf.CeilToInt(tide.SecondsToTurn) <= 5)
        {
            tideWarned = true;
            ShowTideToast("Tide turning");
        }
    }

    // Islands without wind or lightning never show the storm chip or pulse the screen edge on a Shade steal.
    void UpdateStorm()
    {
        if (!stormSearched)
        {
            stormSearched = true;
            hasStorm = Wind.Instance != null || Lightning.Instance != null;
            if (hasStorm && GetComponent<StealPulse>() == null)
            {
                gameObject.AddComponent<StealPulse>();
            }
        }

        if (!hasStorm || stormChip == null)
        {
            return;
        }

        float angle = 0f;
        float strength = 0f;
        Wind wind = Wind.Instance;
        if (wind != null)
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            Vector3 direction = wind.Direction;
            direction.y = 0f;
            if (forward.sqrMagnitude > 0.0001f && direction.sqrMagnitude > 0.0001f)
            {
                angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            }

            strength = wind.Phase == WindPhase.Gust ? wind.Strength01 : 0f;
        }

        float thunder = 0f;
        Lightning lightning = Lightning.Instance;
        if (lightning != null && lightning.Phase == LightningPhase.Thunder)
        {
            thunder = UserSettings.ReduceFlashing ? 0.8f : 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 14f));
        }

        stormChip.Show(true, angle, strength, thunder);
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
        if (!bound || GameManager.Instance == null)
        {
            bound = false;
            return;
        }

        GameManager manager = GameManager.Instance;
        manager.PromptChanged -= OnPromptChanged;
        manager.WonGame -= ShowWin;
        manager.LostGame -= ShowLose;
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

    // Safety net for scenes whose references were lost: finds the prefab parts and panels once, by type and name.
    void ResolveMissing()
    {
        if (missingResolved)
        {
            return;
        }

        missingResolved = true;
        if (fuelGauge == null)
        {
            fuelGauge = GetComponentInChildren<FuelGaugeWidget>(true);
            roofs = roofs != null ? roofs : GetComponentInChildren<BeaconRoofs>(true);
            drainIcons = drainIcons != null ? drainIcons : GetComponentInChildren<DrainIcons>(true);
            timerLabel = timerLabel != null ? timerLabel : GetComponentInChildren<TimerLabel>(true);
            islandLabel = islandLabel != null ? islandLabel : GetComponentInChildren<IslandLabel>(true);
            tideChip = tideChip != null ? tideChip : GetComponentInChildren<TideChip>(true);
            stormChip = stormChip != null ? stormChip : GetComponentInChildren<StormChip>(true);
            Debug.LogWarning("HUD widget references were not wired. Resolved by type once.", this);
        }

        promptText = promptText != null ? promptText : FindText("PromptText");
        winDetailText = winDetailText != null ? winDetailText : FindText("WinDetail");
        loseDetailText = loseDetailText != null ? loseDetailText : FindText("LoseDetail");
        pauseScreen = pauseScreen != null ? pauseScreen : GetComponentInChildren<PauseScreen>(true);
        winPanel = winPanel != null ? winPanel : FindObject("WinPanel");
        losePanel = losePanel != null ? losePanel : FindObject("LosePanel");
    }

    public bool ConsumePauseBack()
    {
        return pauseScreen != null && pauseScreen.ConsumeBack();
    }

    public void EnsureWidgets()
    {
        if (fadeOverlay == null)
        {
            fadeOverlay = FindImage("FadeOverlay");
        }

        if (deathOverlay == null)
        {
            deathOverlay = FindImage("DeathOverlay");
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
    }

    // Quiet island name at top centre, plus a subtitle on each panel.
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
        deathOverlay.sprite = VignetteSprite();
        deathOverlay.type = Image.Type.Simple;
        deathOverlay.preserveAspect = false;
        Color color = Color.black;
        color.a = DeathAmount;
        deathOverlay.color = color;
        deathOverlay.raycastTarget = false;
        deathOverlay.enabled = DeathAmount > 0.01f;
        if (DeathAmount > 0.45f)
        {
            float fill = Mathf.InverseLerp(0.45f, 1f, DeathAmount);
            Color black = Color.black;
            black.a = fill;
            fadeOverlay.color = black;
            fadeOverlay.enabled = fill > 0.01f;
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
    }

    void OnLightFailed()
    {
        shakeRemaining = PromptShakeDuration;
    }

    void OnPromptChanged()
    {
        if (GameManager.Instance != null)
        {
            RefreshPrompt(GameManager.Instance);
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

            builder.Append("<indent=1.3em><line-indent=-1.3em>•  ").Append(lines[i]).Append("</line-indent></indent>");
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

    GameObject FindObject(string objectName)
    {
        Transform found = FindNamed(objectName);
        return found != null ? found.gameObject : null;
    }

    TMP_Text FindText(string objectName)
    {
        Transform found = FindNamed(objectName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    Image FindImage(string objectName)
    {
        Transform found = FindNamed(objectName);
        return found != null ? found.GetComponent<Image>() : null;
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
}
}
