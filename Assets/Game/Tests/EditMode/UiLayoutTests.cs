using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
// Text scale 1.3 at 1920x1080 and 1280x720: nothing in a screen may overflow its box.
// Every screen prefab is instantiated under a canvas of the given size, scaled, laid out and measured.
// The HUD canvas is measured from the Island1 scene. Log pages are exempt from width (they wrap) but must fit their height.
public class UiLayoutTests
{
    const string ScaleKey = "LanternKeeperTextScale";
    const string PrefabFolder = "Assets/Game/Prefabs/UI/";
    const string ThemePath = "Assets/Game/Art/UI/UITheme.asset";

    static readonly Vector2[] CanvasSizes = { new Vector2(1920f, 1080f), new Vector2(1280f, 720f) };

    // HUD texts that are still Phase F's: plain code-made labels whose box is not a layout row. Each entry says why.
    // (Anything else under the HUD canvas that overflows at 130% fails the test.)
    static readonly HashSet<string> PhaseFHudTexts = new HashSet<string>
    {
        "WinPanel", "LosePanel" // end-of-round panels: restyled in Phase F2 task 4
    };

    GameObject canvasGo;
    bool hadScale;
    float savedScale;
    readonly List<UnityEngine.SceneManagement.Scene> openedScenes = new List<UnityEngine.SceneManagement.Scene>();

    [SetUp]
    public void SetUp()
    {
        hadScale = PlayerPrefs.HasKey(ScaleKey);
        savedScale = PlayerPrefs.GetFloat(ScaleKey, 1f);
        UserSettings.DeferSave = true;
    }

    [TearDown]
    public void TearDown()
    {
        if (canvasGo != null)
        {
            UnityEngine.Object.DestroyImmediate(canvasGo);
            canvasGo = null;
        }
        for (int i = 0; i < openedScenes.Count; i++)
        {
            if (openedScenes[i].IsValid())
            {
                EditorSceneManager.CloseScene(openedScenes[i], true);
            }
        }
        openedScenes.Clear();
        Undo.ClearAll();
        UserSettings.TextScale = savedScale;
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

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    GameObject MakeCanvas(Vector2 size)
    {
        if (canvasGo != null)
        {
            UnityEngine.Object.DestroyImmediate(canvasGo);
        }
        canvasGo = new GameObject("LayoutTestCanvas", typeof(RectTransform), typeof(Canvas));
        // A root Overlay canvas takes its size from the Game view and ignores sizeDelta, so the loop would not vary the size.
        // A WorldSpace canvas keeps the rect we set.
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform rect = (RectTransform)canvasGo.transform;
        rect.sizeDelta = size;
        Assert.AreEqual(size.x, rect.rect.width, 0.01f, "canvas width sticks");
        Assert.AreEqual(size.y, rect.rect.height, 0.01f, "canvas height sticks");
        return canvasGo;
    }

    GameObject Instantiate(string prefabName, Vector2 size)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");
        Assert.IsNotNull(prefab, prefabName + " prefab is missing; run Lantern Keeper > Build UI");
        GameObject parent = MakeCanvas(size);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        instance.SetActive(true);
        return instance;
    }

