using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The Settings hint line in a live scene: selecting a row shows its hint, cycling Preset or Render scale updates it,
// and the Settings screen under the island's pause menu carries the hint line too.
public class SettingsHintLiveTest
{
    const string GraphicsKey = "LanternKeeperGraphics";

    int savedGraphics;
    float savedScale;
    bool hadScale;

    [SetUp]
    public void SetUp()
    {
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
        hadScale = PlayerPrefs.HasKey("LanternKeeperRenderScale");
        savedScale = UserSettings.RenderScale;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        UserSettings.RenderScale = savedScale;
        if (!hadScale)
        {
            PlayerPrefs.DeleteKey("LanternKeeperRenderScale");
        }
        PlayerPrefs.Save();
    }

    static Type Game(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name);
        return type;
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

    static object Prop(object target, string name)
    {
        PropertyInfo info = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, target.GetType().Name + "." + name);
        return info.GetValue(target);
    }

    static SwitchRow RowWithId(Component settings, string id)
    {
        foreach (SwitchRow row in settings.GetComponentsInChildren<SwitchRow>(true))
        {
            SettingsHintRow hint = row.GetComponent<SettingsHintRow>();
            if (hint != null && hint.HintId == id)
            {
                return row;
            }
        }
        Assert.Fail("no switch row with hint id " + id);
        return null;
    }

    [UnityTest]
    public IEnumerator SelectingARowShowsItsHintAndPresetFollowsTheValue()
    {
        PlayerPrefs.SetInt(GraphicsKey, 1);
        yield return Load("MainMenu");
        Component settings = (Component)UnityEngine.Object.FindFirstObjectByType(Game("SettingsScreen"), FindObjectsInactive.Include);
        Assert.IsNotNull(settings);
        Game("SettingsScreen").GetMethod("Open").Invoke(settings, new object[] { null, null });
        SectionList list = (SectionList)Prop(settings, "SectionList");
        string Hint() { return (string)Prop(settings, "CurrentHint"); }

        // Opened on Display with a tab selected: no row, so the neutral line.
        Assert.AreEqual(SettingsHints.Neutral, Hint(), "nothing selected, nothing to say");

        // Graphics tab, then the Preset row.
        list.Show(1);
        SwitchRow preset = RowWithId(settings, SettingsHints.Preset);
        EventSystem.current.SetSelectedGameObject(preset.gameObject);
        yield return null;
        Assert.AreEqual(SettingsHints.Get(SettingsHints.Preset, preset.Index), Hint(), "Preset shows the hint for the current value");
        Assert.AreEqual(((TMPro.TMP_Text)((ThemedLabel)Prop(settings, "HintLabel")).Text).text, Hint(), "the label shows it");

        // Cycling the value updates the hint at once.
        int before = preset.Index;
        preset.Cycle(1);
        int after = preset.Index;
        Assert.AreNotEqual(before, after);
        Assert.AreEqual(SettingsHints.Get(SettingsHints.Preset, after), Hint(), "Preset hint follows the value");
        preset.Cycle(-1);
        Assert.AreEqual(SettingsHints.Get(SettingsHints.Preset, before), Hint());

        // Render scale follows its value too.
        SwitchRow scale = RowWithId(settings, SettingsHints.RenderScale);
        EventSystem.current.SetSelectedGameObject(scale.gameObject);
        yield return null;
        Assert.AreEqual(SettingsHints.Get(SettingsHints.RenderScale, scale.Index), Hint());
        for (int i = 0; i < scale.Options.Length; i++)
        {
            scale.Cycle(1);
            Assert.AreEqual(SettingsHints.Get(SettingsHints.RenderScale, scale.Index), Hint(), "Render scale " + scale.Options[scale.Index]);
        }

        // A single-text row.
        SwitchRow vsync = RowWithId(settings, SettingsHints.VSync);
        EventSystem.current.SetSelectedGameObject(vsync.gameObject);
        yield return null;
        Assert.AreEqual(SettingsHints.Get(SettingsHints.VSync, 0), Hint());

        // Back on a tab: the hint clears again.
        EventSystem.current.SetSelectedGameObject(list.SectionButtons[1].gameObject);
        yield return null;
        Assert.AreEqual(SettingsHints.Neutral, Hint());

        // The Controls tab explains that its rows are fixed.
        list.Show(4);
        Assert.AreEqual(SettingsHints.ControlsTab, Hint());
    }

    [UnityTest]
    public IEnumerator TheIslandsSettingsScreenHasTheHintLine()
    {
        yield return Load("Island1");
        Component settings = (Component)UnityEngine.Object.FindFirstObjectByType(Game("SettingsScreen"), FindObjectsInactive.Include);
        Assert.IsNotNull(settings, "the island has a Settings screen under pause");
        Assert.IsNotNull(Prop(settings, "HintLabel"), "and it carries the hint line");
        foreach (SwitchRow row in settings.GetComponentsInChildren<SwitchRow>(true))
        {
            Assert.IsNotNull(row.GetComponent<SettingsHintRow>(), row.Label + " has a hint id");
        }
    }
}
}
