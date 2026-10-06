using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class KeepersLogScreenTests
{
    const string LogPath = "Assets/Game/Prefabs/UI/KeepersLogScreen.prefab";
    const string ScaleKey = "LanternKeeperTextScale";

    GameObject canvasGo;
    bool hadScale;
    float savedScale;

    [SetUp]
    public void SetUp()
    {
        hadScale = PlayerPrefs.HasKey(ScaleKey);
        savedScale = PlayerPrefs.GetFloat(ScaleKey, 1f);
        PlayerPrefs.DeleteKey(ScaleKey);
        canvasGo = new GameObject("LogTestCanvas", typeof(RectTransform), typeof(Canvas));
        ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(1920f, 1080f);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(canvasGo);
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

    static string LongEntry(int words)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        for (int i = 0; i < words; i++)
        {
            b.Append("beacon").Append(i % 7).Append(' ');
        }
        return b.ToString().Trim();
    }

    // Builds IslandLog[] through reflection (the type lives in Assembly-CSharp).
    static object Islands(Type logType, int count, int found, string[] entries)
    {
        Array array = Array.CreateInstance(logType, count);
        for (int i = 0; i < count; i++)
        {
            object item = Activator.CreateInstance(logType);
            logType.GetField("header").SetValue(item, "Island " + (i + 1) + " · Test");
            logType.GetField("entries").SetValue(item, entries);
            logType.GetField("found").SetValue(item, found);
            array.SetValue(item, i);
        }
        return array;
    }

    int SpreadsAt(float scale, out bool allFit)
    {
        UserSettings.TextScale = scale;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LogPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
        Type screenType = Type.GetType("LanternKeeper.KeepersLogScreen, Assembly-CSharp");
        Type logType = Type.GetType("LanternKeeper.IslandLog, Assembly-CSharp");
        Component screen = instance.GetComponent(screenType);
        instance.GetComponent<TextScaler>().Reapply();
        string[] entries = { LongEntry(160), LongEntry(220), LongEntry(100), LongEntry(180), LongEntry(130) };
        screenType.GetMethod("Populate").Invoke(screen, new[] { Islands(logType, 2, 5, entries) });
        int count = (int)screenType.GetProperty("SpreadCount").GetValue(screen);

        // Every page must fit its text box at this scale.
        ParchmentSpread spread = instance.GetComponentInChildren<ParchmentSpread>(true);
        float height = ((RectTransform)spread.LeftBody.transform).rect.height;
        float width = ((RectTransform)spread.LeftBody.transform).rect.width;
        allFit = true;
        MethodInfo turn = screenType.GetMethod("TurnTo");
        for (int i = 0; i < count; i++)
        {
            turn.Invoke(screen, new object[] { i });
            foreach (TMP_Text body in new[] { spread.LeftBody, spread.RightBody })
            {
                float h = body.GetPreferredValues(body.text, width, 0f).y;
                allFit = allFit && h <= height + 1f;
            }
        }
        UnityEngine.Object.DestroyImmediate(instance);
        return count;
    }

    [Test]
    public void LargerTextMakesMorePagesAndNeverOverflows()
    {
        bool fitSmall;
        bool fitLarge;
        int small = SpreadsAt(1.0f, out fitSmall);
        int large = SpreadsAt(1.3f, out fitLarge);
        Assert.IsTrue(fitSmall, "pages fit at 100%");
        Assert.IsTrue(fitLarge, "pages fit at 130%");
        Assert.GreaterOrEqual(small, 4, "two islands, each over one spread");
        Assert.Greater(large, small, "130% text paginates onto more spreads");
    }

    [Test]
    public void EachIslandStartsOnItsOwnSpread()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LogPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
        Type screenType = Type.GetType("LanternKeeper.KeepersLogScreen, Assembly-CSharp");
        Type logType = Type.GetType("LanternKeeper.IslandLog, Assembly-CSharp");
        Component screen = instance.GetComponent(screenType);
        screenType.GetMethod("Populate").Invoke(screen, new[] { Islands(logType, 4, 0, new[] { "a", "b", "c" }) });
        Assert.AreEqual(4, (int)screenType.GetProperty("SpreadCount").GetValue(screen), "short islands get one spread each");
        ParchmentSpread spread = instance.GetComponentInChildren<ParchmentSpread>(true);
        Assert.AreEqual("Island 1 · Test", spread.LeftTitle.text);
        Assert.AreEqual("pages 0 of 3", spread.LeftSub.text);
        StringAssert.Contains(LogPaging.TornPage, spread.LeftBody.text);
        screenType.GetMethod("TurnTo").Invoke(screen, new object[] { 2 });
        Assert.AreEqual("Island 3 · Test", spread.LeftTitle.text);
        Assert.AreEqual(2, (int)screenType.GetProperty("SpreadIndex").GetValue(screen));
        screenType.GetMethod("TurnTo").Invoke(screen, new object[] { 99 });
        Assert.AreEqual(3, (int)screenType.GetProperty("SpreadIndex").GetValue(screen), "clamped to the last spread");
        UnityEngine.Object.DestroyImmediate(instance);
    }
}
}
