using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace LanternKeeper
{
/// <summary>
/// On-screen touch controls for phones and tablets: a floating stick on the left half, drag-look on the right half, and
/// Jump / Light / Sprint buttons bottom-right with Pause top-right. One DontDestroyOnLoad canvas built in code, shown only
/// while TouchMode.Active in an island scene with no menu, card, credits or finale up. Fingers come from EnhancedTouch and
/// are claimed by the region they started in (FingerClaims), so the stick and the look-drag run together.
/// </summary>
public sealed class TouchControlsOverlay : MonoBehaviour
{
    public const float FadeSeconds = 0.15f;
    const int SortingOrder = 5;
    const float ButtonHitPadding = 1.12f;

    enum ButtonKind
    {
        Jump,
        Light,
        Sprint,
        Pause
    }

    sealed class TouchButton
    {
        public ButtonKind Kind;
        public float Diameter;
        public RectTransform Rect;
        public RectTransform Visual;
        public Image Background;
        public Image Ring;
        public Image Icon;
        public TMP_Text Label;
        public int PressedBy = int.MinValue;
    }

    static TouchControlsOverlay instance;

    public static TouchControlsOverlay Instance => instance;

    readonly FingerClaims claims = new FingerClaims();
    readonly VirtualStick stick = new VirtualStick();
    readonly List<TouchButton> buttons = new List<TouchButton>();
    readonly List<TMP_Text> labels = new List<TMP_Text>();
    readonly List<int> live = new List<int>();

    UITheme theme;
    bool built;
    Canvas canvas;
    CanvasGroup group;
    RectTransform content;
    RectTransform safeArea;
    RectTransform stickBase;
    RectTransform stickKnob;
    Rect lastSafe;
    Vector2Int lastScreen;
    bool shown;
    bool sprintOn;
    bool sentSprint;
    bool ownsMove;
    bool ownsSprint;
    int stickFinger = int.MinValue;
    GameManager creditsOwner;
    CreditsRoll credits;

    /// <summary>True while the controls are meant to be up (the fade may still be running).</summary>
    public bool IsShown => shown;

    public float Alpha => group != null ? group.alpha : 0f;

    public bool SprintToggled => sprintOn;

    public bool StickActive => stick.Active;

    public Vector2 JumpButtonScreenPosition => ButtonScreenPosition(ButtonKind.Jump);

    public Vector2 LightButtonScreenPosition => ButtonScreenPosition(ButtonKind.Light);

    public Vector2 SprintButtonScreenPosition => ButtonScreenPosition(ButtonKind.Sprint);

    public Vector2 PauseButtonScreenPosition => ButtonScreenPosition(ButtonKind.Pause);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_WEBGL || UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return;
        }

        if (instance != null)
        {
            return;
        }

        // Domain reload may be off, so an instance from an earlier play session can still be in the DontDestroyOnLoad scene.
        TouchControlsOverlay existing = FindFirstObjectByType<TouchControlsOverlay>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("TouchControlsOverlay");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<TouchControlsOverlay>();
#endif
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        ReleaseAll();
        if (instance == this)
        {
            EnhancedTouchSupport.Disable();
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // ---------- visibility ----------

    /// <summary>Whether the controls should be up this frame. The rules mirror CameraFollow.MenuOpen plus the cards, credits and finale.</summary>
    static bool ShouldShow(GameManager manager, bool creditsRolling)
    {
        if (!TouchMode.Active || manager == null)
        {
            return false;
        }

        if (manager.IsPaused || manager.ControlsLocked || manager.IntroShowing)
        {
            return false;
        }

        if (manager.IsRoundOver && !manager.DawnPlaying)
        {
            return false;
        }

        if (manager.FinaleRuns && manager.WinStarted)
        {
            return false;
        }

        return !creditsRolling;
    }

    bool CreditsRolling(GameManager manager)
    {
        if (manager == null)
        {
            credits = null;
            creditsOwner = null;
            return false;
        }

        if (creditsOwner != manager)
        {
            creditsOwner = manager;
            credits = FindFirstObjectByType<CreditsRoll>(FindObjectsInactive.Include);
        }

        return credits != null && credits.Started;
    }

    void Update()
    {
        GameManager manager = GameManager.Instance;
        bool show = ShouldShow(manager, CreditsRolling(manager));
        if (show && !built)
        {
            Build();
        }

        if (show && !shown)
        {
            EnterShown();
        }

        shown = show;
        if (built)
        {
            float target = show ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / FadeSeconds);
            bool visible = show || group.alpha > 0f;
            if (content.gameObject.activeSelf != visible)
            {
                content.gameObject.SetActive(visible);
            }
        }

        if (!show)
        {
            ReleaseAll();
            return;
        }

        UpdateSafeArea();
        ProcessTouches();
        ApplyOutputs();
        UpdateVisuals(manager);
    }

    // Fingers already down when the controls appear stay inert until they lift.
    void EnterShown()
    {
        claims.Clear();
        if (!EnhancedTouchSupport.enabled)
        {
            return;
        }

        var touches = ETouch.activeTouches;
        for (int i = 0; i < touches.Count; i++)
        {
            ETouch touch = touches[i];
            if (touch.phase != ETouchPhase.Ended && touch.phase != ETouchPhase.Canceled)
            {
                claims.Ignore(touch.touchId, touch.screenPosition);
            }
        }
    }

    // ---------- touch handling ----------

    float ReferenceScale()
    {
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        return scale > 0.0001f ? scale : 1f;
    }

    void ProcessTouches()
    {
        if (!EnhancedTouchSupport.enabled)
        {
            return;
        }

        float width = Screen.width;
        float dpi = Screen.dpi;
        float scale = ReferenceScale();
        live.Clear();
        var touches = ETouch.activeTouches;
        for (int i = 0; i < touches.Count; i++)
        {
            ETouch touch = touches[i];
            int id = touch.touchId;
            bool ended = touch.phase == ETouchPhase.Ended || touch.phase == ETouchPhase.Canceled;
            Vector2 position = touch.screenPosition;
            if (!ended)
            {
                live.Add(id);
            }

            FingerClaims.Region region = claims.RegionOf(id);
            if (region == FingerClaims.Region.None)
            {
                if (ended)
                {
                    continue;
                }

                // Classify by where the finger went down; two events merged into one frame still start from the right place.
                Vector2 start = touch.startScreenPosition;
                TouchButton hit = ButtonAt(start);
                region = claims.Begin(id, start, width, hit != null);
                if (region == FingerClaims.Region.Button && hit != null)
                {
                    hit.PressedBy = id;
                    Press(hit);
                }
                else if (region == FingerClaims.Region.Stick)
                {
                    stickFinger = id;
                    stick.Down(start / scale);
                }
            }

            Vector2 delta = claims.Move(id, position);
            if (region == FingerClaims.Region.Stick && id == stickFinger)
            {
                stick.Drag(position / scale);
            }
            else if (FingerClaims.TurnsCamera(region) && delta != Vector2.zero)
            {
                CameraFollow.ExternalLookDelta += FingerClaims.LookDegrees(delta, dpi);
            }

            if (ended)
            {
                claims.End(id);
            }
        }

        claims.ReleaseExcept(live, null);
        if (stick.Active && claims.RegionOf(stickFinger) != FingerClaims.Region.Stick)
        {
            stick.Up();
            stickFinger = int.MinValue;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            TouchButton button = buttons[i];
            if (button.PressedBy != int.MinValue && claims.RegionOf(button.PressedBy) == FingerClaims.Region.None)
            {
                button.PressedBy = int.MinValue;
            }
        }
    }

    void Press(TouchButton button)
    {
        switch (button.Kind)
        {
            case ButtonKind.Jump:
                GameInput.PressJump();
                break;
            case ButtonKind.Light:
                GameInput.PressInteract();
                break;
            case ButtonKind.Pause:
                GameInput.PressPause();
                break;
            case ButtonKind.Sprint:
                sprintOn = !sprintOn;
                break;
        }
    }

    void ApplyOutputs()
    {
        stick.Tick(Time.unscaledDeltaTime);
        bool sprint = sprintOn || stick.AutoSprint;
        if (stick.Active)
        {
            PlayerController.ExternalMove = stick.Output;
            PlayerController.ExternalSprint = sprint;
            ownsMove = true;
            ownsSprint = true;
        }
        else
        {
            ReleaseExternals();
        }

        GameInput.SetTouchSprint(sprint);
        sentSprint = true;
    }

    // Zeroes the statics only if this overlay wrote them, so tests and probes that drive ExternalMove themselves are left alone.
    void ReleaseExternals()
    {
        if (ownsMove)
        {
            PlayerController.ExternalMove = Vector2.zero;
            ownsMove = false;
        }

        if (ownsSprint)
        {
            PlayerController.ExternalSprint = false;
            ownsSprint = false;
        }
    }

    void ReleaseAll()
    {
        claims.Clear();
        stick.Up();
        stickFinger = int.MinValue;
        sprintOn = false;
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].PressedBy = int.MinValue;
        }

        ReleaseExternals();
        if (sentSprint)
        {
            GameInput.SetTouchSprint(false);
            sentSprint = false;
        }
    }

    // ---------- layout and visuals ----------

    static Vector2 ButtonCenter(TouchButton button)
    {
        return button.Rect.TransformPoint(button.Rect.rect.center);
    }

    Vector2 ButtonScreenPosition(ButtonKind kind)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].Kind == kind)
            {
                return ButtonCenter(buttons[i]);
            }
        }

        return Vector2.zero;
    }

    TouchButton ButtonAt(Vector2 screenPosition)
    {
        float scale = ReferenceScale();
        TouchButton best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < buttons.Count; i++)
        {
            TouchButton button = buttons[i];
            float reach = button.Diameter * 0.5f * scale * ButtonHitPadding;
            float distance = Vector2.Distance(ButtonCenter(button), screenPosition);
            if (distance <= reach && distance < bestDistance)
            {
                best = button;
                bestDistance = distance;
            }
        }

        return best;
    }

    void UpdateSafeArea()
    {
        Rect safe = Screen.safeArea;
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        if (safe == lastSafe && screen == lastScreen)
        {
            return;
        }

        lastSafe = safe;
        lastScreen = screen;
        if (screen.x <= 0 || screen.y <= 0)
        {
            return;
        }

        safeArea.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
        safeArea.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
    }

    void UpdateVisuals(GameManager manager)
    {
        bool promptUp = manager != null && manager.ShowInteractPrompt;
        float time = Time.unscaledTime;
        bool auto = stick.AutoSprint;
        Color amber = theme.amber;
        Color ink = theme.inkPanel;
        for (int i = 0; i < buttons.Count; i++)
        {
            TouchButton button = buttons[i];
            bool pressed = button.PressedBy != int.MinValue;
            float visualScale = pressed ? 0.92f : 1f;
            Color background = new Color(ink.r, ink.g, ink.b, 0.72f);
            Color ring = new Color(amber.r, amber.g, amber.b, 0.8f);
            Color icon = theme.textPrimary;
            float dim = 1f;

            if (button.Kind == ButtonKind.Light)
            {
                if (promptUp)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6f);
                    visualScale *= 1f + 0.07f * pulse;
                    ring = Color.Lerp(amber, theme.glowCore, pulse);
                    background = Color.Lerp(background, new Color(amber.r, amber.g, amber.b, 0.55f), 0.5f + 0.5f * pulse);
                }
                else
                {
                    dim = 0.45f;
                }
            }
            else if (button.Kind == ButtonKind.Sprint && (sprintOn || auto))
            {
                background = new Color(amber.r, amber.g, amber.b, 0.92f);
                ring = theme.glowCore;
                icon = new Color(ink.r, ink.g, ink.b, 1f);
            }

            if (pressed)
            {
                background = Color.Lerp(background, theme.glowCore, 0.35f);
            }

            background.a *= dim;
            ring.a *= dim;
            icon.a *= Mathf.Lerp(0.5f, 1f, dim);
            button.Background.color = background;
            button.Ring.color = ring;
            button.Icon.color = icon;
            if (button.Label != null)
            {
                button.Label.color = icon;
            }

            button.Visual.localScale = new Vector3(visualScale, visualScale, 1f);
        }

        bool stickUp = stick.Active;
        if (stickBase.gameObject.activeSelf != stickUp)
        {
            stickBase.gameObject.SetActive(stickUp);
        }

        if (stickUp)
        {
            stickBase.anchoredPosition = stick.Origin;
            stickKnob.anchoredPosition = stick.KnobOffset;
        }
    }

    // ---------- building ----------

    // The HUD's theme asset is loaded by the island's UI, so it is found among the loaded objects. Without it, the defaults give the same palette.
    static UITheme FindTheme()
    {
        UITheme[] found = Resources.FindObjectsOfTypeAll<UITheme>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].uiFontStrong != null)
            {
                return found[i];
            }
        }

        UITheme made = ScriptableObject.CreateInstance<UITheme>();
        made.hideFlags = HideFlags.HideAndDontSave;
        return made;
    }

    void Build()
    {
        built = true;
        theme = FindTheme();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // No GraphicRaycaster: the overlay reads fingers itself and never takes UI clicks, hidden or not.
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        content = NewRect("Content", transform);
        Stretch(content);
        safeArea = NewRect("SafeArea", content);
        Stretch(safeArea);

        stickBase = NewRect("StickBase", content);
        SetAnchor(stickBase, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        stickBase.sizeDelta = new Vector2(VirtualStick.Radius * 2f + 30f, VirtualStick.Radius * 2f + 30f);
        Image baseImage = stickBase.gameObject.AddComponent<Image>();
        baseImage.sprite = TouchSprites.Ring;
        baseImage.raycastTarget = false;
        baseImage.color = new Color(theme.textPrimary.r, theme.textPrimary.g, theme.textPrimary.b, 0.35f);
        stickKnob = NewRect("StickKnob", stickBase);
        SetAnchor(stickKnob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        stickKnob.sizeDelta = new Vector2(96f, 96f);
        Image knobImage = stickKnob.gameObject.AddComponent<Image>();
        knobImage.sprite = TouchSprites.Disc;
        knobImage.raycastTarget = false;
        knobImage.color = new Color(theme.amber.r, theme.amber.g, theme.amber.b, 0.55f);
        stickBase.gameObject.SetActive(false);

        AddButton(ButtonKind.Jump, 140f, new Vector2(1f, 0f), new Vector2(-150f, 150f), TouchSprites.Jump, "Jump", 22);
        AddButton(ButtonKind.Light, 120f, new Vector2(1f, 0f), new Vector2(-335f, 105f), TouchSprites.Flame, "Light", 20);
        AddButton(ButtonKind.Sprint, 120f, new Vector2(1f, 0f), new Vector2(-125f, 330f), TouchSprites.Sprint, "Run", 20);
        AddButton(ButtonKind.Pause, 90f, new Vector2(1f, 1f), new Vector2(-90f, -90f), TouchSprites.Pause, null, 0);

        for (int i = 0; i < labels.Count; i++)
        {
            if (theme.uiFontStrong != null)
            {
                labels[i].font = theme.uiFontStrong;
            }
        }

        lastSafe = default(Rect);
        lastScreen = Vector2Int.zero;
        Canvas.ForceUpdateCanvases();
    }

    void AddButton(ButtonKind kind, float diameter, Vector2 anchor, Vector2 position, Sprite iconSprite, string text, int labelPx)
    {
        TouchButton button = new TouchButton { Kind = kind, Diameter = diameter };
        button.Rect = NewRect(kind + "Button", safeArea);
        SetAnchor(button.Rect, anchor, anchor, new Vector2(0.5f, 0.5f));
        button.Rect.anchoredPosition = position;
        button.Rect.sizeDelta = new Vector2(diameter, diameter);

        button.Visual = NewRect("Visual", button.Rect);
        Stretch(button.Visual);
        button.Background = AddImage("Background", button.Visual, TouchSprites.Disc);
        Stretch(button.Background.rectTransform);
        button.Ring = AddImage("Ring", button.Visual, TouchSprites.Ring);
        Stretch(button.Ring.rectTransform);

        bool hasLabel = !string.IsNullOrEmpty(text);
        float iconSize = diameter * (hasLabel ? 0.44f : 0.5f);
        button.Icon = AddImage("Icon", button.Visual, iconSprite);
        SetAnchor(button.Icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        button.Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        button.Icon.rectTransform.anchoredPosition = new Vector2(0f, hasLabel ? diameter * 0.1f : 0f);

        if (hasLabel)
        {
            RectTransform labelRect = NewRect("Label", button.Visual);
            SetAnchor(labelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            labelRect.sizeDelta = new Vector2(diameter, labelPx + 8f);
            labelRect.anchoredPosition = new Vector2(0f, -diameter * 0.3f);
            TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = labelPx;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            button.Label = label;
            labels.Add(label);
        }

        buttons.Add(button);
    }

    static Image AddImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
    }
}
}
