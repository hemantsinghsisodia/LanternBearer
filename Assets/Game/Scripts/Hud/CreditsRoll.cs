using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
// The credits roll that follows the Island 5 finale: a column of text scrolls up over a darkened band while the dawn stays visible above
// and below it. It runs on unscaled time, scrolls at 64 px/s at the 1080p reference height, and ends when the last line leaves the band.
// Any key, mouse button or gamepad button skips it, but only after ArmSeconds and after every key has been released once, so a key the
// player was holding when the finale ended cannot skip it. Both ways out raise ExitStarted (the HUD fades the music) and then
// MenuRequested (the HUD loads the main menu; without a listener the roll loads it itself).
// The text is built at run time from Resources/Credits.txt (a copy of CREDITS.txt kept by CreditsSync) through CreditsFormat, and from
// the template labels the UIBuilder made, so the fonts and colours come from the UI kit.
public class CreditsRoll : MonoBehaviour
{
    public const float PixelsPerSecond = 64f;
    public const float ReferenceHeight = 1080f;
    public const float ArmSeconds = 1f;
    public const float FadeInSeconds = 1f;
    public const float HintFadeSeconds = 0.6f;
    public const float HintAlphaTarget = 0.55f;
    public const string HintText = "Press any key to skip";
    public const float ExitFadeSeconds = 0.8f;
    public const string MenuScene = "MainMenu";
    public const string Closing = "Thank you for keeping the light.";
    public const string MadeWithUnity = "Made with Unity";
    public const string ResourceName = "Credits";

    // Test hook: the scroll runs this many times faster (1 in the game). Reset in OnDestroy.
    public static float TestSpeed = 1f;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private ThemedLabel gameTitleTemplate;
    [SerializeField] private ThemedLabel madeByTemplate;
    [SerializeField] private ThemedLabel headingTemplate;
    [SerializeField] private ThemedLabel entryTemplate;
    [SerializeField] private ThemedLabel bylineTemplate;
    [SerializeField] private ThemedLabel closingTemplate;
    [SerializeField] private ThemedLabel skipHint;
    [SerializeField] private CanvasGroup skipHintGroup;

    private readonly List<GameObject> built = new List<GameObject>();
    private readonly StringBuilder text = new StringBuilder();
    private bool started;
    private bool rolling;
    private bool exiting;
    private bool armed;
    private bool previousHeld;
    private bool sawRelease;
    private float elapsed;
    private float offset;
    private float scrollLength;

    // The HUD fades the music (seconds) when the roll starts to leave, and loads the menu when the fade is done.
    public event Action<float> ExitStarted;
    public event Action MenuRequested;

    // Test seam: when set, replaces the Input System read ("is any key, mouse button or gamepad button down").
    public Func<bool> InputHeld { get; set; }

    public bool Started { get { return started; } }
    public bool Rolling { get { return rolling && !exiting; } }
    public bool Exiting { get { return exiting; } }
    public bool Armed { get { return armed; } }
    public float Alpha { get { return group != null ? group.alpha : 0f; } }
    public string BuiltText { get { return text.ToString(); } }
    public float ScrollLength { get { return scrollLength; } }
    // Seconds from the start to the last line leaving the band at 1080p (the spec asks for a short roll: 75 s or less).
    public float Seconds { get { return scrollLength / (PixelsPerSecond * UnitsPerReferencePixel()); } }
    public float HintAlpha { get { return skipHintGroup != null ? skipHintGroup.alpha : 0f; } }
    public float Offset { get { return offset; } }
    public RectTransform Content { get { return content; } }
    public RectTransform Viewport { get { return viewport; } }

    public void Configure(CanvasGroup canvasGroup, GameObject rollPanel, RectTransform band, RectTransform column, ThemedLabel gameTitle, ThemedLabel madeBy,
        ThemedLabel heading, ThemedLabel entry, ThemedLabel byline, ThemedLabel closing, ThemedLabel hint, CanvasGroup hintGroup)
    {
        group = canvasGroup;
        panel = rollPanel;
        viewport = band;
        content = column;
        gameTitleTemplate = gameTitle;
        madeByTemplate = madeBy;
        headingTemplate = heading;
        entryTemplate = entry;
        bylineTemplate = byline;
        closingTemplate = closing;
        skipHint = hint;
        skipHintGroup = hintGroup;
    }

    private void OnEnable()
    {
        if (!started && panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        TestSpeed = 1f;
    }

    // Builds the lines and starts rolling. Does nothing the second time.
    public void Begin()
    {
        if (started)
        {
            return;
        }

        started = true;
        Build();
        elapsed = 0f;
        offset = 0f;
        armed = false;
        previousHeld = false;
        sawRelease = false;
        group.alpha = 0f;
        panel.SetActive(true);
        Place();
        rolling = true;
    }

    // Removes every built line (also any left in the scene by an earlier edit-mode build) and, before the roll starts, hides the panel again.
    public void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        built.Clear();
        text.Length = 0;
        if (!started)
        {
            panel.SetActive(false);
        }
    }

