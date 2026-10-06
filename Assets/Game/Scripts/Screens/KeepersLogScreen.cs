using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
// One island's slice of the Keeper's Log.
public struct IslandLog
{
    public string header;
    public string[] entries;
    public int found;
}

// The Keeper's Log as an open parchment book. Islands 1..N in order, each starting on a new spread; an island's
// entries flow onto further spreads when a page fills. Prev/Next, the bumpers and the Left/Right arrows turn the
// page (a short unscaled fade); Esc, B or Close shuts the book and returns focus to the opener.
// Page capacity is measured from the real text boxes at the current text scale, then verified, so larger text
// makes more pages and never overflows.
public class KeepersLogScreen : MonoBehaviour
{
    const float FallbackPageWidth = 600f;
    const float FallbackPageHeight = 540f;
    const string Sample = "The lamp chose you, keeper; keep the flame small and your steps careful. ";

    [SerializeField] private ParchmentSpread spread;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button firstSelected;

    private struct View
    {
        public int island;
        public string header;
        public string sub;
        public LogSpread text;
    }

    private readonly List<View> views = new List<View>();
    private IslandLog[] islands = new IslandLog[0];
    private int spreadIndex;
    private Selectable opener;
    private Action onClosed;
    private Coroutine turning;

    public bool IsOpen { get { return gameObject.activeInHierarchy; } }
    public ParchmentSpread Spread { get { return spread; } }
    public Button CloseButton { get { return closeButton; } }
    public Button FirstSelected { get { return firstSelected; } }
    public int SpreadIndex { get { return spreadIndex; } }
    public int SpreadCount { get { return views.Count; } }

    public void Configure(ParchmentSpread book, Button close, Button first)
    {
        spread = book;
        closeButton = close;
        firstSelected = first;
    }

    public void Open(Selectable openedBy, Action closedCallback)
    {
        opener = openedBy;
        onClosed = closedCallback;
        MainMenu menu = GetComponentInParent<MainMenu>();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResetFade();
        Populate(menu != null ? menu.LogIslands() : islands);
        Select(firstSelected != null && firstSelected.interactable ? firstSelected : closeButton);
    }

    // Paginates every island at the current text scale and shows the first spread.
    public void Populate(IslandLog[] source)
    {
        islands = source ?? new IslandLog[0];
        Repaginate(0, null);
        Show(false);
    }

