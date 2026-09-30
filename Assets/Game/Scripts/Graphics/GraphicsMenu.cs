using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
public class GraphicsMenu : MonoBehaviour
{
    public const string FpsKey = "LanternKeeperFps";

    static readonly Color NormalButton = new Color(0.14f, 0.16f, 0.2f, 1f);
    static readonly Color SelectedButton = new Color(0.92f, 0.58f, 0.18f, 1f);
    static readonly Color FocusButton = new Color(0.85f, 0.55f, 0.25f, 1f);

    [SerializeField] GameObject panel;
    [SerializeField] Button lowButton;
    [SerializeField] Button mediumButton;
    [SerializeField] Button highButton;
    [SerializeField] Button ultraButton;
    [SerializeField] Button vsyncButton;
    [SerializeField] Button fpsButton;
    [SerializeField] Button backButton;
    [SerializeField] GameObject statusLabel;
    [SerializeField] GameObject fpsReadout;

    readonly StringBuilder fpsBuilder = new StringBuilder(12);
    readonly Button[] tabOrder = new Button[7];
    Action closed;
    float fpsAccumulated;
    int fpsFrames;
    int shownFps = int.MinValue;
    bool wired;

    public bool IsOpen => panel != null && panel.activeSelf;

    public static bool FpsEnabled
    {
        get { return PlayerPrefs.GetInt(FpsKey, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(FpsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        Wire();
        ApplyFpsVisibility();
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void Update()
    {
        if (!wired)
        {
            Wire();
        }

        TickFps();
        if (IsOpen)
        {
            HandleTab(tabOrder);
        }
    }

    public void Open(Action onClosed)
    {
        Wire();
        closed = onClosed;
        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        Refresh();
        Select(ButtonFor(GraphicsQuality.Current));
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        panel.SetActive(false);
        Action callback = closed;
        closed = null;
        if (callback != null)
        {
            callback();
        }
    }

    public void DismissQuiet()
    {
        closed = null;
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public static void HandleTab(Button[] order)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || order == null || !keyboard.tabKey.wasPressedThisFrame)
        {
            return;
        }

        EventSystem system = EventSystem.current;
        if (system == null || system.currentSelectedGameObject == null)
        {
            return;
        }

        int current = -1;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] != null && order[i].gameObject == system.currentSelectedGameObject)
            {
                current = i;
                break;
            }
        }

        if (current < 0)
        {
            return;
        }

        int step = keyboard.shiftKey.isPressed ? -1 : 1;
        for (int n = 1; n <= order.Length; n++)
        {
            int index = current + step * n;
            index %= order.Length;
            if (index < 0)
            {
                index += order.Length;
            }

            Button next = order[index];
            if (next != null && next.gameObject.activeInHierarchy && next.interactable)
            {
                system.SetSelectedGameObject(next.gameObject);
                return;
            }
        }
    }

    public static void Select(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy)
        {
            return;
        }

        EventSystem system = EventSystem.current;
        if (system == null)
        {
            return;
        }

