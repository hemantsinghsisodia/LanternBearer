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

    Button logBackButton;
    GameObject logPanel;
    Text logBody;
    ScrollRect logScroll;
    MainMenuScreen screen;

    public int LevelCount
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

    // Beacons on the island, from its LevelConfig. A level with no config shows no roofs.
    int BeaconCount(int index)
    {
        if (levels != null && levels.Length > 0 && levels[index] != null)
        {
            return Mathf.Max(0, levels[index].beaconCount);
        }

        return 0;
    }

    // Everything one island row shows.
    public IslandInfo Info(int index)
    {
        IslandInfo info = new IslandInfo();
        info.name = DisplayName(index);
        info.beacons = BeaconCount(index);
        info.found = GameSettings.GetLogCount(LevelId(index));
        info.bestTime = GameSettings.GetBestTime(LevelId(index));
        info.unlocked = GameSettings.IsLevelUnlocked(LevelId(index));
        return info;
    }

    void OnEnable()
    {
        Cache();
        Listen(logBackButton, CloseLog);
        RefreshLog();
    }

    void Cache()
    {
        logBackButton = FindButton("LogBackButton");
        Transform log = transform.Find("LogPanel");
        logPanel = log != null ? log.gameObject : null;
        logBody = FindText("LogBody");
        logScroll = logPanel != null ? logPanel.GetComponentInChildren<ScrollRect>(true) : null;
        screen = GetComponentInChildren<MainMenuScreen>(true);
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

    public void PlayLevel(int index)
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
        if (logPanel != null && logPanel.activeSelf)
        {
            HandleLogKeys();
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

    // Shows the existing log presentation until the Keeper's Log book arrives.
    public void OpenLog()
    {
        if (logPanel == null)
        {
            return;
        }

        RefreshLog();
        if (screen != null)
        {
            screen.SetPanelVisible(false);
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

        if (screen != null)
        {
            screen.SetPanelVisible(true);
            screen.FocusLog();
        }
    }

    public void QuitGame()
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
