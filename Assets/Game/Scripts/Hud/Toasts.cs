using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
public enum ToastKind { Info, Tide, LogPage }

// Small toasts near the top. They queue first-in first-out and show one at a time, each fading in and out on unscaled time.
// Info and Tide use the ink panel; a Keeper's Log page uses the parchment of the log book.
public class Toasts : MonoBehaviour
{
    public const float FadeIn = 0.3f;
    public const float FadeOut = 0.4f;
    public const int MaxQueued = 8;
    const float InfoSeconds = 2.4f;
    const float LogSeconds = 5f;
    const float NarrowWidth = 460f;
    const float WideWidth = 820f;

    private struct Item
    {
        public string text;
        public ToastKind kind;
    }

    [SerializeField] private UITheme theme;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform box;
    [SerializeField] private Image backing;
    [SerializeField] private Graphic[] frame;
    [SerializeField] private ThemedLabel header;
    [SerializeField] private ThemedLabel body;

    private readonly Queue<Item> queue = new Queue<Item>();
    private bool active;
    private bool closed;
    private Item current;
    private float age;
    private float duration;

    public bool IsShowing { get { return active; } }
    public int QueuedCount { get { return queue.Count; } }
    public string CurrentText { get { return active ? current.text : null; } }
    public ToastKind CurrentKind { get { return current.kind; } }
    public float Alpha { get { return group != null ? group.alpha : 0f; } }
    public Color BackingColour { get { return backing.color; } }
    public Color BodyColour { get { return body.Text.color; } }
    public ThemedLabel Header { get { return header; } }
    public ThemedLabel Body { get { return body; } }

    public void Configure(UITheme newTheme, CanvasGroup canvasGroup, RectTransform toastBox, Image back, Graphic[] frameLines, ThemedLabel headerLabel, ThemedLabel bodyLabel)
    {
        theme = newTheme;
        group = canvasGroup;
        box = toastBox;
        backing = back;
        frame = frameLines;
        header = headerLabel;
        body = bodyLabel;
    }

    private void OnEnable()
    {
        SetVisible(active);
    }

    // Once a result panel is up (Clear with close = true) nothing more is queued.
    public void Show(string text, ToastKind kind)
    {
        if (closed || string.IsNullOrEmpty(text))
        {
            return;
        }
        if (queue.Count >= MaxQueued)
        {
            queue.Dequeue();
        }
        Item item;
        item.text = text;
        item.kind = kind;
        queue.Enqueue(item);
        if (!active)
        {
            StartNext();
        }
    }

    public void Clear(bool close = false)
    {
        closed = close;
        queue.Clear();
        active = false;
        age = 0f;
        SetVisible(false);
    }

    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    // Advances the fade by dt seconds. Update calls it with unscaled time; tests call it with fixed steps.
    public void Tick(float dt)
    {
        if (!active)
        {
            StartNext();
            return;
        }
        age += Mathf.Max(0f, dt);
        if (age >= duration)
        {
            active = false;
            SetVisible(false);
            StartNext();
            return;
        }
        group.alpha = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((duration - age) / FadeOut);
    }

    private void StartNext()
    {
        if (queue.Count == 0)
        {
            return;
        }
        current = queue.Dequeue();
        active = true;
        age = 0f;
        bool log = current.kind == ToastKind.LogPage;
        duration = log ? LogSeconds : InfoSeconds;
        Style(current.kind);
        body.Text.text = current.text;
        box.sizeDelta = new Vector2(log ? WideWidth : NarrowWidth, box.sizeDelta.y);
        SetVisible(true);
        group.alpha = 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(box);
    }

    private void Style(ToastKind kind)
    {
        bool log = kind == ToastKind.LogPage;
        Color back = log ? theme.parchment : theme.inkPanel;
        back.a = log ? 0.96f : 0.92f;
        backing.color = back;
        Color line = log ? theme.parchmentEdge : theme.brassLine;
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i].color = line;
        }
        header.gameObject.SetActive(log);
        header.SetColourOverride(true, theme.ribbon);
        body.SetColourOverride(true, log ? theme.inkText : kind == ToastKind.Tide ? TideChip.TideColour : theme.textPrimary);
    }

    private void SetVisible(bool on)
    {
        if (box != null && box.gameObject.activeSelf != on)
        {
            box.gameObject.SetActive(on);
        }
        if (!on && group != null)
        {
            group.alpha = 0f;
        }
    }
}
}
