using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// The Settings hint line: every row in the prefab has a hint id with text, and Preset / Render scale have a text per value.
public class SettingsHintTests
{
    const string SettingsPath = "Assets/Game/Prefabs/UI/SettingsScreen.prefab";

    [Test]
    public void EverySettingsRowHasAHintId()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath);
        Assert.IsNotNull(prefab, "SettingsScreen prefab is missing; run Lantern Keeper > Build UI");

        List<string> problems = new List<string>();
        HashSet<string> seen = new HashSet<string>();

        SwitchRow[] switches = prefab.GetComponentsInChildren<SwitchRow>(true);
        SliderRow[] sliders = prefab.GetComponentsInChildren<SliderRow>(true);
        DropdownRow[] dropdowns = prefab.GetComponentsInChildren<DropdownRow>(true);
        List<Component> rows = new List<Component>();
        rows.AddRange(switches);
        rows.AddRange(sliders);
        rows.AddRange(dropdowns);
        Assert.AreEqual(13, rows.Count, "13 settings rows (1+1+1 Display, 4 Graphics, 4 Audio, 2 Accessibility)");

        for (int i = 0; i < rows.Count; i++)
        {
            SettingsHintRow hint = rows[i].GetComponent<SettingsHintRow>();
            if (hint == null || string.IsNullOrEmpty(hint.HintId))
            {
                problems.Add(rows[i].name + " has no hint id");
                continue;
            }
            if (!seen.Add(hint.HintId))
            {
                problems.Add("duplicate hint id " + hint.HintId);
            }
            if (string.IsNullOrEmpty(SettingsHints.Get(hint.HintId, 0)))
            {
                problems.Add(hint.HintId + " has no hint text");
            }
        }
        for (int i = 0; i < SettingsHints.SettingRowIds.Length; i++)
        {
            if (!seen.Contains(SettingsHints.SettingRowIds[i]))
            {
                problems.Add("no row carries the id " + SettingsHints.SettingRowIds[i]);
            }
        }

        // Controls rows are labels: one hint each, in binding order.
        Type controls = Type.GetType("LanternKeeper.ControlsSection, Assembly-CSharp");
        Assert.IsNotNull(controls);
        string[] bindings = (string[])controls.GetField("Bindings").GetValue(null);
        SettingsHintRow[] all = prefab.GetComponentsInChildren<SettingsHintRow>(true);
        Assert.AreEqual(13 + bindings.Length, all.Length, "one hint row per settings row and per binding");
        Assert.AreEqual(bindings.Length, SettingsHints.ControlHints.Length, "a control hint per binding");
        for (int i = 0; i < bindings.Length; i++)
        {
            Assert.IsFalse(string.IsNullOrEmpty(SettingsHints.Get(SettingsHints.ControlId(i), 0)), "control " + i + " has a hint");
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void PresetAndRenderScaleHaveAHintForEveryValue()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath);
        Assert.IsNotNull(prefab);
        SwitchRow preset = null;
        SwitchRow renderScale = null;
        foreach (SwitchRow row in prefab.GetComponentsInChildren<SwitchRow>(true))
        {
            SettingsHintRow hint = row.GetComponent<SettingsHintRow>();
            if (hint != null && hint.HintId == SettingsHints.Preset) { preset = row; }
            if (hint != null && hint.HintId == SettingsHints.RenderScale) { renderScale = row; }
        }
        Assert.IsNotNull(preset);
        Assert.IsNotNull(renderScale);
        Assert.AreEqual(preset.Options.Length, SettingsHints.ValueCount(SettingsHints.Preset));
        Assert.AreEqual(renderScale.Options.Length, SettingsHints.ValueCount(SettingsHints.RenderScale));
        Assert.AreEqual(SettingsMath.RenderScales.Length, SettingsHints.RenderScaleHints.Length);

        HashSet<string> presetTexts = new HashSet<string>();
        for (int i = 0; i < preset.Options.Length; i++)
        {
            string text = SettingsHints.Get(SettingsHints.Preset, i);
            Assert.IsFalse(string.IsNullOrEmpty(text), "Preset " + preset.Options[i]);
            Assert.IsTrue(presetTexts.Add(text), "Preset " + preset.Options[i] + " has its own text");
        }
        HashSet<string> scaleTexts = new HashSet<string>();
        for (int i = 0; i < renderScale.Options.Length; i++)
        {
            string text = SettingsHints.Get(SettingsHints.RenderScale, i);
            Assert.IsFalse(string.IsNullOrEmpty(text), "Render scale " + renderScale.Options[i]);
            scaleTexts.Add(text);
        }
        Assert.AreEqual(3, scaleTexts.Count, "100%, the 125/150% pair and 200% read differently");
    }

    [Test]
    public void LongestHintIsShortEnoughForTwoLines()
    {
        int longest = 0;
        for (int i = 0; i < SettingsHints.SettingRowIds.Length; i++)
        {
            string id = SettingsHints.SettingRowIds[i];
            for (int v = 0; v < SettingsHints.ValueCount(id); v++)
            {
                longest = Math.Max(longest, SettingsHints.Get(id, v).Length);
            }
        }
        Assert.LessOrEqual(longest, 160, "keep every hint to roughly two lines at 130% text");
    }
}
}
