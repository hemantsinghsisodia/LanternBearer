using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LanternKeeper
{
// The main menu: actions on the left (Play, Keeper's Log, Settings, Quit), the island list on the right.
// MainMenu (on the canvas) owns the level data and loading; this screen presents it and drives focus.
// Right from an action goes to the selected island; Left from an island goes back to Play; submitting an island
// selects it and moves focus to Play. Escape does nothing here (no quit-on-Esc).
public class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private ThemedButton playButton;
    [SerializeField] private ThemedButton logButton;
    [SerializeField] private ThemedButton settingsButton;
    [SerializeField] private ThemedButton quitButton;
    [SerializeField] private IslandList islandList;
    [SerializeField] private SwitchRow difficultyRow;
    [SerializeField] private Button firstSelected;

    private MainMenu menu;
    private SettingsScreen settings;
    private int selected;
    private bool initialised;

    public ThemedButton PlayButton { get { return playButton; } }
    public IslandList Islands { get { return islandList; } }
    public int SelectedIndex { get { return selected; } }

    public void Configure(GameObject panelRoot, ThemedButton play, ThemedButton log, ThemedButton settingsBtn, ThemedButton quit,
        IslandList list, SwitchRow difficulty)
    {
        panel = panelRoot;
        playButton = play;
        logButton = log;
        settingsButton = settingsBtn;
        quitButton = quit;
        islandList = list;
        difficultyRow = difficulty;
        firstSelected = play;
    }

    private void OnEnable()
    {
        menu = GetComponentInParent<MainMenu>();
        settings = menu != null ? menu.GetComponentInChildren<SettingsScreen>(true) : null;
        playButton.onClick.AddListener(Play);
        logButton.onClick.AddListener(OpenLog);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(Quit);
        islandList.Chosen += OnIslandChosen;
        difficultyRow.IndexChanged += OnDifficultyChanged;
        Refresh();
        FocusPlay();
    }

    private void OnDisable()
    {
        playButton.onClick.RemoveListener(Play);
        logButton.onClick.RemoveListener(OpenLog);
        settingsButton.onClick.RemoveListener(OpenSettings);
        quitButton.onClick.RemoveListener(Quit);
        islandList.Chosen -= OnIslandChosen;
        difficultyRow.IndexChanged -= OnDifficultyChanged;
    }

    // Reads the level data from MainMenu and redraws. The first call picks the latest unlocked island.
    public void Refresh()
    {
        difficultyRow.SetIndexWithoutNotify((int)GameSettings.Current);
        if (menu == null)
        {
            return;
        }
        int count = menu.LevelCount;
        IslandInfo[] infos = new IslandInfo[count];
        bool[] unlocked = new bool[count];
        for (int i = 0; i < count; i++)
        {
            infos[i] = menu.Info(i);
            unlocked[i] = infos[i].unlocked;
        }
        if (!initialised || selected < 0 || selected >= count || !unlocked[selected])
        {
            selected = IslandList.LatestUnlocked(unlocked);
            initialised = true;
        }
        islandList.Show(infos, selected);
        LinkNavigation();
    }

    public void Select(int islandIndex)
    {
        if (menu == null || islandIndex < 0 || islandIndex >= menu.LevelCount || !menu.Info(islandIndex).unlocked)
        {
            return;
        }
        selected = islandIndex;
        islandList.SetSelected(selected);
        LinkNavigation();
    }

    public void Play()
    {
        if (menu != null)
        {
            menu.PlayLevel(selected);
        }
    }

    public void OpenLog()
    {
        if (menu != null)
        {
            menu.OpenLog();
        }
    }

    public void OpenSettings()
    {
        if (settings == null)
        {
            return;
        }
        panel.SetActive(false);
        settings.Open(settingsButton, OnSettingsClosed);
    }

    public void Quit()
    {
        if (menu != null)
        {
            menu.QuitGame();
        }
    }

    // Used by MainMenu around the Keeper's Log view.
    public void SetPanelVisible(bool visible)
    {
        panel.SetActive(visible);
    }

    public void FocusLog()
    {
        SelectTarget(logButton);
    }

    public void FocusPlay()
    {
        SelectTarget(playButton);
    }

    private void OnSettingsClosed()
    {
        panel.SetActive(true);
        Refresh();
        SelectTarget(settingsButton);
    }

    private void OnIslandChosen(int index)
    {
        Select(index);
        FocusPlay();
    }

    private void OnDifficultyChanged(int index)
    {
        GameSettings.Current = (Difficulty)Mathf.Clamp(index, 0, 2);
    }

    private void LinkNavigation()
    {
        Button[] actions = { playButton, logButton, settingsButton, quitButton };
        Selectable target = islandList.RowSelectable(selected);
        for (int i = 0; i < actions.Length; i++)
        {
            Navigation nav = actions[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? actions[i - 1] : null;
            nav.selectOnDown = i < actions.Length - 1 ? actions[i + 1] : null;
            nav.selectOnLeft = null;
            nav.selectOnRight = target;
            actions[i].navigation = nav;
        }
        Navigation diff = difficultyRow.navigation;
        diff.mode = Navigation.Mode.Explicit;
        diff.selectOnUp = null;
        diff.selectOnDown = target;
        diff.selectOnLeft = null;
        diff.selectOnRight = null;
        difficultyRow.navigation = diff;
        islandList.LinkRows(difficultyRow, playButton);
    }

    private static void SelectTarget(Selectable target)
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
