using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LanternKeeper
{
public class MainMenu : MonoBehaviour
{
    const string Island1Id = "island1";
    const string Island2Id = "island2";
    const string Island1Scene = "Island1";
    const string Island2Scene = "Island2";

    readonly Button[] menuTab = new Button[7];
    Button playButton;
    Button difficultyButton;
    Button musicButton;
    Button graphicsButton;
    Button island1Button;
    Button island2Button;
    Button quitButton;
    Text best1;
    Text best2;
    GameObject column;
    GraphicsMenu graphicsMenu;

    void OnEnable()
    {
        Cache();
        Listen(playButton, Play);
        Listen(difficultyButton, Cycle);
        Listen(musicButton, ToggleMusic);
        Listen(graphicsButton, OpenGraphics);
        Listen(island1Button, Play);
        Listen(island2Button, PlayIsland2);
        Listen(quitButton, QuitGame);
        Refresh();
        GraphicsMenu.Select(playButton);
    }

    void Cache()
    {
        playButton = FindButton("PlayButton");
        difficultyButton = FindButton("DifficultyButton");
        musicButton = FindButton("MusicButton");
        graphicsButton = FindButton("GraphicsButton");
        island1Button = FindButton("Island1Button");
        island2Button = FindButton("Island2Button");
        quitButton = FindButton("QuitButton");
        best1 = FindText("BestIsland1");
        best2 = FindText("BestIsland2");
        Transform panel = transform.Find("Panel");
        column = panel != null ? panel.gameObject : null;
        graphicsMenu = GetComponent<GraphicsMenu>();
        menuTab[0] = playButton;
        menuTab[1] = difficultyButton;
        menuTab[2] = musicButton;
        menuTab[3] = graphicsButton;
        menuTab[4] = island1Button;
        menuTab[5] = island2Button;
        menuTab[6] = quitButton;
        LinkColumn();
    }

    void Refresh()
    {
        if (difficultyButton != null)
        {
            SetLabel(difficultyButton, "Difficulty: " + GameSettings.Current);
        }

        RefreshMusicLabel();

        if (best1 != null)
        {
            best1.text = "Best " + GameManager.FormatTime(GameSettings.GetBestTime(Island1Id));
        }

        if (best2 != null)
        {
            best2.text = "Best " + GameManager.FormatTime(GameSettings.GetBestTime(Island2Id));
        }

        bool unlocked = GameSettings.IsLevelUnlocked(Island2Id);
        if (island2Button != null)
        {
            island2Button.interactable = unlocked;
            SetLabel(island2Button, unlocked ? "Island 2" : "Island 2 (Locked)");
        }

        LinkColumn();
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
        SceneManager.LoadScene(Island1Scene);
    }

    void PlayIsland2()
    {
        if (!GameSettings.IsLevelUnlocked(Island2Id))
        {
            return;
        }

        SceneManager.LoadScene(Island2Scene);
    }

    void Update()
    {
        RefreshMusicLabel();
        if (column != null && column.activeSelf && (graphicsMenu == null || !graphicsMenu.IsOpen))
        {
            GraphicsMenu.HandleTab(menuTab);
        }
    }

    void OpenGraphics()
    {
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
