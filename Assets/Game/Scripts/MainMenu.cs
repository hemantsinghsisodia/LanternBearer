using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
public class MainMenu : MonoBehaviour
{
    static readonly string[] FallbackIds = { "island1", "island2", "island3", "island4" };
    static readonly string[] FallbackScenes = { "Island1", "Island2", "Island3", "Island4" };
    static readonly string[] FallbackNames = { "Island 1", "Island 2", "Island 3", "Island 4" };

    [SerializeField] LevelConfig[] levels = new LevelConfig[0];

    Button[] menuTab = new Button[0];
    Button[] levelButtons = new Button[0];
    Text[] bestLabels = new Text[0];
    Button playButton;
    Button difficultyButton;
    Button musicButton;
    Button graphicsButton;
    Button logButton;
    Button logBackButton;
    Button quitButton;
    GameObject column;
    GameObject logPanel;
    Text logBody;
    ScrollRect logScroll;
    GraphicsMenu graphicsMenu;
    SettingsScreen settingsScreen;

    int LevelCount
    {
        get { return levels != null && levels.Length > 0 ? levels.Length : FallbackIds.Length; }
    }

    string LevelId(int index)
    {
        if (levels != null && levels.Length > 0)
        {
            return levels[index] != null ? levels[index].levelId : "";
        }

        return FallbackIds[index];
    }

    string SceneName(int index)
    {
        if (levels != null && levels.Length > 0)
        {
            return levels[index] != null ? levels[index].sceneName : "";
        }

        return FallbackScenes[index];
    }

    string DisplayName(int index)
    {
        if (levels != null && levels.Length > 0)
        {
            return levels[index] != null ? levels[index].displayName : "";
        }

        return FallbackNames[index];
    }

    void OnEnable()
    {
        Cache();
        Listen(playButton, Play);
        Listen(difficultyButton, Cycle);
        Listen(musicButton, ToggleMusic);
        Listen(graphicsButton, OpenGraphics);
        Listen(logButton, OpenLog);
        Listen(logBackButton, CloseLog);
        Listen(quitButton, QuitGame);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int index = i;
            if (levelButtons[i] != null)
            {
                levelButtons[i].onClick.RemoveAllListeners();
                levelButtons[i].onClick.AddListener(() => PlayLevel(index));
            }
        }