    // Stops any page-turn fade and leaves the pages fully visible.
    private void ResetFade()
    {
        if (turning != null)
        {
            StopCoroutine(turning);
            turning = null;
        }
        if (spread != null && spread.Pages != null)
        {
            spread.Pages.alpha = 1f;
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        ResetFade();
        gameObject.SetActive(false);
        Selectable back = opener;
        Action callback = onClosed;
        opener = null;
        onClosed = null;
        if (back != null && back.gameObject.activeInHierarchy)
        {
            Select(back);
        }
        else if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        if (callback != null)
        {
            callback();
        }
    }

    // Back (Esc or gamepad B) closes the book. Returns true if it did.
    public bool ConsumeBack()
    {
        if (!IsOpen)
        {
            return false;
        }
        Close();
        return true;
    }

    public void TurnTo(int index)
    {
        if (views.Count == 0)
        {
            return;
        }
        int clamped = Mathf.Clamp(index, 0, views.Count - 1);
        bool moved = clamped != spreadIndex;
        spreadIndex = clamped;
        Show(moved);
    }

    private void OnEnable()
    {
        if (spread != null)
        {
            spread.Prev += TurnBack;
            spread.Next += TurnForward;
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
        UserSettings.Changed += OnSettingsChanged;
    }

    private void OnDisable()
    {
        if (spread != null)
        {
            spread.Prev -= TurnBack;
            spread.Next -= TurnForward;
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
        UserSettings.Changed -= OnSettingsChanged;
        ResetFade();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            ConsumeBack();
            return;
        }
        if ((keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame))
        {
            TurnBack();
        }
        else if ((keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame))
        {
            TurnForward();
        }
    }

    private void TurnBack()
    {
        TurnTo(spreadIndex - 1);
    }

    private void TurnForward()
    {
        TurnTo(spreadIndex + 1);
    }

    // Text size changed while the book is open: repaginate and stay on the same island.
    private void OnSettingsChanged()
    {
        if (!IsOpen || views.Count == 0)
        {
            return;
        }
        TextScaler scaler = GetComponent<TextScaler>();
        if (scaler != null)
        {
            scaler.Reapply();
        }
        View current = views[Mathf.Clamp(spreadIndex, 0, views.Count - 1)];
        Repaginate(current.island, Anchor(current.text.left));
        Show(false);
    }

    // The start of the page's first line: enough to find the same text again after the pages are re-cut.
    private static string Anchor(string leftPage)
    {
        if (string.IsNullOrEmpty(leftPage))
        {
            return null;
        }
        int end = leftPage.IndexOf((char)10);
        string line = end >= 0 ? leftPage.Substring(0, end) : leftPage;
        return line.Length > 24 ? line.Substring(0, 24) : line;
    }

    private void Repaginate(int keepIsland, string anchor)
    {
        float width;
        float height;
        PageSize(out width, out height);
        int capacity = EstimateCapacity(width, height);
        // Verify against the real text box; shrink the capacity until every page fits.
        for (int attempt = 0; attempt < 24; attempt++)
        {
            views.Clear();
            bool fits = true;
            for (int i = 0; i < islands.Length; i++)
            {
                List<LogSpread> spreads = LogPaging.Paginate(islands[i].entries, islands[i].found, capacity);
                int total = islands[i].entries == null ? 0 : islands[i].entries.Length;
                for (int s = 0; s < spreads.Count; s++)
                {
                    views.Add(new View
                    {
                        island = i,
                        header = islands[i].header,
                        sub = "pages " + Mathf.Clamp(islands[i].found, 0, total) + " of " + total,
                        text = spreads[s]
                    });
                    fits = fits && Fits(spreads[s].left, width, height) && Fits(spreads[s].right, width, height);
                }
            }
            if (fits || capacity <= 16)
            {
                break;
            }
            capacity = Mathf.Max(16, Mathf.FloorToInt(capacity * 0.9f));
        }
        // Stay on the island, on the first spread that holds the text that was at the top of the page.
        spreadIndex = 0;
        bool islandFound = false;
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i].island != keepIsland)
            {
                continue;
            }
            if (!islandFound)
            {
                spreadIndex = i;
                islandFound = true;
            }
            if (!string.IsNullOrEmpty(anchor)
                && (views[i].text.left.Contains(anchor) || views[i].text.right.Contains(anchor)))
            {
                spreadIndex = i;
                break;
            }
        }
    }

    private void PageSize(out float width, out float height)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)spread.LeftBody.transform;
        width = rect.rect.width;
        height = rect.rect.height;
        if (width < 100f || height < 100f)
        {
            width = FallbackPageWidth;
            height = FallbackPageHeight;
        }
    }

    // First guess at characters per page: lines that fit times characters per line, less a margin for paragraph gaps.
    private int EstimateCapacity(float width, float height)
    {
        TMP_Text body = spread.LeftBody;
        float lineHeight = Mathf.Max(1f, body.GetPreferredValues("Ag", width, 0f).y);
        string text = "";
        for (int i = 0; i < 6; i++)
        {
            text += Sample;
        }
        float sampleHeight = body.GetPreferredValues(text, width, 0f).y;
        float sampleLines = Mathf.Max(1f, Mathf.Round(sampleHeight / lineHeight));
        float charsPerLine = text.Length / sampleLines;
        float lines = Mathf.Floor(height / lineHeight);
        return Mathf.Max(16, Mathf.FloorToInt(lines * charsPerLine * 0.85f));
    }

    private bool Fits(string text, float width, float height)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }
        return spread.LeftBody.GetPreferredValues(text, width, 0f).y <= height + 0.5f;
    }

    private void Show(bool fade)
    {
        if (views.Count == 0)
        {
            spread.SetSpread("", "", "", "");
            SetInteractable(spread.PrevButton, false);
            SetInteractable(spread.NextButton, false);
            LinkNavigation();
            return;
        }
        View view = views[spreadIndex];
        string left = view.text.left;
        if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(view.text.right))
        {
            left = "No pages are kept for this island.";
        }
        spread.SetSpread(view.header, view.sub, left, view.text.right);
        SetInteractable(spread.PrevButton, spreadIndex > 0);
        SetInteractable(spread.NextButton, spreadIndex < views.Count - 1);
        LinkNavigation();
        EventSystem system = EventSystem.current;
        if (system != null && system.currentSelectedGameObject != null)
        {
            Selectable focused = system.currentSelectedGameObject.GetComponent<Selectable>();
            if (focused != null && !focused.interactable)
            {
                Select(closeButton);
            }
        }
        if (fade && isActiveAndEnabled)
        {
            if (turning != null)
            {
                StopCoroutine(turning);
            }
            turning = StartCoroutine(spread.TurnFade());
        }
    }

    private static void SetInteractable(Selectable button, bool on)
    {
        if (button != null)
        {
            button.interactable = on;
        }
    }

    // Left and Right belong to page turning, so the buttons chain up and down only. The chain skips disabled
    // buttons (Selectable.Navigate does not check interactable), so focus never lands on one.
    private void LinkNavigation()
    {
        Selectable[] order = { spread.PrevButton, spread.NextButton, closeButton };
        List<Selectable> live = new List<Selectable>();
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] != null && order[i].interactable)
            {
                live.Add(order[i]);
            }
        }
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == null)
            {
                continue;
            }
            int at = live.IndexOf(order[i]);
            Selectable up = null;
            Selectable down = null;
            if (at >= 0)
            {
                up = at > 0 ? live[at - 1] : null;
                down = at < live.Count - 1 ? live[at + 1] : null;
            }
            Link(order[i], up, down);
        }
    }

    private static void Link(Selectable button, Selectable up, Selectable down)
    {
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        nav.selectOnLeft = null;
        nav.selectOnRight = null;
        button.navigation = nav;
    }

    private static void Select(Selectable target)
    {
        EventSystem system = EventSystem.current;
        if (target == null || system == null || !target.gameObject.activeInHierarchy)
        {
            return;
        }
        system.SetSelectedGameObject(null);
        system.SetSelectedGameObject(target.gameObject);
    }
}
}