    static void Layout(GameObject root)
    {
        TextScaler scaler = root.GetComponentInParent<TextScaler>(true);
        if (scaler != null)
        {
            scaler.Reapply();
        }
        Canvas.ForceUpdateCanvases();
        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].GetComponent<LayoutGroup>() != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rects[i]);
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    static string PathOf(Transform t, Transform root)
    {
        string path = t.name;
        for (Transform walk = t.parent; walk != null && walk != root.parent; walk = walk.parent)
        {
            path = walk.name + "/" + path;
        }
        return path;
    }

    // Checks one text. widthExempt: wrapped body text (Log pages) where only the height matters.
    static void Check(TMP_Text text, Transform root, string label, bool widthExempt, float minPx, List<string> problems)
    {
        if (string.IsNullOrEmpty(text.text))
        {
            return;
        }
        text.ForceMeshUpdate();
        RectTransform rect = text.rectTransform;
        string where = label + " '" + PathOf(text.transform, root) + "' (\"" + Shorten(text.text) + "\")";
        if (text.isTextOverflowing)
        {
            Vector2 pref = text.GetPreferredValues(text.text);
            problems.Add(where + " overflows (isTextOverflowing): font " + text.fontSize.ToString("F1") + ", needs " + pref.x.ToString("F0") + "x"
                + pref.y.ToString("F0") + ", box " + rect.rect.width.ToString("F0") + "x" + rect.rect.height.ToString("F0"));
        }
        bool wraps = text.textWrappingMode != TextWrappingModes.NoWrap;
        if (!widthExempt)
        {
            Vector2 preferred = wraps ? text.GetPreferredValues(text.text, rect.rect.width, 0f) : text.GetPreferredValues(text.text);
            if (text.enableAutoSizing)
            {
                // The preferred size is at the maximum; what counts is what the auto-size settled on, and it may not shrink more than ThemedLabel.MaxShrinkPx.
                preferred = new Vector2(text.renderedWidth, text.renderedHeight);
                if (text.fontSizeMax - text.fontSize > ThemedLabel.MaxShrinkPx + 0.01f)
                {
                    problems.Add(where + " auto-sized to " + text.fontSize.ToString("F1") + " from " + text.fontSizeMax.ToString("F1"));
                }
            }
            if (!wraps && preferred.x > rect.rect.width + 1f)
            {
                problems.Add(where + " is " + preferred.x.ToString("F1") + " px wide in a " + rect.rect.width.ToString("F1") + " px box");
            }
            if (wraps && preferred.y > rect.rect.height + 1f)
            {
                problems.Add(where + " is " + preferred.y.ToString("F1") + " px tall in a " + rect.rect.height.ToString("F1") + " px box");
            }
        }
        else
        {
            float h = text.GetPreferredValues(text.text, rect.rect.width, 0f).y;
            if (h > rect.rect.height + 1f)
            {
                problems.Add(where + " needs " + h.ToString("F1") + " px of height, has " + rect.rect.height.ToString("F1"));
            }
        }
        // The floor is bodyPx x 0.9 at the 100% base, so undo the text scale before comparing.
        float basePx = text.fontSize / Mathf.Max(0.01f, TextScaler.Current);
        if (basePx < minPx - 0.01f && !text.enableAutoSizing)
        {
            problems.Add(where + " base font " + basePx.ToString("F1") + " is below the minimum " + minPx.ToString("F1"));
        }
    }

    static string Shorten(string s)
    {
        s = s.Replace("\n", " ");
        return s.Length > 28 ? s.Substring(0, 28) + "..." : s;
    }

    static void CheckAll(GameObject root, string label, float minPx, List<string> problems, Func<TMP_Text, bool> widthExempt)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(false);
        for (int i = 0; i < texts.Length; i++)
        {
            Check(texts[i], root.transform, label, widthExempt != null && widthExempt(texts[i]), minPx, problems);
        }
    }

    static float MinPx()
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Assert.IsNotNull(theme);
        // Never below bodyPx x 0.9 (the brief's floor), measured at the 100% base.
        return theme.bodyPx * 0.9f;
    }

    static void ScreenSettings(GameObject instance, List<string> problems, string label, float minPx)
    {
        SectionList list = instance.GetComponentInChildren<SectionList>(true);
        Assert.IsNotNull(list);
        for (int s = 0; s < list.SectionPanels.Length; s++)
        {
            list.Show(s);
            Layout(instance);
            CheckAll(instance, label + " section " + s, minPx, problems, null);
        }
    }

    // A MainMenu component stands in for the scene's data source: the real LevelConfig assets, in level order.
    static void FeedMenu(GameObject canvas, GameObject screenInstance)
    {
        Type menuType = GameType("MainMenu");
        Component menu = canvas.AddComponent(menuType);
        string[] guids = AssetDatabase.FindAssets("t:LevelConfig");
        List<UnityEngine.Object> levels = new List<UnityEngine.Object>();
        for (int i = 0; i < guids.Length; i++)
        {
            levels.Add(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guids[i])));
        }
        levels.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        SerializedObject so = new SerializedObject(menu);
        SerializedProperty prop = so.FindProperty("levels");
        prop.arraySize = levels.Count;
        for (int i = 0; i < levels.Count; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Type screenType = GameType("MainMenuScreen");
        Component screen = screenInstance.GetComponent(screenType);
        screenType.GetField("menu", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(screen, menu);
        screenType.GetMethod("Refresh").Invoke(screen, null);
    }

    [Test]
    public void NoOverflowAt130Percent()
    {
        List<string> problems = new List<string>();
        float minPx = MinPx();
        UserSettings.TextScale = 1.3f;
        Assert.AreEqual(1.3f, TextScaler.Current, 0.001f, "text scale is 130%");

        for (int c = 0; c < CanvasSizes.Length; c++)
        {
            Vector2 size = CanvasSizes[c];
            string at = (int)size.x + "x" + (int)size.y;

            GameObject settings = Instantiate("SettingsScreen", size);
            ScreenSettings(settings, problems, "Settings@" + at, minPx);

            GameObject pause = Instantiate("PauseScreen", size);
            Component pauseScreen = pause.GetComponent(GameType("PauseScreen"));
            // The pause panel is hidden until shown; show it for measuring.
            FieldInfo panelField = GameType("PauseScreen").GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
            ((GameObject)panelField.GetValue(pauseScreen)).SetActive(true);
            Layout(pause);
            CheckAll(pause, "Pause@" + at, minPx, problems, null);

            GameObject menu = Instantiate("MainMenuScreen", size);
            FeedMenu(canvasGo, menu);
            Layout(menu);
            CheckAll(menu, "MainMenu@" + at, minPx, problems, null);

            GameObject log = Instantiate("KeepersLogScreen", size);
            CheckLog(log, problems, at, minPx);
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // Log pages wrap, so only the height matters: every page of the real log, at this scale, fits its text box.
    void CheckLog(GameObject log, List<string> problems, string at, float minPx)
    {
        Type screenType = GameType("KeepersLogScreen");
        Type logType = GameType("IslandLog");
        Component screen = log.GetComponent(screenType);
        // Real entries from the LevelConfigs, all found, so every spread is as full as it gets.
        string[] guids = AssetDatabase.FindAssets("t:LevelConfig");
        List<UnityEngine.Object> levels = new List<UnityEngine.Object>();
        for (int i = 0; i < guids.Length; i++)
        {
            levels.Add(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guids[i])));
        }
        levels.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        Array islands = Array.CreateInstance(logType, levels.Count);
        for (int i = 0; i < levels.Count; i++)
        {
            SerializedObject so = new SerializedObject(levels[i]);
            SerializedProperty entries = so.FindProperty("logEntries");
            string[] lines = new string[entries.arraySize];
            for (int e = 0; e < lines.Length; e++)
            {
                lines[e] = entries.GetArrayElementAtIndex(e).stringValue;
            }
            object item = Activator.CreateInstance(logType);
            logType.GetField("header").SetValue(item, so.FindProperty("displayName").stringValue + " · " + so.FindProperty("islandTitle").stringValue);
            logType.GetField("entries").SetValue(item, lines);
            logType.GetField("found").SetValue(item, lines.Length);
            islands.SetValue(item, i);
        }
        Layout(log);
        screenType.GetMethod("Populate").Invoke(screen, new object[] { islands });
        Layout(log);
        ParchmentSpread spread = log.GetComponentInChildren<ParchmentSpread>(true);
        int count = (int)screenType.GetProperty("SpreadCount").GetValue(screen);
        Assert.Greater(count, 0, "the log has spreads");
        MethodInfo turn = screenType.GetMethod("TurnTo");
        for (int s = 0; s < count; s++)
        {
            turn.Invoke(screen, new object[] { s });
            Layout(log);
            CheckAll(log, "Log@" + at + " spread " + s, minPx, problems,
                text => text == spread.LeftBody || text == spread.RightBody);
        }
    }

    [Test]
    public void HudCanvasFitsAt130Percent()
    {
        List<string> problems = new List<string>();
        UserSettings.TextScale = 1.3f;
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Island1.unity", OpenSceneMode.Additive);
        openedScenes.Add(scene);
        Type hudType = GameType("HUD");
        Component hud = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            hud = go.GetComponentInChildren(hudType, true);
            if (hud != null)
            {
                break;
            }
        }
        Assert.IsNotNull(hud, "Island1 has a HUD");
        TextScaler scaler = hud.GetComponent<TextScaler>();
        Assert.IsNotNull(scaler, "the HUD canvas has a TextScaler");
        Assert.IsTrue(scaler.ScalePlainText, "the HUD scaler scales plain texts");

        scaler.Reapply();
        Canvas.ForceUpdateCanvases();

        // HUD texts are anchored to a corner or the centre with fixed boxes, so the canvas size does not change their fit.
        string at = "any size";
        TMP_Text[] texts = hud.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (IsPhaseF(texts[i].transform, hud.transform) || InsideScreen(texts[i].transform, hud.transform))
            {
                continue;
            }
            Check(texts[i], hud.transform, "HUD@" + at, false, 0f, problems);
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    static bool IsPhaseF(Transform text, Transform hud)
    {
        Transform top = text;
        while (top.parent != null && top.parent != hud)
        {
            top = top.parent;
        }
        return PhaseFHudTexts.Contains(top.name);
    }

    // PauseScreen and SettingsScreen under the HUD are checked as screens above.
    static bool InsideScreen(Transform text, Transform hud)
    {
        for (Transform walk = text; walk != null && walk != hud; walk = walk.parent)
        {
            if (walk.name == "PauseScreen" || walk.name == "SettingsScreen")
            {
                return true;
            }
        }
        return false;
    }
}
}
