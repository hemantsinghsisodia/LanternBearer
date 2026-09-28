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

    Button playButton;
    Button difficultyButton;
    Button island1Button;
    Button island2Button;
    Button quitButton;
    Text best1;
    Text best2;

    void OnEnable()
    {
        Cache();
        Listen(playButton, Play);
        Listen(difficultyButton, Cycle);
        Listen(island1Button, Play);
        Listen(island2Button, PlayIsland2);
        Listen(quitButton, QuitGame);
        Refresh();
    }

    void Cache()
    {
        playButton = FindButton("PlayButton");
        difficultyButton = FindButton("DifficultyButton");
        island1Button = FindButton("Island1Button");
        island2Button = FindButton("Island2Button");
        quitButton = FindButton("QuitButton");
        best1 = FindText("BestIsland1");
        best2 = FindText("BestIsland2");
    }

    void Refresh()
    {
        if (difficultyButton != null)
        {
            SetLabel(difficultyButton, "Difficulty: " + GameSettings.Current);
        }

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
