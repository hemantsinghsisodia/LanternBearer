using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// "Build Island 5" wipes the bridges, rings and music director, so BuildIsland5 must run the installers afterwards (bridges before rings).
// "Build All Levels" must reach the same entry for Island 5.
public class Island5BuildChainTests
{
    static Type Builder()
    {
        Type builder = Type.GetType("LanternKeeper.IslandBuilder, Assembly-CSharp-Editor");
        Assert.IsNotNull(builder, "IslandBuilder type");
        return builder;
    }

    [Test]
    public void Island5PostStepsOrder()
    {
        FieldInfo field = Builder().GetField("Island5PostSteps", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "IslandBuilder.Island5PostSteps");
        var steps = (ValueTuple<string, Action>[])field.GetValue(null);
        Assert.AreEqual(9, steps.Length);
        Assert.AreEqual("BridgeInstaller", steps[0].Item1);
        Assert.AreEqual("RingInstaller", steps[1].Item1);
        Assert.AreEqual("LighthouseInstaller", steps[2].Item1);
        Assert.AreEqual("HorizonLighthouseInstaller", steps[3].Item1);
        Assert.AreEqual("Island5PerfInstaller", steps[4].Item1);
        Assert.AreEqual("FinaleInstaller", steps[5].Item1);
        Assert.AreEqual("StormSurgeInstaller", steps[6].Item1);
        Assert.AreEqual("LightningHunterInstaller", steps[7].Item1);
        Assert.AreEqual("AudioInstaller", steps[8].Item1);
        for (int i = 0; i < steps.Length; i++)
        {
            Assert.IsNotNull(steps[i].Item2, steps[i].Item1);
        }
    }

    [Test]
    public void BuildAllBuildsIsland5ThroughBuildIsland5()
    {
        MethodInfo pick = Builder().GetMethod("BuildActionFor", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(pick, "IslandBuilder.BuildActionFor (the per-level entry BuildAll runs)");
        Action island5 = (Action)pick.Invoke(null, new object[] { "Island5" });
        Assert.AreEqual("BuildIsland5", island5.Method.Name, "Island 5 must get its post steps in Build All Levels");
        for (int i = 1; i <= 4; i++)
        {
            Action other = (Action)pick.Invoke(null, new object[] { "Island" + i });
            Assert.AreNotEqual("BuildIsland5", other.Method.Name, "Island " + i + " keeps the plain Build");
        }
    }

    [Test]
    public void MusicLibraryHasTheIsland5Hush()
    {
        // MusicLibrary lives in Assembly-CSharp, which this assembly can't reference: read it through SerializedObject.
        ScriptableObject library = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Resources/MusicLibrary.asset");
        Assert.IsNotNull(library);
        SerializedProperty islands = new SerializedObject(library).FindProperty("islands");
        SerializedProperty entry = null;
        for (int i = 0; i < islands.arraySize; i++)
        {
            if (islands.GetArrayElementAtIndex(i).FindPropertyRelative("scene").stringValue == "Island5")
            {
                entry = islands.GetArrayElementAtIndex(i);
            }
        }

        Assert.IsNotNull(entry, "Island5 entry");
        UnityEngine.Object hush = entry.FindPropertyRelative("hush").objectReferenceValue;
        Assert.IsNotNull(hush, "Island5 hush clip");
        Assert.AreEqual("Hush_Island5_1", hush.name);
    }

    [Test]
    public void SummitHushPlaysAtSixTenthsOfTheMusicVolume()
    {
        Type director = Type.GetType("LanternKeeper.MusicDirector, Assembly-CSharp");
        Assert.IsNotNull(director, "MusicDirector type");
        FieldInfo level = director.GetField("HushLevel", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(level, "MusicDirector.HushLevel");
        Assert.AreEqual(0.6f, (float)level.GetRawConstantValue(), 1e-6f);
    }
}
}
