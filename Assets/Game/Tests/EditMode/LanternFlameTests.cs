using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
// LanternFlame lives in the game assembly, which the test assembly cannot reference, so it is driven by reflection,
// the same way KeeperCloseupCapture drives it in edit mode.
public class LanternFlameTests
{
    GameObject root;
    Component flame;
    Transform flameTransform;

    [SetUp]
    public void SetUp()
    {
        Type type = FindType("LanternKeeper.LanternFlame");
        Assert.IsNotNull(type, "LanternFlame type is missing");
        root = new GameObject("LanternFlameTest");
        root.AddComponent(FindType("LanternKeeper.Lantern"));
        GameObject flameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flameObject.transform.SetParent(root.transform, false);
        flameObject.transform.localScale = Vector3.one;
        flameTransform = flameObject.transform;
        flame = root.AddComponent(type);
        SetField("flame", flameTransform);
        SetField("flameRenderer", flameObject.GetComponent<Renderer>());
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
    }

    [Test]
    public void HeightFollowsFuel()
    {
        Apply(0f);
        Assert.AreEqual(0.45f, flameTransform.localScale.y, 1e-4f);
        Apply(1f);
        Assert.AreEqual(1f, flameTransform.localScale.y, 1e-4f);
    }

    [Test]
    public void RepeatedApplyDoesNotCompound()
    {
        Apply(0.3f);
        float first = flameTransform.localScale.y;
        Apply(0.3f);
        Assert.AreEqual(first, flameTransform.localScale.y, 1e-6f);
        Assert.AreEqual(LanternFlameMapping.Height(0.3f), first, 1e-4f);
    }

    [Test]
    public void WorksWithoutLanternOrRenderers()
    {
        SetField("lantern", null);
        SetField("flameRenderer", null);
        SetField("glassRenderer", null);
        Assert.DoesNotThrow(() => Apply(0.5f));
    }

    void Apply(float fuel)
    {
        MethodInfo method = flame.GetType().GetMethod("ApplyFuel", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "ApplyFuel is missing");
        method.Invoke(flame, new object[] { fuel });
    }

    void SetField(string name, object value)
    {
        FieldInfo field = flame.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, name + " field is missing");
        field.SetValue(flame, value);
    }

    static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
}
