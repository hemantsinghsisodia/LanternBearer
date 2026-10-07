using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
// The pause screen (ink and brass panel on a dim): Resume, Restart, How to Play, Settings, Main Menu.
// It sits on the island HUD canvas. The root stays active so Update can watch GameManager; only the "panel" child
// toggles. How to Play shows HUD's intro card (HUD reads HowToOpen); Settings is the shared SettingsScreen.
// ConsumeBack (Esc / P through GameManager via HUD) closes How to Play or Settings first, then resumes.
public class PauseScreen : MonoBehaviour
{
    const float HowToUnlockDelay = 0.2f;

    [SerializeField] private GameObject panel;
    [SerializeField] private ThemedLabel title;
    [SerializeField] private ThemedLabel subtitle;
    [SerializeField] private ThemedButton resumeButton;
    [SerializeField] private ThemedButton restartButton;
    [SerializeField] private ThemedButton howToButton;
    [SerializeField] private ThemedButton settingsButton;
    [SerializeField] private ThemedButton menuButton;
    [SerializeField] private Button firstSelected;
    [SerializeField] private SettingsScreen settings;

    private Button[] tabOrder;
    private bool howToOpen;
    private float howToUnlock;
    private int settingsClosedFrame = -1;

    public bool IsShowing { get { return panel != null && panel.activeSelf; } }
    public bool HowToOpen { get { return howToOpen; } }
    public float HowToUnlockTime { get { return howToUnlock; } }
    public ThemedButton ResumeButton { get { return resumeButton; } }
    public ThemedButton RestartButton { get { return restartButton; } }
    public ThemedButton HowToButton { get { return howToButton; } }
    public ThemedButton SettingsButton { get { return settingsButton; } }
    public ThemedButton MenuButton { get { return menuButton; } }
    public Button FirstSelected { get { return firstSelected; } }
    public ThemedLabel Subtitle { get { return subtitle; } }

    public void Configure(GameObject panelRoot, ThemedLabel titleLabel, ThemedLabel subtitleLabel, ThemedButton resume,
        ThemedButton restart, ThemedButton howTo, ThemedButton settingsBtn, ThemedButton menu)
    {
        panel = panelRoot;
        title = titleLabel;
        subtitle = subtitleLabel;
        resumeButton = resume;
        restartButton = restart;
        howToButton = howTo;
        settingsButton = settingsBtn;
        menuButton = menu;
        firstSelected = resume;
    }

    public void SetSettings(SettingsScreen screen)
    {
        settings = screen;
    }

    private void OnEnable()
    {
        if (settings == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            settings = canvas != null ? canvas.GetComponentInChildren<SettingsScreen>(true) : null;
        }
        if (resumeButton == null || restartButton == null || howToButton == null || settingsButton == null || menuButton == null)
        {
            Debug.LogError("PauseScreen: a button is not wired; run Lantern Keeper > Install Pause.", this);
            return;
        }
        resumeButton.onClick.AddListener(OnResume);
        restartButton.onClick.AddListener(OnRestart);
        howToButton.onClick.AddListener(OnHowTo);
        settingsButton.onClick.AddListener(OnSettings);
        menuButton.onClick.AddListener(OnMenu);
        LinkNavigation();
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (resumeButton == null || restartButton == null || howToButton == null || settingsButton == null || menuButton == null)
        {
            return;
        }
        resumeButton.onClick.RemoveListener(OnResume);
        restartButton.onClick.RemoveListener(OnRestart);
        howToButton.onClick.RemoveListener(OnHowTo);
        settingsButton.onClick.RemoveListener(OnSettings);
        menuButton.onClick.RemoveListener(OnMenu);
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || panel == null || tabOrder == null)
        {
            return;
        }
        if (howToOpen && !manager.IsPaused)
        {
            howToOpen = false;
        }
        bool settingsOpen = settings != null && settings.IsOpen;
        bool show = manager.IsPaused && !settingsOpen && !manager.IntroShowing && !howToOpen;
        if (panel.activeSelf != show)
        {
            panel.SetActive(show);
            if (show)
            {
                transform.SetAsLastSibling();
                Focus(firstSelected);
            }
        }
        if (show)
        {
            RefreshSubtitle(manager);
            GraphicsMenu.HandleTab(tabOrder);
        }
    }

    private void RefreshSubtitle(GameManager manager)
    {
        if (subtitle == null)
        {
            return;
        }
        bool has = !string.IsNullOrEmpty(manager.IslandLabel);
        subtitle.gameObject.SetActive(has);
        string line = has ? manager.IslandLabelHud : "";
        if (subtitle.Text.text != line)
        {
            subtitle.Text.text = line;
        }
    }

    public void Show()
    {
        GameManager manager = GameManager.Instance;
        if (panel == null || manager == null || !manager.IsPaused)
        {
            return;
        }
        panel.SetActive(true);
        transform.SetAsLastSibling();
        Focus(firstSelected);
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    // Esc / P while paused (GameManager asks HUD, which asks us). Always true: this screen handles the press itself,
    // either by closing a child screen or by resuming through GameManager.
    public bool ConsumeBack()
    {
        if (howToOpen)
        {
            CloseHowTo();
            return true;
        }
        if (settings != null && (settings.IsOpen || settingsClosedFrame == Time.frameCount))
        {
            // Settings watches Esc and gamepad B itself; only step it for keys it does not watch (P).
            Keyboard keyboard = Keyboard.current;
            if (settings.IsOpen && keyboard != null && !keyboard.escapeKey.wasPressedThisFrame)
            {
                settings.ConsumeBack();
            }
            return true;
        }
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.IsPaused)
        {
            manager.Resume();
        }
        return true;
    }

    public void CloseHowTo()
    {
        howToOpen = false;
        Show();
    }

    private void OnResume()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Resume();
        }
    }

    private void OnRestart()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
            return;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GoToMenu();
            return;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void OnHowTo()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.IsPaused || manager.IntroShowing)
        {
            return;
        }
        howToOpen = true;
        howToUnlock = Time.unscaledTime + HowToUnlockDelay;
        Hide();
    }

    private void OnSettings()
    {
        if (settings == null)
        {
            return;
        }
        Hide();
        settings.Open(settingsButton, OnSettingsClosed);
    }

    private void OnSettingsClosed()
    {
        settingsClosedFrame = Time.frameCount;
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.IsPaused)
        {
            return;
        }
        panel.SetActive(true);
        transform.SetAsLastSibling();
        Focus(settingsButton);
    }

    private void LinkNavigation()
    {
        tabOrder = new Button[] { resumeButton, restartButton, howToButton, settingsButton, menuButton };
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
