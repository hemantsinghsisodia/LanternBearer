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

    [Test]
    public void RingVisibilityHugsWalkableGround()
    {
        Type ring = Type.GetType("LanternKeeper.BeaconSafeRing, Assembly-CSharp");
        Assert.IsNotNull(ring, "BeaconSafeRing type");
        MethodInfo visibility = ring.GetMethod("Visibility", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(visibility, "Visibility");
        System.Func<float, bool, float, float, float> vis = (g, on, b, w) => (float)visibility.Invoke(null, new object[] { g, on, b, w });
        Assert.AreEqual(1f, vis(10f, true, 10f, 2f), 1e-5f, "same height is fully visible");
        Assert.AreEqual(1f, vis(10.5f, true, 10f, 2f), 1e-5f, "small slope is fully visible");
        Assert.AreEqual(0f, vis(11.5f, true, 10f, 2f), 1e-5f, "a rise of more than 1 m is hidden");
        Assert.AreEqual(0f, vis(8f, true, 10f, 2f), 1e-5f, "a cliff drop is hidden");
        Assert.AreEqual(0f, vis(2.1f, true, 2.1f, 2f), 1e-5f, "below water plus margin is hidden");
        Assert.AreEqual(0f, vis(10f, false, 10f, 2f), 1e-5f, "off the terrain is hidden");
        float mid = vis(10.85f, true, 10f, 2f);
        Assert.Greater(mid, 0f);
        Assert.Less(mid, 1f);
    }

    [Test]
    public void TotalHeightAboutThreeMetres()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        Assert.IsNotNull(model);
        float lo = float.MaxValue;
        float hi = float.MinValue;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name.EndsWith("_L1") || filter.name == "BeaconBeams")
            {
                continue;
            }

            Bounds b = filter.sharedMesh.bounds;
            lo = Mathf.Min(lo, b.min.y);
            hi = Mathf.Max(hi, b.max.y);
        }

        Assert.GreaterOrEqual(hi - lo, 3.0f, "height");
        Assert.LessOrEqual(hi - lo, 3.3f, "height");
    }

    [Test]
    public void UnlitIronIsDark()
    {
        Material iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Beacons/BeaconIron.mat");
        Assert.IsNotNull(iron);
        Color c = iron.GetColor("_BaseColor");
        Assert.Less(c.r + c.g + c.b, 0.3f, "iron base colour is dark");
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Color g = glass.GetColor("_GlassColor");
        Assert.Less(g.r + g.g + g.b, 0.12f, "glass is near black");
    }
}
}
