using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
// The Phase E screens driven the way a keyboard or gamepad drives them: move and submit events through the EventSystem,
// and the screens' own Back (ConsumeBack) entry points that Esc / B reach. The game types are reached by reflection.
public class MenuNavigationTest
{
    const string GraphicsKey = "LanternKeeperGraphics";
    const string DifficultyKey = "LanternKeeperDifficulty";
    const string Won1Key = "LanternKeeperWon_island1";
    const string Won2Key = "LanternKeeperWon_island2";
    const string Won3Key = "LanternKeeperWon_island3";
    const string Intro1Key = "LanternKeeperIntro_island1";

    int savedGraphics;
    bool hadDifficulty, hadWon1, hadWon2, hadWon3, hadIntro1;
    int savedDifficulty, savedWon1, savedWon2, savedWon3, savedIntro1;

    [SetUp]
    public void SetUp()
    {
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        hadDifficulty = PlayerPrefs.HasKey(DifficultyKey);
        hadWon1 = PlayerPrefs.HasKey(Won1Key);
        hadWon2 = PlayerPrefs.HasKey(Won2Key);
        hadWon3 = PlayerPrefs.HasKey(Won3Key);
        hadIntro1 = PlayerPrefs.HasKey(Intro1Key);
        savedWon2 = PlayerPrefs.GetInt(Won2Key, 0);
        savedWon3 = PlayerPrefs.GetInt(Won3Key, 0);
        savedDifficulty = PlayerPrefs.GetInt(DifficultyKey, 1);
        savedWon1 = PlayerPrefs.GetInt(Won1Key, 0);
        savedIntro1 = PlayerPrefs.GetInt(Intro1Key, 0);
        // Exactly Islands 1 and 2 are open (the second row is the latest), and Island 1's intro card does not pause the game.
        PlayerPrefs.SetInt(Won1Key, 1);
        PlayerPrefs.DeleteKey(Won2Key);
        PlayerPrefs.DeleteKey(Won3Key);
        PlayerPrefs.SetInt(Intro1Key, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        Restore(DifficultyKey, hadDifficulty, savedDifficulty);
        Restore(Won1Key, hadWon1, savedWon1);
        Restore(Won2Key, hadWon2, savedWon2);
        Restore(Won3Key, hadWon3, savedWon3);
        Restore(Intro1Key, hadIntro1, savedIntro1);
        PlayerPrefs.Save();
    }

    static void Restore(string key, bool had, int value)
    {
        if (had)
        {
            PlayerPrefs.SetInt(key, value);
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    // ---------- helpers ----------

    static Type Game(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static UnityEngine.Object Find(string typeName)
    {
        UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(Game(typeName), FindObjectsInactive.Include);
        Assert.IsNotNull(found, typeName + " not found in " + SceneManager.GetActiveScene().name);
        return found;
    }

    static object Prop(object target, string name)
    {
        PropertyInfo info = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, target.GetType().Name + "." + name);
        return info.GetValue(target);
    }

    static object Call(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, target.GetType().Name + "." + name + "()");
        return method.Invoke(target, args);
    }

    static IEnumerator Load(string scene)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone)
        {
            yield return null;
        }
        yield return null;
        yield return null;
    }

