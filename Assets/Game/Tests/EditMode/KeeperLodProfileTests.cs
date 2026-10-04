using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class KeeperLodProfileTests
{
    const string GraphicsPath = "Assets/Game/Settings/Graphics/GraphicsProfile_";

    static int LodOf(string name)
    {
        ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GraphicsPath + name + ".asset");
        Assert.IsNotNull(profile, "missing GraphicsProfile_" + name);
        SerializedProperty prop = new SerializedObject(profile).FindProperty("keeperLod");
        Assert.IsNotNull(prop, name + " has no keeperLod");
        return prop.intValue;
    }

    [Test]
    public void KeeperLodPerPreset()
    {
        Assert.AreEqual(1, LodOf("Low"), "Low keeps the lighter keeper");
        Assert.AreEqual(1, LodOf("Medium"), "Medium keeps the lighter keeper");
        Assert.AreEqual(0, LodOf("High"), "High shows the full keeper");
        Assert.AreEqual(0, LodOf("Ultra"), "Ultra shows the full keeper");
    }
}
}
