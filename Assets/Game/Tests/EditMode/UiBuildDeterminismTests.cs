using System;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// Build UI must not churn the prefab files (PrefabIdStabilizer), and TextScaler must scale texts the moment they are made.
public class UiBuildDeterminismTests
{
    const string ScaleKey = "LanternKeeperTextScale";

    // A two-object prefab as Unity writes it, with the fileIDs the first build happened to get.
    const string Previous =
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
        "--- !u!1 &111\nGameObject:\n  m_Component:\n  - component: {fileID: 112}\n  - component: {fileID: 113}\n  m_Name: Root\n" +
        "--- !u!224 &112\nRectTransform:\n  m_GameObject: {fileID: 111}\n  m_Children:\n  - {fileID: 122}\n  m_Father: {fileID: 0}\n" +
        "--- !u!114 &113\nMonoBehaviour:\n  m_GameObject: {fileID: 111}\n  m_Script: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}\n  target: {fileID: 121}\n" +
        "--- !u!1 &121\nGameObject:\n  m_Component:\n  - component: {fileID: 122}\n  m_Name: Child\n" +
        "--- !u!224 &122\nRectTransform:\n  m_GameObject: {fileID: 121}\n  m_Children: []\n  m_Father: {fileID: 112}\n";

    // The same prefab rebuilt: every fileID is different (and the child was saved first).
    const string Rebuilt =
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
        "--- !u!1 &9001\nGameObject:\n  m_Component:\n  - component: {fileID: 9002}\n  - component: {fileID: 9003}\n  m_Name: Root\n" +
        "--- !u!224 &9002\nRectTransform:\n  m_GameObject: {fileID: 9001}\n  m_Children:\n  - {fileID: 9012}\n  m_Father: {fileID: 0}\n" +
        "--- !u!114 &9003\nMonoBehaviour:\n  m_GameObject: {fileID: 9001}\n  m_Script: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}\n  target: {fileID: 9011}\n" +
        "--- !u!1 &9011\nGameObject:\n  m_Component:\n  - component: {fileID: 9012}\n  m_Name: Child\n" +
        "--- !u!224 &9012\nRectTransform:\n  m_GameObject: {fileID: 9011}\n  m_Children: []\n  m_Father: {fileID: 9002}\n";

    GameObject canvas;
    bool hadScale;
    float savedScale;

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
        if (canvas != null)
        {
            UnityEngine.Object.DestroyImmediate(canvas);
            canvas = null;
        }
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