    static Button ButtonNamed(Component root, string name)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name == name)
            {
                return button;
            }
        }
        Assert.Fail("No button named " + name + " under " + root.name);
        return null;
    }

    static Button RowButton(Array rows, int index)
    {
        return (Button)Prop(rows.GetValue(index), "Button");
    }

    static GameObject Selected()
    {
        Assert.IsNotNull(EventSystem.current, "an EventSystem is in the scene");
        return EventSystem.current.currentSelectedGameObject;
    }

    static void AssertFocus(Component expected, string message)
    {
        GameObject selected = Selected();
        Assert.AreSame(expected.gameObject, selected, message + " (focus is " + (selected != null ? selected.name : "nothing") + ")");
    }

    static void Move(MoveDirection direction)
    {
        EventSystem system = EventSystem.current;
        GameObject current = system.currentSelectedGameObject;
        Assert.IsNotNull(current, "something has focus before moving " + direction);
        AxisEventData data = new AxisEventData(system);
        data.moveDir = direction;
        data.moveVector = direction == MoveDirection.Right ? Vector2.right : direction == MoveDirection.Left ? Vector2.left
            : direction == MoveDirection.Up ? Vector2.up : Vector2.down;
        ExecuteEvents.Execute(current, data, ExecuteEvents.moveHandler);
    }

    static void Submit()
    {
        EventSystem system = EventSystem.current;
        ExecuteEvents.Execute(system.currentSelectedGameObject, new BaseEventData(system), ExecuteEvents.submitHandler);
    }

    // ---------- settings and log from the main menu ----------

    [UnityTest]
    public IEnumerator MainMenuToEverySettingsSectionAndBack()
    {
        yield return Load("MainMenu");
        Component menu = (Component)Find("MainMenuScreen");
        Component settings = (Component)Find("SettingsScreen");
        Button settingsButton = ButtonNamed(menu, "SettingsButton");
        Assert.IsFalse((bool)Prop(settings, "IsOpen"), "settings starts closed");

        settingsButton.onClick.Invoke();
        Assert.IsTrue((bool)Prop(settings, "IsOpen"), "the Settings button opens the screen");
        SectionList list = (SectionList)Prop(settings, "SectionList");
        Assert.AreEqual(5, list.SectionButtons.Length);
        for (int i = 0; i < list.SectionButtons.Length; i++)
        {
            // Focusing a section tab shows that section (and only that one).
            EventSystem.current.SetSelectedGameObject(list.SectionButtons[i].gameObject);
            Assert.AreEqual(i, list.Current, "section " + i + " is current");
            for (int p = 0; p < list.SectionPanels.Length; p++)
            {
                Assert.AreEqual(p == i, list.SectionPanels[p].activeSelf, "panel " + p + " while section " + i + " is shown");
            }
            // Every section but Controls (a read-only list) has a control to step into.
            Selectable first = list.SectionPanels[i].GetComponentInChildren<Selectable>(false);
            Assert.AreEqual(i != 4, first != null, "section " + i + " has controls: " + (first != null));
        }

        Assert.IsTrue((bool)Call(settings, "ConsumeBack"), "Back on the tab list closes Settings");
        Assert.IsFalse((bool)Prop(settings, "IsOpen"), "settings closed");
        AssertFocus(settingsButton, "Back returns to the Settings button");
        Assert.IsTrue(((Component)Prop(menu, "PlayButton")).gameObject.activeInHierarchy, "the menu is back");
    }

    [UnityTest]
    public IEnumerator LogPagesAndBack()
    {
        yield return Load("MainMenu");
        Component menu = (Component)Find("MainMenuScreen");
        Component log = (Component)Find("KeepersLogScreen");
        Button logButton = ButtonNamed(menu, "LogButton");

        logButton.onClick.Invoke();
        Assert.IsTrue((bool)Prop(log, "IsOpen"), "the Log button opens the book");
        int spreads = (int)Prop(log, "SpreadCount");
        Assert.Greater(spreads, 1, "the book has more than one spread");
        Assert.AreEqual(0, (int)Prop(log, "SpreadIndex"), "it opens on the first spread");

        Call(log, "TurnTo", 1);
        Assert.AreEqual(1, (int)Prop(log, "SpreadIndex"), "turned forward one spread");
        Call(log, "TurnTo", spreads + 5);
        Assert.AreEqual(spreads - 1, (int)Prop(log, "SpreadIndex"), "turning past the end stops at the last spread");
        Call(log, "TurnTo", 0);
        Assert.AreEqual(0, (int)Prop(log, "SpreadIndex"), "turned back to the first spread");

        Assert.IsTrue((bool)Call(log, "ConsumeBack"), "Back closes the book");
        Assert.IsFalse((bool)Prop(log, "IsOpen"), "the book is closed");
        AssertFocus(logButton, "Back returns to the Keeper's Log button");
    }

    // ---------- pause ----------

    static IEnumerator PauseIsland1()
    {
        yield return Load("Island1");
        object manager = Game("GameManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Assert.IsNotNull(manager, "GameManager");
        Assert.IsFalse((bool)Prop(manager, "IntroShowing"), "no intro card (marked seen)");
        Call(manager, "Pause");
        Assert.IsTrue((bool)Prop(manager, "IsPaused"), "paused");
        yield return null;
        yield return null;
    }

    static object Manager()
    {
        return Game("GameManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    [UnityTest]
    public IEnumerator PauseSettingsBackResume()
    {
        yield return PauseIsland1();
        Component pause = (Component)Find("PauseScreen");
        Component settings = (Component)Find("SettingsScreen");
        Assert.IsTrue((bool)Prop(pause, "IsShowing"), "the pause panel is up");
        AssertFocus((Component)Prop(pause, "ResumeButton"), "Resume has focus first");

        ((Button)Prop(pause, "SettingsButton")).onClick.Invoke();
        Assert.IsTrue((bool)Prop(settings, "IsOpen"), "Settings opens over the pause panel");
        Assert.IsFalse((bool)Prop(pause, "IsShowing"), "the pause panel steps aside");
        Assert.IsTrue((bool)Prop(Manager(), "IsPaused"), "still paused");

        Assert.IsTrue((bool)Call(settings, "ConsumeBack"), "Back closes Settings");
        Assert.IsFalse((bool)Prop(settings, "IsOpen"), "settings closed");
        Assert.IsTrue((bool)Prop(pause, "IsShowing"), "the pause panel is back");
        Assert.IsTrue((bool)Prop(Manager(), "IsPaused"), "closing Settings does not resume");

        yield return null; // a later frame than the one that closed Settings
        Assert.IsTrue((bool)Call(pause, "ConsumeBack"), "Back on the pause panel is handled");
        Assert.IsFalse((bool)Prop(Manager(), "IsPaused"), "the next Back resumes");
        Assert.AreEqual(1f, Time.timeScale, 0.001f, "time runs again");
    }

    // Esc on a paused island, the order GameManager asks: How to Play closes first, then Settings (without also resuming
    // in the same frame), then the pause panel resumes.
    [UnityTest]
    public IEnumerator PauseBackOrderOnIsland()
    {
        yield return PauseIsland1();
        Component pause = (Component)Find("PauseScreen");
        Component settings = (Component)Find("SettingsScreen");
        object manager = Manager();

        // How to Play, then Esc closes it and the pause panel returns.
        ((Button)Prop(pause, "HowToButton")).onClick.Invoke();
        Assert.IsTrue((bool)Prop(pause, "HowToOpen"), "How to Play is open");
        Assert.IsFalse((bool)Prop(pause, "IsShowing"), "the pause panel is hidden behind the card");
        Assert.IsTrue((bool)Call(pause, "ConsumeBack"));
        Assert.IsFalse((bool)Prop(pause, "HowToOpen"), "Esc closed How to Play");
        Assert.IsTrue((bool)Prop(pause, "IsShowing"), "the pause panel is back");
        Assert.IsTrue((bool)Prop(manager, "IsPaused"), "Esc on How to Play did not resume");

        // Settings, then Esc closes it. Settings handles Esc itself in its own Update; GameManager then asks the pause
        // screen in the same frame. That second Back must not resume.
        ((Button)Prop(pause, "SettingsButton")).onClick.Invoke();
        Assert.IsTrue((bool)Prop(settings, "IsOpen"));
        Call(settings, "ConsumeBack");
        Assert.IsFalse((bool)Prop(settings, "IsOpen"), "Esc closed Settings");
        Assert.IsTrue((bool)Call(pause, "ConsumeBack"), "the pause screen takes the same-frame press");
        Assert.IsTrue((bool)Prop(manager, "IsPaused"), "closing Settings did not resume in the same frame");
        AssertFocus((Component)Prop(pause, "SettingsButton"), "focus is back on the Settings button");

        // A later frame: Esc resumes.
        yield return null;
        Assert.IsTrue((bool)Call(pause, "ConsumeBack"));
        Assert.IsFalse((bool)Prop(manager, "IsPaused"), "the final Esc resumes");
    }

    // ---------- Back returns focus to the opener ----------

    [UnityTest]
    public IEnumerator BackRestoresOpenerFocus()
    {
        yield return Load("MainMenu");
        Component menu = (Component)Find("MainMenuScreen");
        Component settings = (Component)Find("SettingsScreen");
        Component log = (Component)Find("KeepersLogScreen");

        Button settingsButton = ButtonNamed(menu, "SettingsButton");
        settingsButton.onClick.Invoke();
        Call(settings, "ConsumeBack");
        AssertFocus(settingsButton, "Settings from the main menu: Back restores the Settings button");

        Button logButton = ButtonNamed(menu, "LogButton");
        logButton.onClick.Invoke();
        Call(log, "ConsumeBack");
        AssertFocus(logButton, "Log from the main menu: Back restores the Keeper's Log button");

        // Back from inside a section steps to the tab first, then closes: the opener still ends up focused.
        settingsButton.onClick.Invoke();
        SectionList list = (SectionList)Prop(settings, "SectionList");
        Selectable inside = list.SectionPanels[list.Current].GetComponentInChildren<Selectable>(false);
        EventSystem.current.SetSelectedGameObject(inside.gameObject);
        Call(settings, "ConsumeBack");
        AssertFocus(list.SectionButtons[list.Current], "Back inside a section returns to its tab");
        Call(settings, "ConsumeBack");
        AssertFocus(settingsButton, "a second Back closes Settings and restores the opener");

        yield return PauseIsland1();
        Component pause = (Component)Find("PauseScreen");
        Component islandSettings = (Component)Find("SettingsScreen");
        Button pauseSettings = (Button)Prop(pause, "SettingsButton");
        pauseSettings.onClick.Invoke();
        Call(islandSettings, "ConsumeBack");
        AssertFocus(pauseSettings, "Settings from pause: Back restores the pause menu's Settings button");
    }

    // ---------- main menu navigation ----------

    [UnityTest]
    public IEnumerator MainMenuRightLeftSubmit()
    {
        yield return Load("MainMenu");
        Component menu = (Component)Find("MainMenuScreen");
        Component play = (Component)Prop(menu, "PlayButton");
        Array rows = (Array)Prop(Prop(menu, "Islands"), "Rows");
        Assert.GreaterOrEqual(rows.Length, 2);
        AssertFocus(play, "the menu opens with Play focused");

        // Up and Down stay in the action column.
        Move(MoveDirection.Down);
        AssertFocus(ButtonNamed(menu, "LogButton"), "Down from Play goes to Keeper's Log");
        Move(MoveDirection.Down);
        AssertFocus(ButtonNamed(menu, "SettingsButton"), "Down goes to Settings");
        Move(MoveDirection.Down);
        AssertFocus(ButtonNamed(menu, "QuitButton"), "Down goes to Quit");
        Move(MoveDirection.Down);
        AssertFocus(ButtonNamed(menu, "QuitButton"), "Down from the last action stays put");
        Move(MoveDirection.Up);
        Move(MoveDirection.Up);
        Move(MoveDirection.Up);
        AssertFocus(play, "Up returns to Play");

        // Right goes to the selected island (the latest unlocked: Island 2 here); Left goes back to Play.
        int selected = (int)Prop(menu, "SelectedIndex");
        Assert.AreEqual(1, selected, "Island 2 is the latest unlocked island");
        Move(MoveDirection.Right);
        AssertFocus(RowButton(rows, selected), "Right goes to the selected island row");
        Move(MoveDirection.Left);
        AssertFocus(play, "Left from an island goes back to Play");

        // Submitting another island selects it and moves focus to Play.
        Move(MoveDirection.Right);
        Move(MoveDirection.Up);
        AssertFocus(RowButton(rows, 0), "Up moves to the Island 1 row");
        Submit();
        Assert.AreEqual(0, (int)Prop(menu, "SelectedIndex"), "Submit on Island 1 selects it");
        AssertFocus(play, "Submit on an island focuses Play");

        // Esc does nothing here: nothing to consume, the menu is still up.
        Assert.IsTrue(play.gameObject.activeInHierarchy);
    }

    [UnityTest]
    public IEnumerator PlayLoadsTheSelectedIslandsScene()
    {
        yield return Load("MainMenu");
        Component menu = (Component)Find("MainMenuScreen");
        Call(menu, "Select", 1);
        Assert.AreEqual(1, (int)Prop(menu, "SelectedIndex"));

        string loaded = null;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler = (scene, mode) => loaded = scene.name;
        SceneManager.sceneLoaded += handler;
        try
        {
            ((Button)Prop(menu, "PlayButton")).onClick.Invoke();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (loaded == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }
        finally
        {
            SceneManager.sceneLoaded -= handler;
        }
        Assert.AreEqual("Island2", loaded, "Play loads the selected island's scene");

        yield return Load("MainMenu");
        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "back on the main menu");
    }

    [UnityTest]
    public IEnumerator DifficultySwitchSetsGameSettings()
    {
        Type settingsType = Game("GameSettings");
        PropertyInfo current = settingsType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
        object original = current.GetValue(null);
        try
        {
            current.SetValue(null, Enum.ToObject(current.PropertyType, 1));
            yield return Load("MainMenu");
            Component menu = (Component)Find("MainMenuScreen");
            SwitchRow row = (SwitchRow)Prop(Prop(menu, "Islands"), "DifficultyRow");
            Assert.AreEqual(1, row.Index, "the switch shows Normal");

            row.Cycle(1);
            Assert.AreEqual(2, Convert.ToInt32(current.GetValue(null)), "Hard");
            row.Cycle(-1);
            row.Cycle(-1);
            Assert.AreEqual(0, Convert.ToInt32(current.GetValue(null)), "Easy");
            row.Cycle(1);
            Assert.AreEqual(1, Convert.ToInt32(current.GetValue(null)), "back to Normal");
        }
        finally
        {
            current.SetValue(null, original);
        }
    }
}
}
