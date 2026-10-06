using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
public class UiKitTests
{
    const string ThemePath = "Assets/Game/Art/UI/UITheme.asset";
    const string ScaleKey = "LanternKeeperTextScale";

    GameObject root;
    UITheme theme;
    bool hadScale;
    float savedScale;

    [SetUp]
    public void SetUp()
    {
        hadScale = PlayerPrefs.HasKey(ScaleKey);
        savedScale = PlayerPrefs.GetFloat(ScaleKey, 1f);
        PlayerPrefs.DeleteKey(ScaleKey);
        theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Assert.IsNotNull(theme);
        root = new GameObject("KitTestRoot", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Undo.ClearAll();
        UserSettings.DeferSave = false;
        PlayerPrefs.DeleteKey(ScaleKey);
        if (hadScale)
        {
            PlayerPrefs.SetFloat(ScaleKey, savedScale);
        }
        PlayerPrefs.Save();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Undo.ClearAll();
    }

    static GameObject Build(string method, params object[] args)
    {
        Type type = Type.GetType("LanternKeeper.UIBuilder, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "UIBuilder not found in Assembly-CSharp-Editor");
        System.Reflection.MethodInfo m = type.GetMethod(method);
        Assert.IsNotNull(m, "UIBuilder." + method + " not found");
        return (GameObject)m.Invoke(null, args);
    }

    [Test]
    public void ButtonPrimaryUsesAmber()
    {
        GameObject go = Build("MakeButton", root.transform, "Play", true);
        ThemedButton button = go.GetComponent<ThemedButton>();
        Assert.IsTrue(button.Primary);
        Assert.AreEqual(theme.amber, button.Gradient.Top, "gradient top is the theme amber");
        Assert.AreEqual(theme.amberDeep, button.Gradient.Bottom);
        Assert.AreEqual(ThemedLabel.ColourFor(theme, ThemedLabel.Role.Label, ThemedLabel.Tint.OnAmber), button.Label.Text.color);
        GameObject plain = Build("MakeButton", root.transform, "Back", false);
        Assert.IsNull(plain.GetComponent<ThemedButton>().Gradient);
        Assert.IsNotNull(button.FocusFrame);
        Assert.IsFalse(button.FocusFrame.activeSelf, "focus outline starts hidden");
    }

    [Test]
    public void LabelsUseThemeFonts()
    {
        foreach (ThemedLabel.Role role in Enum.GetValues(typeof(ThemedLabel.Role)))
        {
            GameObject go = Build("MakeLabel", root.transform, "Hello", role, 22f);
            TMP_Text text = go.GetComponent<ThemedLabel>().Text;
            TMP_FontAsset expected = ThemedLabel.FontFor(theme, role);
            Assert.AreSame(expected, text.font, role.ToString());
            Assert.IsFalse(text.font.name.Contains("Liberation"), role + " uses LiberationSans");
        }
        Assert.AreSame(theme.titleFont, ThemedLabel.FontFor(theme, ThemedLabel.Role.Title));
        Assert.AreSame(theme.flavourFont, ThemedLabel.FontFor(theme, ThemedLabel.Role.Flavour));
        Assert.AreSame(theme.uiFont, ThemedLabel.FontFor(theme, ThemedLabel.Role.Body));
        Assert.AreSame(theme.uiFontStrong, ThemedLabel.FontFor(theme, ThemedLabel.Role.Label));
    }

    [Test]
    public void InkPanelHasInkColourAndDoubleFrame()
    {
        GameObject go = Build("MakeInkPanel", root.transform, new Vector2(400, 300));
        Assert.AreEqual(theme.inkPanel, go.GetComponent<Image>().color);
        Assert.IsNotNull(go.transform.Find("Frame"));
        Assert.IsNotNull(go.transform.Find("FrameInner"));
    }

    [Test]
    public void SliderRowRaisesValueChanged()
    {
        GameObject go = Build("MakeSliderRow", root.transform, "Master");
        SliderRow row = go.GetComponent<SliderRow>();
        float got = -1f;
        row.ValueChanged += v => got = v;
        row.Slider.value = 0.5f;
        Assert.AreEqual(0.5f, got, 0.0001f);
        got = -1f;
        row.SetValueWithoutNotify(0.25f);
        Assert.AreEqual(-1f, got, "SetValueWithoutNotify must not raise");
        Assert.AreEqual(0.25f, row.Slider.value, 0.0001f);
    }

    [Test]
    public void SliderRowDefersSavingButStillNotifiesSettings()
    {
        GameObject go = Build("MakeSliderRow", root.transform, "Master");
        SliderRow row = go.GetComponent<SliderRow>();
        int changed = 0;
        Action handler = () => changed++;
        UserSettings.Changed += handler;
        row.ValueChanged += v => UserSettings.TextScale = 1.3f;
        try
        {
            row.Slider.value = 0.4f;
            Assert.AreEqual(1, changed, "Changed fires live");
            Assert.IsTrue(UserSettings.PendingSave, "save is deferred while dragging");
            Assert.IsFalse(UserSettings.DeferSave, "DeferSave is restored after the handlers run");
            row.Commit();
            Assert.IsFalse(UserSettings.PendingSave, "pointer-up/commit flushes");
        }
        finally
        {
            UserSettings.Changed -= handler;
        }
    }

    [Test]
    public void SwitchRowCyclesAndWraps()
    {
        GameObject go = Build("MakeSwitchRow", root.transform, "Mode", new[] { "A", "B", "C" }, 2);
        SwitchRow row = go.GetComponent<SwitchRow>();
        int last = -1;
        row.IndexChanged += i => last = i;
        Assert.AreEqual(2, row.Index);
        row.Cycle(1);
        Assert.AreEqual(0, row.Index, "index 2 plus right wraps to 0");
        Assert.AreEqual(0, last);
        row.Cycle(-1);
        Assert.AreEqual(2, row.Index, "left from 0 wraps to 2");
        Assert.AreEqual("C", row.ValueText.text);
        last = -1;
        row.SetIndexWithoutNotify(1);
        Assert.AreEqual(-1, last);
        Assert.AreEqual("B", row.ValueText.text);
    }

    [Test]
    public void DropdownRowSetsOptionsWithoutNotify()
    {
        GameObject go = Build("MakeDropdownRow", root.transform, "Resolution");
        DropdownRow row = go.GetComponent<DropdownRow>();
        int fired = 0;
        row.IndexChanged += i => fired++;
        row.SetOptions(new[] { "1280x720", "1920x1080", "2560x1440" }, 1);
        Assert.AreEqual(1, row.Dropdown.value);
        Assert.AreEqual(3, row.Dropdown.options.Count);
        Assert.AreEqual(0, fired);
        row.Dropdown.value = 2;
        Assert.AreEqual(1, fired);
    }

    [Test]
    public void SectionListShowsOnlyOne()
    {
        GameObject go = Build("MakeSectionList", root.transform, new[] { "Audio", "Video", "Access", "Controls" });
        SectionList list = go.GetComponent<SectionList>();
        int changed = -1;
        list.SectionChanged += i => changed = i;
        list.Show(2);
        Assert.AreEqual(2, list.Current);
        Assert.AreEqual(2, changed);
        for (int i = 0; i < list.SectionPanels.Length; i++)
        {
            Assert.AreEqual(i == 2, list.SectionPanels[i].activeSelf, "panel " + i);
            ThemedButton button = (ThemedButton)list.SectionButtons[i];
            Assert.AreEqual(i == 2, button.Marked, "button " + i + " marked");
        }
        ThemedButton marked = (ThemedButton)list.SectionButtons[2];
        Assert.AreEqual(theme.amber, marked.Label.Text.color);
    }

    [Test]
    public void ParchmentSpreadSetsTextAndFades()
    {
        GameObject go = Build("MakeParchmentSpread", root.transform, new Vector2(1400, 800));
        ParchmentSpread spread = go.GetComponent<ParchmentSpread>();
        spread.SetSpread("Title", "Sub", "Left", "Right");
        Assert.AreEqual("Title", spread.LeftTitle.text);
        Assert.AreEqual("Sub", spread.LeftSub.text);
        Assert.AreEqual("Left", spread.LeftBody.text);
        Assert.AreEqual("Right", spread.RightBody.text);
        Assert.AreEqual(theme.ribbon, spread.Ribbon.color);
        int prev = 0;
        int next = 0;
        spread.Prev += () => prev++;
        spread.Next += () => next++;
        spread.PrevButton.onClick.Invoke();
        spread.NextButton.onClick.Invoke();
        Assert.AreEqual(1, prev);
        Assert.AreEqual(1, next);
        IEnumerator turn = spread.TurnFade(0.2f);
        Assert.IsTrue(turn.MoveNext());
        Assert.Less(spread.Pages.alpha, 0.5f, "fade starts transparent");
    }

    [Test]
    public void TextScalerAppliesScale()
    {
        root.AddComponent<TextScaler>();
        GameObject go = Build("MakeLabel", root.transform, "Hello", ThemedLabel.Role.Body, 22f);
        ThemedLabel label = go.GetComponent<ThemedLabel>();
        UserSettings.TextScale = 1.0f;
        Assert.AreEqual(22f, label.Text.fontSize, 0.001f);
        UserSettings.TextScale = 1.3f;
        Assert.AreEqual(1.3f, TextScaler.Current, 0.0001f);
        Assert.AreEqual(28.6f, label.Text.fontSize, 0.001f);
    }
}
}