    static string Normalize(string fresh, string previous)
    {
        Type type = Type.GetType("LanternKeeper.PrefabIdStabilizer, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "PrefabIdStabilizer");
        return (string)type.GetMethod("Normalize").Invoke(null, new object[] { fresh, previous });
    }

    [Test]
    public void RebuildWithNewFileIdsKeepsThePreviousText()
    {
        Assert.AreEqual(Previous, Normalize(Rebuilt, Previous), "an unchanged rebuild is byte-identical to the last build");
    }

    [Test]
    public void NormalizingTwiceChangesNothing()
    {
        string once = Normalize(Rebuilt, null);
        Assert.AreEqual(once, Normalize(Rebuilt, once));
        Assert.AreEqual(once, Normalize(once, once));
    }

    [Test]
    public void FirstBuildGetsFileIdsFromTheHierarchyNotFromChance()
    {
        // Two different "random" results for the same hierarchy normalise to the same text.
        string other = Rebuilt.Replace("9001", "7001").Replace("9002", "7002").Replace("9003", "7003").Replace("9011", "7011").Replace("9012", "7012");
        Assert.AreEqual(Normalize(Rebuilt, null), Normalize(other, null));
    }

    [Test]
    public void NewObjectsAreAddedWithoutRenumberingTheOthers()
    {
        string grown = Rebuilt + "--- !u!1 &9021\nGameObject:\n  m_Component:\n  - component: {fileID: 9022}\n  m_Name: Extra\n" +
            "--- !u!224 &9022\nRectTransform:\n  m_GameObject: {fileID: 9021}\n  m_Children: []\n  m_Father: {fileID: 9002}\n";
        grown = grown.Replace("  m_Children:\n  - {fileID: 9012}\n", "  m_Children:\n  - {fileID: 9012}\n  - {fileID: 9022}\n");
        string result = Normalize(grown, Previous);
        StringAssert.Contains("--- !u!1 &111\n", result);
        StringAssert.Contains("--- !u!224 &122\n", result);
        StringAssert.Contains("m_Name: Extra", result);
        StringAssert.DoesNotContain("9021", result);
    }

    [Test]
    public void InsertingASiblingBeforeKeepsTheExistingSiblingsFileIds()
    {
        // "Extra" is saved into slot 0, so Child moves from child index 0 to 1. Child must keep its old fileIDs.
        string grown = Rebuilt + "--- !u!1 &9021\nGameObject:\n  m_Component:\n  - component: {fileID: 9022}\n  m_Name: Extra\n" +
            "--- !u!224 &9022\nRectTransform:\n  m_GameObject: {fileID: 9021}\n  m_Children: []\n  m_Father: {fileID: 9002}\n";
        grown = grown.Replace("  m_Children:\n  - {fileID: 9012}\n", "  m_Children:\n  - {fileID: 9022}\n  - {fileID: 9012}\n");
        string result = Normalize(grown, Previous);
        StringAssert.Contains("--- !u!1 &121\n", result, "Child GameObject keeps its fileID");
        StringAssert.Contains("--- !u!224 &122\n", result, "Child transform keeps its fileID");
        StringAssert.Contains("m_Father: {fileID: 112}", result);
        StringAssert.DoesNotContain("9021", result);
    }

    // ---------- TextScaler ----------

    TextScaler MakeScaler()
    {
        canvas = new GameObject("ScalerCanvas", typeof(RectTransform), typeof(Canvas));
        TextScaler scaler = canvas.AddComponent<TextScaler>();
        scaler.ScalePlainText = true;
        return scaler;
    }

    static TextMeshProUGUI MakeText(Transform parent, float size)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        return text;
    }

    [Test]
    public void NewTextsAreScaledWhenTheyAreMade()
    {
        UserSettings.TextScale = 1.3f;
        TextScaler scaler = MakeScaler();
        TextMeshProUGUI text = MakeText(canvas.transform, 18f);
        Assert.AreEqual(18f, text.fontSize, 0.001f, "made at its authored size");
        TextScaler.Notify(text);
        Assert.AreEqual(18f * 1.3f, text.fontSize, 0.01f, "scaled at once, not on the next rescan");
        scaler.Reapply();
        Assert.AreEqual(18f * 1.3f, text.fontSize, 0.01f, "a rescan does not scale it twice");
    }

    [Test]
    public void ASizeSetLaterBecomesTheNewBase()
    {
        UserSettings.TextScale = 1.3f;
        TextScaler scaler = MakeScaler();
        TextMeshProUGUI text = MakeText(canvas.transform, 20f);
        scaler.Reapply();
        Assert.AreEqual(26f, text.fontSize, 0.01f);

        text.fontSize = 30f; // code changes the size after the scaler has seen the text
        scaler.Reapply();
        Assert.AreEqual(39f, text.fontSize, 0.01f, "30 is the new base, scaled once");

        UserSettings.TextScale = 1f;
        scaler.Reapply();
        Assert.AreEqual(30f, text.fontSize, 0.01f, "back at 100% it is the new base, not the stale 20");
    }

    [Test]
    public void TextsOutsideAScalerAreLeftAlone()
    {
        UserSettings.TextScale = 1.3f;
        GameObject loose = new GameObject("Loose", typeof(RectTransform));
        try
        {
            TextMeshProUGUI text = MakeText(loose.transform, 18f);
            TextScaler.Notify(text);
            Assert.AreEqual(18f, text.fontSize, 0.001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(loose);
        }
    }
}
}
