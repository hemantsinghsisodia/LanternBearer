using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
public class HUD : MonoBehaviour
{
    [SerializeField] Image fuelFill;
    [SerializeField] RectTransform fuelMeter;
    [SerializeField] RectTransform beaconDots;
    [SerializeField] Text promptText;
    [SerializeField] Text timerText;
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] Text winDetailText;
    [SerializeField] Text loseDetailText;

    Lantern lantern;
    Image[] dots;
    bool bound;
    bool buttonsWired;

    void OnEnable()
    {
        BindIfNeeded();
        WireButtons();
        HidePanels();
    }

    void OnDisable()
    {
        Unbind();
    }

    void Update()
    {
        BindIfNeeded();
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

        if (timerText != null)
        {
            timerText.text = GameManager.FormatTime(manager.Elapsed);
        }

        if (promptText != null)
        {
            bool show = manager.ShowInteractPrompt && !manager.IsRoundOver;
            if (promptText.gameObject.activeSelf != show)
            {
                promptText.gameObject.SetActive(show);
            }

            promptText.text = "Press E";
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
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

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

    void ResolveMissing()
    {
        if (fuelFill == null)
        {
            fuelFill = FindImage("FuelFill");
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

        if (winDetailText == null)
        {
            winDetailText = FindText("WinDetail");
        }

        if (loseDetailText == null)
        {
            loseDetailText = FindText("LoseDetail");
        }
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
        if (dots == null)
        {
            return;
        }

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
            {
                continue;
            }

            dots[i].color = i < lit
                ? new Color(1f, 0.55f, 0.16f, 1f)
                : new Color(0.28f, 0.3f, 0.34f, 1f);
        }
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
        bool show = GameManager.Instance != null && GameManager.Instance.ShowInteractPrompt;
        if (promptText != null)
        {
            promptText.gameObject.SetActive(show);
            promptText.text = "Press E";
        }
    }

    void OnTimeChanged(float seconds)
    {
        if (timerText != null)
        {
            timerText.text = GameManager.FormatTime(seconds);
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

        SceneManager.LoadScene(GameManager.Instance.NextLevelScene);
    }

    void GoMenu()
    {
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
}
}
