using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class BeaconVisualTests
{
    const string PrefabPath = "Assets/Game/Prefabs/Gameplay/Beacon.prefab";
    const string FbxPath = "Assets/Game/Art/Beacons/Beacon.fbx";
    const string GlassMaterialPath = "Assets/Game/Art/Beacons/BeaconGlass.mat";
    const string BeamMaterialPath = "Assets/Game/Art/Beacons/BeaconBeam.mat";

    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Undo.ClearAll();
    }

    static Type VisualType()
    {
        Type type = Type.GetType("LanternKeeper.BeaconVisual, Assembly-CSharp");
        Assert.IsNotNull(type, "BeaconVisual type");
        return type;
    }

    static GameObject Prefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "Beacon prefab");
        return prefab;
    }

    [Test]
    public void PrefabUsesNewBeacon()
    {
        GameObject prefab = Prefab();
        Component visual = prefab.GetComponentInChildren(VisualType(), true);
        Assert.IsNotNull(visual, "BeaconVisual on the visual child");
        Assert.IsNotNull(prefab.GetComponent(Type.GetType("LanternKeeper.Beacon, Assembly-CSharp")), "Beacon logic still on the root");

        int fbxMeshes = 0;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (AssetDatabase.GetAssetPath(filter.sharedMesh) == FbxPath)
            {
                fbxMeshes++;
            }
        }

        Assert.GreaterOrEqual(fbxMeshes, 5, "cairn, iron, glass and beams (plus the LOD1 meshes) come from Beacon.fbx");
        Assert.IsNotNull(visual.GetComponentInParent<LODGroup>() ?? visual.GetComponent<LODGroup>(), "LOD group on the visual");

        bool glass = false;
        bool beam = false;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null || material.shader == null)
            {
                continue;
            }

            glass |= material.shader.name == "LanternKeeper/BeaconGlass";
            beam |= material.shader.name == "LanternKeeper/BeaconBeam";
        }

        Assert.IsTrue(glass, "glass slot uses LanternKeeper/BeaconGlass");
        Assert.IsTrue(beam, "beam cards use LanternKeeper/BeaconBeam");
    }

    [Test]
    public void ColliderUnchanged()
    {
        GameObject prefab = Prefab();
        Collider[] colliders = prefab.GetComponents<Collider>();
        Assert.AreEqual(1, colliders.Length, "one collider on the root");
        CapsuleCollider capsule = colliders[0] as CapsuleCollider;
        Assert.IsNotNull(capsule, "still a capsule");
        // The values from before the swap (git: Beacon.prefab at HEAD 156a187).
        Assert.AreEqual(0.22f, capsule.radius, 1e-5f, "radius");
        Assert.AreEqual(2.7f, capsule.height, 1e-5f, "height");
        Assert.AreEqual(1, capsule.direction, "direction");
        Assert.AreEqual(new Vector3(0f, 1.35f, 0f), capsule.center, "centre");
        Assert.IsFalse(capsule.isTrigger, "trigger");
        Assert.AreEqual(0, prefab.transform.Find("Visual").GetComponentsInChildren<Collider>(true).Length, "the visual adds no colliders");
    }

    [Test]
    public void LitColoursAreAmber()
    {
        Color amber = LookPalette.FromHex(LookPalette.LanternAmber);
        Color core = LookPalette.FromHex(LookPalette.GlowCore);
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Material beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
        Assert.IsNotNull(glass, "glass material");
        Assert.IsNotNull(beam, "beam material");
        AssertColour(amber, glass.GetColor("_AmberColor"), "pane amber");
        AssertColour(core, glass.GetColor("_CoreColor"), "pane core");
        AssertColour(amber, beam.GetColor("_BaseColor"), "beam amber");
        AssertColour(core, beam.GetColor("_CoreColor"), "beam core");
        Assert.IsTrue(glass.enableInstancing && beam.enableInstancing, "instancing on");
    }

    static void AssertColour(Color expected, Color actual, string what)
    {
        Assert.AreEqual(expected.r, actual.r, 1e-4f, what + " r");
        Assert.AreEqual(expected.g, actual.g, 1e-4f, what + " g");
        Assert.AreEqual(expected.b, actual.b, 1e-4f, what + " b");
    }

    [Test]
    public void NoLegacyPoleMesh()
    {
        GameObject prefab = Prefab();
        string[] legacy = { "Pole", "CageA", "CageB", "CageC", "CageD", "CageRing", "Cloth", "BandLow", "BandMid", "BandHigh", "Footing" };
        foreach (string name in legacy)
        {
            Assert.IsNull(prefab.transform.Find(name), "legacy child " + name);
        }

        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Transform t = filter.transform;
            bool underVisual = false;
            for (Transform walk = t; walk != null; walk = walk.parent)
            {
                underVisual |= walk.name == "Visual";
            }

            if (underVisual)
            {
                Assert.AreEqual(FbxPath, AssetDatabase.GetAssetPath(filter.sharedMesh), "mesh under Visual on " + filter.name);
            }
        }
    }

    [Test]
    public void LowPresetNumbers()
    {
        Type level = Type.GetType("LanternKeeper.GraphicsLevel, Assembly-CSharp");
        object low = Enum.Parse(level, "Low");
        object ultra = Enum.Parse(level, "Ultra");
        Type visual = VisualType();
        Assert.AreEqual(0.5f, (float)visual.GetMethod("BeamLengthScale").Invoke(null, new[] { low }), 1e-5f);
        Assert.AreEqual(0.6f, (float)visual.GetMethod("BeamIntensityScale").Invoke(null, new[] { low }), 1e-5f);
        Assert.AreEqual(15, (int)visual.GetMethod("EmberCount").Invoke(null, new[] { low }));
        Assert.AreEqual(1f, (float)visual.GetMethod("BeamLengthScale").Invoke(null, new[] { ultra }), 1e-5f);
        Assert.AreEqual(1f, (float)visual.GetMethod("BeamIntensityScale").Invoke(null, new[] { ultra }), 1e-5f);
        Assert.AreEqual(45, (int)visual.GetMethod("EmberCount").Invoke(null, new[] { ultra }));
    }

    [Test]
    public void FlickerStaysInRange()
    {
        MethodInfo flicker = VisualType().GetMethod("Flicker", BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i <= 10; i++)
        {
            float value = (float)flicker.Invoke(null, new object[] { i / 10f });
            Assert.GreaterOrEqual(value, 0.6f - 1e-5f);
            Assert.LessOrEqual(value, 1f + 1e-5f);
        }
    }
}
}
