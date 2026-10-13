using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// The main menu roofs count beacons lit (LanternKeeperLit_<id>), not Keeper's Log pages read. Game types are reached by reflection.
public class GameSettingsLitTests
{
    static readonly string[] Keys =
    {
        "LanternKeeperLit_island5", "LanternKeeperLit_island1",
        "LanternKeeperWon_island1", "LanternKeeperWon_island4", "LanternKeeperWon_island5",
        "LanternKeeperLog_island1", "LanternKeeperLog_island5"
    };

    readonly Dictionary<string, int> saved = new Dictionary<string, int>();
    readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        saved.Clear();
        foreach (string key in Keys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                saved[key] = PlayerPrefs.GetInt(key);
            }
            PlayerPrefs.DeleteKey(key);
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string key in Keys)
        {
            int value;
            if (saved.TryGetValue(key, out value))
            {
                PlayerPrefs.SetInt(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
        PlayerPrefs.Save();
        foreach (UnityEngine.Object o in made)
        {
            if (o != null)
            {
                UnityEngine.Object.DestroyImmediate(o);
            }
        }
        made.Clear();
    }

    static Type Settings()
    {
        Type t = Type.GetType("LanternKeeper.GameSettings, Assembly-CSharp");
        Assert.IsNotNull(t);
        return t;
    }

    static object Call(string method, params object[] args)
    {
        return Settings().GetMethod(method).Invoke(null, args);
    }

    // A MainMenu whose levels are the given (id, beacons) pairs.
    object InfoFor(int index, params object[] idsAndBeacons)
    {
        Type levelType = Type.GetType("LanternKeeper.LevelConfig, Assembly-CSharp");
        Type menuType = Type.GetType("LanternKeeper.MainMenu, Assembly-CSharp");
        Assert.IsNotNull(levelType);
        Assert.IsNotNull(menuType);
        int count = idsAndBeacons.Length / 2;
        Array levels = Array.CreateInstance(levelType, count);
        for (int i = 0; i < count; i++)
        {
            ScriptableObject level = ScriptableObject.CreateInstance(levelType);
            made.Add(level);
            levelType.GetField("levelId").SetValue(level, (string)idsAndBeacons[i * 2]);
            levelType.GetField("beaconCount").SetValue(level, (int)idsAndBeacons[i * 2 + 1]);
            levels.SetValue(level, i);
        }
        GameObject go = new GameObject("MenuForLitTest");
        made.Add(go);
        Component menu = go.AddComponent(menuType);
        SerializedObject so = new SerializedObject(menu);
        SerializedProperty prop = so.FindProperty("levels");
        prop.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = (UnityEngine.Object)levels.GetValue(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return menuType.GetMethod("Info").Invoke(menu, new object[] { index });
    }

    static int Field(object info, string name)
    {
        return (int)info.GetType().GetField(name).GetValue(info);
    }

    [Test]
    public void RecordLitKeepsMax()
    {
        Assert.AreEqual(0, Call("GetLitCount", "island5"));
        Call("RecordLit", "island5", 4);
        Assert.AreEqual(4, Call("GetLitCount", "island5"));
        Call("RecordLit", "island5", 2);
        Assert.AreEqual(4, Call("GetLitCount", "island5"), "a worse run never lowers it");
        Call("RecordLit", "island5", 6);
        Assert.AreEqual(6, Call("GetLitCount", "island5"));
        Assert.AreEqual(6, PlayerPrefs.GetInt("LanternKeeperLit_island5"));
    }

    [Test]
    public void WonIslandShowsAllRoofs()
    {
        PlayerPrefs.SetInt("LanternKeeperWon_island5", 1);
        Call("RecordLogRead", "island5", 3);
        object info = InfoFor(0, "island5", 8);
        Assert.AreEqual(8, Field(info, "lit"), "a won island shows every roof");
        Assert.AreEqual(3, Field(info, "found"), "log pages stay as they were");
    }

    [Test]
    public void PartialRunShowsLitNotLog()
    {
        Call("RecordLogRead", "island5", 3);
        Call("RecordLit", "island5", 5);
        object info = InfoFor(0, "island5", 8);
        Assert.AreEqual(5, Field(info, "lit"), "lit beacons, not log pages");
        Call("RecordLit", "island5", 20);
        Assert.AreEqual(8, Field(InfoFor(0, "island5", 8), "lit"), "capped at the beacon count");
    }
}
}
