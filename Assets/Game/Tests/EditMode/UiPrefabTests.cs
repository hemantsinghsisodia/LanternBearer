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

        // Display: 1 slider, 1 dropdown, 1 switch. Graphics: 3 switches. Audio: 4 sliders. Accessibility: 2 switches.
        Assert.AreEqual(5, prefab.GetComponentsInChildren<SliderRow>(true).Length, "slider rows");
        Assert.AreEqual(6, prefab.GetComponentsInChildren<SwitchRow>(true).Length, "switch rows");
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
}
}
