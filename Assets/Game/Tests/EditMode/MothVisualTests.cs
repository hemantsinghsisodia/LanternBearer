using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class MothVisualTests
{
    const string PrefabPath = "Assets/Game/Prefabs/Gameplay/Moth.prefab";

    GameObject host;

    [TearDown]
    public void TearDown()
    {
        if (host != null)
        {
            UnityEngine.Object.DestroyImmediate(host);
            host = null;
        }

        Undo.ClearAll();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Undo.ClearAll();
    }

    static Type FxType()
    {
        Type type = Type.GetType("LanternKeeper.MothDrainFx, Assembly-CSharp");
        Assert.IsNotNull(type, "MothDrainFx type");
        return type;
    }

    static Type VisualType()
    {
        Type type = Type.GetType("LanternKeeper.MothVisual, Assembly-CSharp");
        Assert.IsNotNull(type, "MothVisual type");
        return type;
    }

    Component NewFx()
    {
        host = new GameObject("FxHost");
        return host.AddComponent(FxType());
    }

    static object Get(Component fx, string name)
    {
        PropertyInfo property = FxType().GetProperty(name);
        Assert.IsNotNull(property, name);
        return property.GetValue(fx);
    }

    static void Call(Component fx, string name, params object[] args)
    {
        MethodInfo method = FxType().GetMethod(name);
        Assert.IsNotNull(method, name);
        method.Invoke(fx, args);
    }

    [Test]
    public void PrefabUsesNewMoth()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab);
        Component visual = prefab.GetComponentInChildren(VisualType(), true);
        Assert.IsNotNull(visual, "MothVisual component on the visual child");

        bool wingMaterial = false;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            Assert.IsFalse(string.IsNullOrEmpty(path) || path.StartsWith("Library/"), "legacy built-in mesh on " + filter.name);
            Assert.AreNotEqual("Sphere", filter.sharedMesh.name);
            Assert.AreNotEqual("Quad", filter.sharedMesh.name);
            Assert.AreNotEqual("Capsule", filter.sharedMesh.name);
        }

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material != null && material.shader != null && material.shader.name == "LanternKeeper/MothWing")
            {
                wingMaterial = true;
            }
        }

        Assert.IsTrue(wingMaterial, "LanternKeeper/MothWing material");
    }

    [Test]
    public void AuraIsDrainViolet()
    {
        Component fx = NewFx();
        Call(fx, "SetDraining", true);
        Call(fx, "Tick", 0.3f);
        Color aura = (Color)Get(fx, "AuraColor");
        Color violet = LookPalette.FromHex(LookPalette.DrainViolet);
        Assert.AreEqual(violet.r, aura.r, 0.002f);
        Assert.AreEqual(violet.g, aura.g, 0.002f);
        Assert.AreEqual(violet.b, aura.b, 0.002f);
        Assert.Greater(aura.a, 0.1f);
        Assert.AreEqual("B48CFF", LookPalette.ToHex(violet));
    }

    [Test]
    public void AuraFadesWhenDrainStops()
    {
        Component fx = NewFx();
        Call(fx, "SetDraining", true);
        Call(fx, "Tick", 0.05f);
        Assert.Greater((float)Get(fx, "Amount"), 0f);
        Assert.IsTrue((bool)Get(fx, "DustEmitting"), "dust emits while draining");
        Call(fx, "SetDraining", false);
        Call(fx, "Tick", 0.3f);
        Assert.AreEqual(0f, (float)Get(fx, "Amount"), 0.0001f);
        Assert.IsFalse((bool)Get(fx, "DustEmitting"), "dust stops when draining stops");
    }

    // Controller ruling: the moth is about 0.40 m across (0.38-0.42 m) so it reads at gameplay distance.
    [Test]
    public void WingspanMatchesRuling()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Creatures/Moth/Moth.fbx");
        Assert.IsNotNull(model);
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            Bounds b = filter.sharedMesh.bounds;
            if (first)
            {
                bounds = b;
                first = false;
            }
            else
            {
                bounds.Encapsulate(b);
            }
        }

        Assert.That(bounds.size.x, Is.InRange(0.38f, 0.42f));
    }

    [Test]
    public void MothCollidersUnchanged()
    {
        // The original Moth.prefab carries no collider (the moth steers around obstacles by overlap tests), so the
        // modelled visual must not add one: the set of colliders is the same empty set as before.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
        Assert.AreEqual(0, colliders.Length);
    }
}
}