        system.SetSelectedGameObject(null);
        system.SetSelectedGameObject(button.gameObject);
    }

    void OnQuality(GraphicsProfile profile)
    {
        Refresh();
    }

    void Wire()
    {
        Resolve();
        if (!Ready())
        {
            return;
        }

        if (wired)
        {
            return;
        }

        Listen(lowButton, ChooseLow);
        Listen(mediumButton, ChooseMedium);
        Listen(highButton, ChooseHigh);
        Listen(ultraButton, ChooseUltra);
        Listen(vsyncButton, ToggleVSync);
        Listen(fpsButton, ToggleFps);
        Listen(backButton, Close);
        tabOrder[0] = lowButton;
        tabOrder[1] = mediumButton;
        tabOrder[2] = highButton;
        tabOrder[3] = ultraButton;
        tabOrder[4] = vsyncButton;
        tabOrder[5] = fpsButton;
        tabOrder[6] = backButton;
        KeepFocusColor(vsyncButton);
        KeepFocusColor(fpsButton);
        KeepFocusColor(backButton);
        LinkRow();
        wired = true;
        Refresh();
    }

    bool Ready()
    {
        return panel != null
            && lowButton != null
            && mediumButton != null
            && highButton != null
            && ultraButton != null
            && vsyncButton != null
            && fpsButton != null
            && backButton != null
            && statusLabel != null
            && fpsReadout != null;
    }

    void Resolve()
    {
        if (panel == null)
        {
            Transform found = FindNamed(transform, "GraphicsPanel");
            if (found != null)
            {
                panel = found.gameObject;
            }
        }

        lowButton = Or(lowButton, "LowButton");
        mediumButton = Or(mediumButton, "MediumButton");
        highButton = Or(highButton, "HighButton");
        ultraButton = Or(ultraButton, "UltraButton");
        vsyncButton = Or(vsyncButton, "VSyncButton");
        fpsButton = Or(fpsButton, "FpsButton");
        backButton = Or(backButton, "BackButton");
        if (statusLabel == null && panel != null)
        {
            Transform status = FindNamed(panel.transform, "GraphicsStatus");
            if (status != null)
            {
                statusLabel = status.gameObject;
            }
        }

        if (fpsReadout == null)
        {
            Transform readout = FindNamed(transform, "FpsReadout");
            if (readout != null)
            {
                fpsReadout = readout.gameObject;
            }
        }
    }

    Button Or(Button current, string objectName)
    {
        if (current != null || panel == null)
        {
            return current;
        }

        Transform found = FindNamed(panel.transform, objectName);
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<Button>();
    }

    void ChooseLow()
    {
        Choose(GraphicsLevel.Low);
    }

    void ChooseMedium()
    {
        Choose(GraphicsLevel.Medium);
    }

    void ChooseHigh()
    {
        Choose(GraphicsLevel.High);
    }

    void ChooseUltra()
    {
        Choose(GraphicsLevel.Ultra);
    }

    void Choose(GraphicsLevel level)
    {
        GraphicsQuality.Set(level);
        Refresh();
        Select(ButtonFor(level));
    }

    void ToggleVSync()
    {
        GraphicsQuality.VSync = !GraphicsQuality.VSync;
        Refresh();
    }

    void ToggleFps()
    {
        FpsEnabled = !FpsEnabled;
        ApplyFpsVisibility();
        Refresh();
    }

    void Refresh()
    {
        GraphicsLevel level = GraphicsQuality.Current;
        SetText(statusLabel, "Graphics: " + level);
        Paint(lowButton, level == GraphicsLevel.Low);
        Paint(mediumButton, level == GraphicsLevel.Medium);
        Paint(highButton, level == GraphicsLevel.High);
        Paint(ultraButton, level == GraphicsLevel.Ultra);
        SetButtonLabel(vsyncButton, GraphicsQuality.VSync ? "VSync: On" : "VSync: Off");
        SetButtonLabel(fpsButton, FpsEnabled ? "FPS counter: On" : "FPS counter: Off");
        ApplyFpsVisibility();
    }

    void ApplyFpsVisibility()
    {
        if (fpsReadout == null)
        {
            return;
        }

        bool show = FpsEnabled;
        if (fpsReadout.activeSelf == show)
        {
            return;
        }

        fpsReadout.SetActive(show);
        if (show)
        {
            fpsAccumulated = 0f;
            fpsFrames = 0;
            shownFps = int.MinValue;
        }
    }

    void TickFps()
    {
        if (fpsReadout == null || !fpsReadout.activeSelf)
        {
            return;
        }

        float frame = Time.unscaledDeltaTime;
        if (frame <= 0f)
        {
            return;
        }

        fpsAccumulated += frame;
        fpsFrames++;
        if (fpsAccumulated < 0.5f)
        {
            return;
        }

        float average = fpsAccumulated / fpsFrames;
        int fps = average > 0.0001f ? Mathf.RoundToInt(1f / average) : 0;
        fpsAccumulated = 0f;
        fpsFrames = 0;
        if (fps == shownFps)
        {
            return;
        }

        shownFps = fps;
        fpsBuilder.Length = 0;
        fpsBuilder.Append(fps);
        fpsBuilder.Append(" FPS");
        SetText(fpsReadout, fpsBuilder.ToString());
    }

    Button ButtonFor(GraphicsLevel level)
    {
        switch (level)
        {
            case GraphicsLevel.Low:
                return lowButton;
            case GraphicsLevel.High:
                return highButton;
            case GraphicsLevel.Ultra:
                return ultraButton;
            default:
                return mediumButton;
        }
    }

    void LinkRow()
    {
        Link(lowButton, null, vsyncButton, null, mediumButton);
        Link(mediumButton, null, vsyncButton, lowButton, highButton);
        Link(highButton, null, vsyncButton, mediumButton, ultraButton);
        Link(ultraButton, null, vsyncButton, highButton, null);
        Link(vsyncButton, mediumButton, fpsButton, null, null);
        Link(fpsButton, vsyncButton, backButton, null, null);
        Link(backButton, fpsButton, null, null, null);
    }

    static void Link(Button button, Button up, Button down, Button left, Button right)
    {
        if (button == null)
        {
            return;
        }

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        navigation.selectOnLeft = left;
        navigation.selectOnRight = right;
        button.navigation = navigation;
    }

    static void Listen(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    static void Paint(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
        }

        if (image != null)
        {
            image.color = Color.white;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = selected ? SelectedButton : NormalButton;
        colors.highlightedColor = FocusButton;
        colors.pressedColor = new Color(1f, 0.72f, 0.32f, 1f);
        colors.selectedColor = selected ? SelectedButton : FocusButton;
        button.colors = colors;
    }

    static void KeepFocusColor(Button button)
    {
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.selectedColor = FocusButton;
        colors.highlightedColor = FocusButton;
        button.colors = colors;
    }

    static void SetButtonLabel(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        Transform label = button.transform.Find("Label");
        if (label != null)
        {
            SetText(label.gameObject, value);
        }
    }

    static void SetText(GameObject host, string value)
    {
        if (host == null)
        {
            return;
        }

        TMP_Text tmp = host.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            if (tmp.text != value)
            {
                tmp.text = value;
            }

            return;
        }

        Text legacy = host.GetComponent<Text>();
        if (legacy != null && legacy.text != value)
        {
            legacy.text = value;
        }
    }

    static Transform FindNamed(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
}
