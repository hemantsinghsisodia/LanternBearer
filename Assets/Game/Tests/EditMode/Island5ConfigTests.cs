using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// IslandBuilder.CreateIsland5Config is the single source for Island 5's scarce-fuel tuning (LightningHunterInstaller copies it into the
// scene's LevelTuning). It must keep writing what the shipped Island5.asset says, or a rebuild silently changes the balance.
public class Island5ConfigTests
{
    const string AssetPath = "Assets/Game/Levels/Island5.asset";

    [Test]
    public void BuilderConfigMatchesTheShippedIsland5Asset()
    {
        Type builder = Type.GetType("LanternKeeper.IslandBuilder, Assembly-CSharp-Editor");
        Assert.IsNotNull(builder, "IslandBuilder type");
        MethodInfo create = builder.GetMethod("CreateIsland5Config", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(create, "IslandBuilder.CreateIsland5Config");
        ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetPath);
        Assert.IsNotNull(asset, AssetPath);
        SerializedObject saved = new SerializedObject(asset);
        float savedDrain = saved.FindProperty("drainMultiplier").floatValue;
        float savedRefund = saved.FindProperty("beaconRefund01").floatValue;
        try
        {
            ScriptableObject made = (ScriptableObject)create.Invoke(null, null);
            SerializedObject built = new SerializedObject(made);
            Assert.AreEqual(1.6f, built.FindProperty("drainMultiplier").floatValue, 1e-4f, "the builder's drain multiplier");
            Assert.AreEqual(0.2f, built.FindProperty("beaconRefund01").floatValue, 1e-4f, "the builder's beacon refund");
            Assert.AreEqual(savedDrain, built.FindProperty("drainMultiplier").floatValue, 1e-4f, "Island5.asset drain multiplier");
            Assert.AreEqual(savedRefund, built.FindProperty("beaconRefund01").floatValue, 1e-4f, "Island5.asset beacon refund");
        }
        finally
        {
            // A drifted builder must not leave the asset changed behind a failing test.
            SerializedObject restore = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetPath));
            restore.FindProperty("drainMultiplier").floatValue = savedDrain;
            restore.FindProperty("beaconRefund01").floatValue = savedRefund;
            restore.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
}
