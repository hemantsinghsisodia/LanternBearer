using System;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class UiPrefabTests
{
    const string SettingsPath = "Assets/Game/Prefabs/UI/SettingsScreen.prefab";
    const string MenuPath = "Assets/Game/Prefabs/UI/MainMenuScreen.prefab";
    const string ThemePath = "Assets/Game/Art/UI/UITheme.asset";

    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void SettingsScreenWired()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath);
        Assert.IsNotNull(prefab, "SettingsScreen prefab is missing; run Lantern Keeper > Build UI");
        Type screenType = Type.GetType("LanternKeeper.SettingsScreen, Assembly-CSharp");
        Assert.IsNotNull(screenType);
        Component screen = prefab.GetComponent(screenType);
        Assert.IsNotNull(screen, "SettingsScreen component on the root");
        Assert.IsNotNull(prefab.GetComponent<TextScaler>());

        SectionList list = prefab.GetComponentInChildren<SectionList>(true);
        Assert.IsNotNull(list);
        Assert.AreEqual(5, list.SectionButtons.Length, "5 section buttons");
        Assert.AreEqual(5, list.SectionPanels.Length, "5 panels");
        for (int i = 0; i < 5; i++)
        {
            Assert.IsNotNull(list.SectionButtons[i]);
            Assert.IsNotNull(list.SectionPanels[i]);
        }

        // Display: 1 slider, 1 dropdown, 1 switch. Graphics: 4 switches. Audio: 4 sliders. Accessibility: 2 switches.
        Assert.AreEqual(5, prefab.GetComponentsInChildren<SliderRow>(true).Length, "slider rows");
        Assert.AreEqual(7, prefab.GetComponentsInChildren<SwitchRow>(true).Length, "switch rows");
        Assert.AreEqual(1, prefab.GetComponentsInChildren<DropdownRow>(true).Length, "dropdown rows");
        foreach (SliderRow row in prefab.GetComponentsInChildren<SliderRow>(true))
        {
            Assert.IsNotNull(row.Slider);
        }

        SerializedObject so = new SerializedObject(screen);
        Assert.IsNotNull(so.FindProperty("firstSelected").objectReferenceValue, "firstSelected is set");
        Assert.IsNotNull(so.FindProperty("backButton").objectReferenceValue);
        string[] expected =
        {
            "How the night looks to you.",
            "What the lamp can afford.",
            "The sea, the wind, the flame.",
            "Make the way easier to see.",
            "How the keeper answers you."
        };
        SerializedProperty flavours = so.FindProperty("flavours");
        Assert.AreEqual(expected.Length, flavours.arraySize);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i], flavours.GetArrayElementAtIndex(i).stringValue);
        }

        foreach (string section in new[] { "DisplaySection", "GraphicsSection", "AudioSection", "AccessibilitySection", "ControlsSection" })
        {
            Type t = Type.GetType("LanternKeeper." + section + ", Assembly-CSharp");
            Assert.IsNotNull(prefab.GetComponentInChildren(t, true), section);
        }

        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        TMP_FontAsset[] allowed = { theme.titleFont, theme.flavourFont, theme.uiFont, theme.uiFontStrong };
        foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.Contains(text.font, allowed, "theme font on " + text.name);
        }
    }

    [Test]
    public void MainMenuScreenWired()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        Assert.IsNotNull(prefab, "MainMenuScreen prefab is missing; run Lantern Keeper > Build UI");
        Type screenType = Type.GetType("LanternKeeper.MainMenuScreen, Assembly-CSharp");
        Type listType = Type.GetType("LanternKeeper.IslandList, Assembly-CSharp");
        Assert.IsNotNull(screenType);
        Assert.IsNotNull(listType);
        Component screen = prefab.GetComponent(screenType);
        Assert.IsNotNull(screen, "MainMenuScreen on the root");
        Assert.IsNotNull(prefab.GetComponent<TextScaler>());

        SerializedObject so = new SerializedObject(screen);
        string[] names = { "playButton", "logButton", "settingsButton", "quitButton" };
        string[] texts = { "Play", "Keeper's Log", "Settings", "Quit" };
        for (int i = 0; i < names.Length; i++)
        {
            ThemedButton button = so.FindProperty(names[i]).objectReferenceValue as ThemedButton;
            Assert.IsNotNull(button, names[i]);
            Assert.AreEqual(i == 0, button.Primary, names[i] + " primary");
            Assert.AreEqual(texts[i], button.Label.Text.text);
        }
        Assert.AreSame(so.FindProperty("playButton").objectReferenceValue, so.FindProperty("firstSelected").objectReferenceValue, "firstSelected is Play");

        Component list = prefab.GetComponentInChildren(listType, true);
        Assert.IsNotNull(list, "IslandList");
        SerializedObject listObject = new SerializedObject(list);
        SerializedProperty rows = listObject.FindProperty("rows");
        Assert.AreEqual(4, rows.arraySize, "4 island rows");
        for (int i = 0; i < 4; i++)
        {
            Assert.IsNotNull(rows.GetArrayElementAtIndex(i).objectReferenceValue, "row " + i);
        }

        SwitchRow[] switches = prefab.GetComponentsInChildren<SwitchRow>(true);
        Assert.AreEqual(1, switches.Length, "one switch row");
        Assert.AreEqual(new[] { "Easy", "Normal", "Hard" }, switches[0].Options);
        Assert.AreSame(switches[0], so.FindProperty("difficultyRow").objectReferenceValue);

        string all = "";
        foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            all += text.text + "|";
        }
        StringAssert.Contains("Lantern Keeper", all);
        StringAssert.Contains("Light the beacons before the flame dies.", all);
        StringAssert.Contains("Islands", all);

        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        TMP_FontAsset[] allowed = { theme.titleFont, theme.flavourFont, theme.uiFont, theme.uiFontStrong };
        foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.Contains(text.font, allowed, "theme font on " + text.name);
        }
    }

    [Test]
    public void LogScreenWired()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/UI/KeepersLogScreen.prefab");
        Assert.IsNotNull(prefab, "KeepersLogScreen prefab is missing; run Lantern Keeper > Build UI");
        Type screenType = Type.GetType("LanternKeeper.KeepersLogScreen, Assembly-CSharp");
        Assert.IsNotNull(screenType);
        Component screen = prefab.GetComponent(screenType);
        Assert.IsNotNull(screen, "KeepersLogScreen on the root");
        Assert.IsNotNull(prefab.GetComponent<TextScaler>());

        ParchmentSpread spread = prefab.GetComponentInChildren<ParchmentSpread>(true);
        Assert.IsNotNull(spread, "ParchmentSpread");
        Assert.IsNotNull(spread.PrevButton, "Prev");
        Assert.IsNotNull(spread.NextButton, "Next");
        Assert.IsNotNull(spread.Ribbon, "ribbon");
        Assert.IsNotNull(spread.Pages, "pages group");

        SerializedObject so = new SerializedObject(screen);
        Assert.AreSame(spread, so.FindProperty("spread").objectReferenceValue);
        UnityEngine.UI.Button close = so.FindProperty("closeButton").objectReferenceValue as UnityEngine.UI.Button;
        Assert.IsNotNull(close, "Close button");
        Assert.AreEqual("Close", close.GetComponentInChildren<TMP_Text>(true).text);
        Assert.IsNotNull(so.FindProperty("firstSelected").objectReferenceValue, "firstSelected is set");

        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        TMP_FontAsset[] allowed = { theme.titleFont, theme.flavourFont, theme.uiFont, theme.uiFontStrong };
        foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.Contains(text.font, allowed, "theme font on " + text.name);
        }
        Assert.AreSame(theme.flavourFont, spread.LeftBody.font, "entries are Spectral italic");
        Assert.AreSame(theme.flavourFont, spread.RightBody.font, "entries are Spectral italic");
    }

    [Test]
    public void PauseScreenWired()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/UI/PauseScreen.prefab");
        Assert.IsNotNull(prefab, "PauseScreen prefab is missing; run Lantern Keeper > Build UI");
        Type screenType = Type.GetType("LanternKeeper.PauseScreen, Assembly-CSharp");
        Assert.IsNotNull(screenType);
        Component screen = prefab.GetComponent(screenType);
        Assert.IsNotNull(screen, "PauseScreen on the root");
        Assert.IsNotNull(prefab.GetComponent<TextScaler>());

        SerializedObject so = new SerializedObject(screen);
        string[] names = { "resumeButton", "restartButton", "howToButton", "settingsButton", "menuButton" };
        string[] texts = { "Resume", "Restart", "How to Play", "Settings", "Main Menu" };
        float lastY = float.MaxValue;
        for (int i = 0; i < names.Length; i++)
        {
            ThemedButton button = so.FindProperty(names[i]).objectReferenceValue as ThemedButton;
            Assert.IsNotNull(button, names[i]);
            Assert.AreEqual(i == 0, button.Primary, names[i] + " primary");
            Assert.AreEqual(texts[i], button.Label.Text.text);
            float y = ((RectTransform)button.transform).anchorMax.y * 10000f + ((RectTransform)button.transform).offsetMax.y;
            Assert.Less(y, lastY, names[i] + " sits below the previous button");
            lastY = y;
        }
        Assert.AreSame(so.FindProperty("resumeButton").objectReferenceValue, so.FindProperty("firstSelected").objectReferenceValue, "firstSelected is Resume");
        Assert.IsNotNull(so.FindProperty("panel").objectReferenceValue, "panel");

        ThemedLabel title = so.FindProperty("title").objectReferenceValue as ThemedLabel;
        Assert.IsNotNull(title);
        Assert.AreEqual("Paused", title.Text.text);
        Assert.IsNotNull(so.FindProperty("subtitle").objectReferenceValue, "island subtitle");
        Assert.AreEqual(5, prefab.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length, "five buttons");

        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        TMP_FontAsset[] allowed = { theme.titleFont, theme.flavourFont, theme.uiFont, theme.uiFontStrong };
        foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.Contains(text.font, allowed, "theme font on " + text.name);
        }
    }

    [Test]
    public void IslandHudsCarryPauseAndSettings()
    {
        // The scene HUDs are wired by Lantern Keeper > Install Pause; Scene Wiring validates the same references.
        Type hudType = Type.GetType("LanternKeeper.HUD, Assembly-CSharp");
        Assert.IsNotNull(hudType);
        Assert.IsNull(hudType.GetField("pausePanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), "old pause panel field is gone");
        Assert.IsNotNull(hudType.GetField("pauseScreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), "HUD keeps a PauseScreen reference");
    }

    [Test]
    public void MainMenuRowsShowBeaconCount()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        Assert.IsNotNull(prefab);
        Type rowType = Type.GetType("LanternKeeper.IslandRow, Assembly-CSharp");
        Assert.IsNotNull(rowType);
        Component[] rows = prefab.GetComponentsInChildren(rowType, true);
        Assert.AreEqual(4, rows.Length);
        foreach (Component row in rows)
        {
            ThemedLabel count = new SerializedObject(row).FindProperty("countLabel").objectReferenceValue as ThemedLabel;
            Assert.IsNotNull(count, "count label on " + row.name);
            Assert.AreEqual(ThemedLabel.Role.Number, count.CurrentRole);
        }
    }

    [Test]
    public void IslandListMaths()
    {
        Type listType = Type.GetType("LanternKeeper.IslandList, Assembly-CSharp");
        Assert.IsNotNull(listType);
        System.Reflection.MethodInfo amber = listType.GetMethod("AmberRoofs");
        System.Reflection.MethodInfo latest = listType.GetMethod("LatestUnlocked");
        Assert.AreEqual(3, amber.Invoke(null, new object[] { 3, 5 }));
        Assert.AreEqual(5, amber.Invoke(null, new object[] { 9, 5 }), "capped at the beacon count");
        Assert.AreEqual(0, amber.Invoke(null, new object[] { 4, 0 }), "no beacons, no amber roofs");
        Assert.AreEqual(0, amber.Invoke(null, new object[] { -1, 5 }));
        Assert.AreEqual(2, latest.Invoke(null, new object[] { new[] { true, true, true, false } }));
        Assert.AreEqual(0, latest.Invoke(null, new object[] { new[] { true, false, false, false } }));
        Assert.AreEqual(0, latest.Invoke(null, new object[] { new[] { false, false } }), "falls back to the first island");
    }
}
}