        Refresh();
        GraphicsMenu.Select(playButton);
    }

    void Cache()
    {
        playButton = FindButton("PlayButton");
        difficultyButton = FindButton("DifficultyButton");
        musicButton = FindButton("MusicButton");
        graphicsButton = FindButton("GraphicsButton");
        logButton = FindButton("LogButton");
        logBackButton = FindButton("LogBackButton");
        quitButton = FindButton("QuitButton");
        int count = LevelCount;
        levelButtons = new Button[count];
        bestLabels = new Text[count];
        for (int i = 0; i < count; i++)
        {
            string scene = SceneName(i);
            levelButtons[i] = FindButton(scene + "Button");
            bestLabels[i] = FindText("Best" + scene);
        }

        Transform panel = transform.Find("Panel");
        column = panel != null ? panel.gameObject : null;
        Transform log = transform.Find("LogPanel");
        logPanel = log != null ? log.gameObject : null;
        logBody = FindText("LogBody");
        logScroll = logPanel != null ? logPanel.GetComponentInChildren<ScrollRect>(true) : null;
        graphicsMenu = GetComponent<GraphicsMenu>();
        Transform settingsHost = transform.Find("SettingsScreen");
        settingsScreen = settingsHost != null ? settingsHost.GetComponent<SettingsScreen>() : null;
        List<Button> order = new List<Button>();
        order.Add(playButton);
        order.Add(difficultyButton);
        order.Add(musicButton);
        order.Add(graphicsButton);
        order.Add(logButton);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            order.Add(levelButtons[i]);
        }

        order.Add(quitButton);
        menuTab = order.ToArray();
        LinkColumn();
    }

    void Refresh()
    {
        if (difficultyButton != null)
        {
            SetLabel(difficultyButton, "Difficulty: " + GameSettings.Current);
        }

        RefreshMusicLabel();

        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (bestLabels[i] != null)
            {
                bestLabels[i].text = "Best " + GameManager.FormatTime(GameSettings.GetBestTime(LevelId(i)));
            }

            if (levelButtons[i] != null)
            {
                bool unlocked = GameSettings.IsLevelUnlocked(LevelId(i));
                levelButtons[i].interactable = unlocked;
                SetLabel(levelButtons[i], unlocked ? DisplayName(i) : DisplayName(i) + " (Locked)");
            }
        }

        RefreshLog();
        LinkColumn();
    }

    void RefreshLog()
    {
        if (logBody == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        int found = 0;
        if (levels != null)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null || levels[i].logEntries == null || levels[i].logEntries.Length == 0)
                {
                    continue;
                }

                string[] entries = levels[i].logEntries;
                int read = Mathf.Min(GameSettings.GetLogCount(levels[i].levelId), entries.Length);
                builder.Append(levels[i].displayName).Append("  (").Append(read).Append("/").Append(entries.Length).Append(")\n");
                for (int e = 0; e < read; e++)
                {
                    builder.Append(entries[e]).Append("\n\n");
                    found++;
                }

                if (read == 0)
                {
                    builder.Append("No pages found yet.\n\n");
                }
            }
        }

        if (found == 0 && builder.Length == 0)
        {
            builder.Append("Light the beacons to find the Keeper's pages.");
        }

        logBody.text = builder.ToString();
    }

    void LinkColumn()
    {
        Button previous = null;
        for (int i = 0; i < menuTab.Length; i++)
        {
            Button button = menuTab[i];
            if (button == null || !button.interactable)
            {
                continue;
            }

            ColorBlock colors = button.colors;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = previous;
            navigation.selectOnDown = null;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            button.navigation = navigation;
            if (previous != null)
            {
                Navigation up = previous.navigation;
                up.selectOnDown = button;
                previous.navigation = up;
            }

            previous = button;
        }

        if (previous != null && playButton != null && playButton.interactable && previous != playButton)
        {
            Navigation bottom = previous.navigation;
            bottom.selectOnDown = playButton;
            previous.navigation = bottom;
            Navigation top = playButton.navigation;
            top.selectOnUp = previous;
            playButton.navigation = top;
        }
    }

    void Play()
    {
        PlayLevel(0);
    }

    void PlayLevel(int index)
    {
        if (index < 0 || index >= LevelCount)
        {
            return;
        }

        if (!GameSettings.IsLevelUnlocked(LevelId(index)))
        {
            return;
        }

        string scene = SceneName(index);
        if (string.IsNullOrEmpty(scene))
        {
            return;
        }

        SceneManager.LoadScene(scene);
    }

    void Update()
    {
        RefreshMusicLabel();
        if (logPanel != null && logPanel.activeSelf)
        {
            HandleLogKeys();
            return;
        }

        bool settingsOpen = settingsScreen != null && settingsScreen.gameObject.activeSelf;
        if (column != null && column.activeSelf && !settingsOpen && (graphicsMenu == null || !graphicsMenu.IsOpen))
        {
            GraphicsMenu.HandleTab(menuTab);
        }
    }

    void HandleLogKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseLog();
            return;
        }

        if (logScroll == null)
        {
            return;
        }

        float step = 0f;
        if (keyboard.downArrowKey.isPressed)
        {
            step = -1f;
        }
        else if (keyboard.upArrowKey.isPressed)
        {
            step = 1f;
        }

        if (step != 0f)
        {
            logScroll.verticalNormalizedPosition = Mathf.Clamp01(logScroll.verticalNormalizedPosition + step * Time.unscaledDeltaTime * 0.6f);
        }
    }

    void OpenLog()
    {
        if (logPanel == null)
        {
            return;
        }

        RefreshLog();
        if (column != null)
        {
            column.SetActive(false);
        }

        logPanel.SetActive(true);
        if (logScroll != null)
        {
            logScroll.verticalNormalizedPosition = 1f;
        }

        GraphicsMenu.Select(logBackButton);
    }

    void CloseLog()
    {
        if (logPanel != null)
        {
            logPanel.SetActive(false);
        }

        if (column != null)
        {
            column.SetActive(true);
        }

        GraphicsMenu.Select(logButton);
    }

    void OpenGraphics()
    {
        // Temporary until the menu is rebuilt: the Graphics button opens the new Settings screen when the scene has one.
        SettingsScreen settings = settingsScreen;
        if (settings != null)
        {
            if (column != null)
            {
                column.SetActive(false);
            }

            settings.Open(graphicsButton, CloseGraphics);
            return;
        }

        if (graphicsMenu == null)
        {
            graphicsMenu = GetComponent<GraphicsMenu>();
        }

        if (graphicsMenu == null)
        {
            return;
        }

        if (column != null)
        {
            column.SetActive(false);
        }

        graphicsMenu.Open(CloseGraphics);
    }

    void CloseGraphics()
    {
        if (column != null)
        {
            column.SetActive(true);
        }

        GraphicsMenu.Select(graphicsButton);
    }

    void ToggleMusic()
    {
        MusicPlayer.ToggleMute();
        RefreshMusicLabel();
    }

    void RefreshMusicLabel()
    {
        if (musicButton != null)
        {
            SetLabel(musicButton, MusicPlayer.IsMuted ? "Music: Off" : "Music: On");
        }
    }

    void Cycle()
    {
        GameSettings.CycleDifficulty();
        Refresh();
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

    static void SetLabel(Button button, string value)
    {
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = value;
        }
    }

    Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    Text FindText(string objectName)
    {
        Text[] labels = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].name == objectName)
            {
                return labels[i];
            }
        }

        return null;
    }
}
}
