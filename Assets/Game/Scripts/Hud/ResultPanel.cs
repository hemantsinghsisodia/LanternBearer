using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LanternKeeper
{
// The end-of-round panel on a dim: "The shore is lit" with the run time, best and lit roofs (Next island, Retry, Main Menu),
// or "The flame went out" (Retry, Main Menu). It only presents; the coordinator subscribes to Next, Retry and Menu and calls
// GameManager. The first visible button takes focus on show, and navigation covers the visible buttons only.
public class ResultPanel : MonoBehaviour
{
    public const string WinTitle = "The shore is lit";
    public const string LoseTitle = "The flame went out";
    public const string LoseText = "Your lantern went dark.";
    public const string NewBestText = "New best!";

    [SerializeField] private UITheme theme;
    [SerializeField] private GameObject panel;
    [SerializeField] private ThemedLabel title;
    [SerializeField] private ThemedLabel subtitle;
    [SerializeField] private ThemedLabel loseLine;
    [SerializeField] private ThemedLabel timeLabel;
    [SerializeField] private ThemedLabel bestLabel;
    [SerializeField] private ThemedLabel newBestLabel;
    [SerializeField] private BeaconRoofs roofs;
    [SerializeField] private ThemedButton nextButton;
    [SerializeField] private ThemedButton retryButton;
    [SerializeField] private ThemedButton menuButton;

    private Button[] tabOrder = new Button[0];

    public event Action Next;
    public event Action Retry;
    public event Action Menu;

    public bool IsShowing { get { return panel != null && panel.activeSelf; } }
    public bool IsWin { get; private set; }
    public ThemedLabel Title { get { return title; } }
    public ThemedLabel LoseLine { get { return loseLine; } }
    public ThemedLabel TimeLabel { get { return timeLabel; } }
    public ThemedLabel BestLabel { get { return bestLabel; } }
    public ThemedLabel NewBestLabel { get { return newBestLabel; } }
    public BeaconRoofs Roofs { get { return roofs; } }
    public ThemedButton NextButton { get { return nextButton; } }
    public ThemedButton RetryButton { get { return retryButton; } }
    public ThemedButton MenuButton { get { return menuButton; } }
    public Button[] TabOrder { get { return tabOrder; } }

    public void Configure(UITheme newTheme, GameObject panelRoot, ThemedLabel titleLabel, ThemedLabel subtitleLabel, ThemedLabel loseFlavour,
        ThemedLabel time, ThemedLabel best, ThemedLabel newBest, BeaconRoofs roofRow, ThemedButton next, ThemedButton retry, ThemedButton menu)
    {
        theme = newTheme;
        panel = panelRoot;
        title = titleLabel;
        subtitle = subtitleLabel;
        loseLine = loseFlavour;
        timeLabel = time;
        bestLabel = best;
        newBestLabel = newBest;
        roofs = roofRow;
        nextButton = next;
        retryButton = retry;
        menuButton = menu;
    }

    private void OnEnable()
    {
        nextButton.onClick.AddListener(OnNext);
        retryButton.onClick.AddListener(OnRetry);
        menuButton.onClick.AddListener(OnMenu);
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        nextButton.onClick.RemoveListener(OnNext);
        retryButton.onClick.RemoveListener(OnRetry);
        menuButton.onClick.RemoveListener(OnMenu);
    }

    private void OnNext()
    {
        if (Next != null)
        {
            Next();
        }
    }

    private void OnRetry()
    {
        if (Retry != null)
        {
            Retry();
        }
    }

    private void OnMenu()
    {
        if (Menu != null)
        {
            Menu();
        }
    }

    // The island name under the title ("Island 1 · The Last Light"); empty hides it.
    public void SetSubtitle(string text)
    {
        bool has = !string.IsNullOrEmpty(text);
        subtitle.gameObject.SetActive(has);
        if (has && subtitle.Text.text != text)
        {
            subtitle.Text.text = text;
        }
    }

    public void ShowWin(float time, float best, bool newBest, int lit, int total, bool hasNext)
    {
        IsWin = true;
        title.Text.text = WinTitle;
        loseLine.gameObject.SetActive(false);
        timeLabel.gameObject.SetActive(true);
        bestLabel.gameObject.SetActive(true);
        timeLabel.Text.text = "Time  " + HudMath.FormatTime(time);
        bestLabel.Text.text = "Best  " + HudMath.FormatTime(best);
        newBestLabel.gameObject.SetActive(newBest);
        roofs.gameObject.SetActive(true);
        roofs.Show(total, lit);
        nextButton.gameObject.SetActive(hasNext);
        Open(hasNext ? nextButton : retryButton);
    }

    public void ShowLose()
    {
        IsWin = false;
        title.Text.text = LoseTitle;
        loseLine.gameObject.SetActive(true);
        loseLine.Text.text = LoseText;
        timeLabel.gameObject.SetActive(false);
        bestLabel.gameObject.SetActive(false);
        newBestLabel.gameObject.SetActive(false);
        roofs.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        Open(retryButton);
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void Open(ThemedButton first)
    {
        panel.SetActive(true);
        LinkNavigation();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
        Focus(first);
    }

    private void LinkNavigation()
    {
        List<Button> visible = new List<Button>();
        ThemedButton[] all = { nextButton, retryButton, menuButton };
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.activeSelf)
            {
                visible.Add(all[i]);
            }
        }
        tabOrder = visible.ToArray();
        for (int i = 0; i < tabOrder.Length; i++)
        {
            Navigation nav = tabOrder[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? tabOrder[i - 1] : null;
            nav.selectOnDown = i < tabOrder.Length - 1 ? tabOrder[i + 1] : null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            tabOrder[i].navigation = nav;
        }
    }

    private static void Focus(Selectable target)
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