    // (Re)builds the column from Resources/Credits.txt. Section headings come from CreditsFormat, in file order, each with its entries below.
    public void Build()
    {
        Clear();
        // The layout only measures an active hierarchy; the group is transparent (alpha 0 as built) until Begin fades it in.
        panel.SetActive(true);

        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        List<CreditEntry> entries = CreditsFormat.Parse(asset != null ? asset.text : "");

        AddLine(gameTitleTemplate, "Lantern Keeper");
        AddLine(madeByTemplate, "Made by " + Application.companyName);
        AddGap(60f);
        string heading = null;
        for (int i = 0; i < entries.Count; i++)
        {
            CreditEntry entry = entries[i];
            if (entry.heading != heading)
            {
                heading = entry.heading;
                AddGap(i == 0 ? 0f : 30f);
                AddLine(headingTemplate, heading);
                AddGap(8f);
            }
            else
            {
                AddGap(6f);
            }

            AddLine(entryTemplate, entry.title);
            AddLine(bylineTemplate, "by " + entry.author + " · " + entry.licence);
        }

        AddGap(60f);
        AddLine(madeByTemplate, MadeWithUnity);
        AddGap(44f);
        AddLine(closingTemplate, Closing);

        LayoutRebuilderForce();
        scrollLength = viewport.rect.height + content.rect.height;
        Place();
    }

    private void AddLine(ThemedLabel template, string line)
    {
        GameObject clone = Instantiate(template.gameObject, content, false);
        clone.name = template.name;
        clone.SetActive(true);
        ThemedLabel label = clone.GetComponent<ThemedLabel>();
        label.Text.text = line;
        built.Add(clone);
        text.Append(line).Append('\n');
    }

    private void AddGap(float height)
    {
        if (height <= 0f)
        {
            return;
        }

        GameObject gap = new GameObject("Gap", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
        gap.transform.SetParent(content, false);
        UnityEngine.UI.LayoutElement element = gap.GetComponent<UnityEngine.UI.LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        built.Add(gap);
    }

    private void LayoutRebuilderForce()
    {
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }

    // Canvas units per reference pixel: the canvas is 1080 tall at the reference size and scales with the screen.
    private float UnitsPerReferencePixel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        return root != null && root.rect.height > 1f ? root.rect.height / ReferenceHeight : 1f;
    }

    // Content top starts at the band's bottom edge and rises by `offset`.
    private void Place()
    {
        content.anchoredPosition = new Vector2(0f, -viewport.rect.height + offset);
    }

    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    // Moves the roll by dt seconds of unscaled time; tests may call it with fixed steps.
    public void Tick(float dt)
    {
        if (!rolling || exiting)
        {
            return;
        }

        dt = Mathf.Max(0f, dt);
        bool held = ReadHeld();
        // A skip needs a fresh press edge once armed: a key pressed before the arm moment and held across it does not skip.
        bool edge = held && !previousHeld;
        previousHeld = held;
        if (armed && edge)
        {
            Exit();
            return;
        }

        if (!held)
        {
            sawRelease = true;
        }

        elapsed += dt;
        armed = elapsed >= ArmSeconds && sawRelease;
        group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt / FadeInSeconds);
        if (skipHintGroup != null)
        {
            skipHintGroup.alpha = Mathf.MoveTowards(skipHintGroup.alpha, armed ? HintAlphaTarget : 0f, dt / HintFadeSeconds);
        }

        offset += PixelsPerSecond * UnitsPerReferencePixel() * Mathf.Max(0.01f, TestSpeed) * dt;
        Place();
        if (offset >= scrollLength)
        {
            Exit();
        }
    }

    private bool ReadHeld()
    {
        Func<bool> seam = InputHeld;
        return seam != null ? seam() : AnyInputDown();
    }

    // Any key, mouse button or gamepad button, null-safe when a device is missing.
    public static bool AnyInputDown()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.anyKey.isPressed || keyboard.anyKey.wasPressedThisFrame))
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed
            || mouse.forwardButton.isPressed || mouse.backButton.isPressed))
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            UnityEngine.InputSystem.Controls.ButtonControl[] buttons =
            {
                pad.buttonSouth, pad.buttonEast, pad.buttonWest, pad.buttonNorth, pad.startButton, pad.selectButton,
                pad.leftShoulder, pad.rightShoulder, pad.leftTrigger, pad.rightTrigger, pad.leftStickButton, pad.rightStickButton,
                pad.dpad.up, pad.dpad.down, pad.dpad.left, pad.dpad.right
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].isPressed)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Exit()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        if (ExitStarted != null)
        {
            ExitStarted.Invoke(ExitFadeSeconds);
        }

        StartCoroutine(ExitRoutine());
    }

    private IEnumerator ExitRoutine()
    {
        float start = group.alpha;
        float t = 0f;
        while (t < ExitFadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / ExitFadeSeconds));
            yield return null;
        }

        group.alpha = 0f;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (MenuRequested != null)
        {
            MenuRequested.Invoke();
        }
        else
        {
            SceneManager.LoadScene(MenuScene);
        }
    }
}
}
