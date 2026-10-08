using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LanternKeeper.Tests
{
public class FireflySwarmTests
{
    const string PrefabPath = "Assets/Game/Prefabs/Gameplay/Firefly.prefab";
    const string MaterialPath = "Assets/Game/Materials/Generated/FireflyMote.mat";

    GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            root = null;
        }

        Undo.ClearAll();
    }

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type");
        return type;
    }

    Component Swarm()
    {
        root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Assert.IsNotNull(root, "Firefly prefab");
        Component swarm = root.GetComponentInChildren(GameType("FireflySwarm"), true);
        Assert.IsNotNull(swarm, "FireflySwarm in the prefab");
        return swarm;
    }

    [Test]
    public void PrefabHasSwarmAndNoPlaceholder()
    {
        Component swarm = Swarm();
        int found = 0;
        for (int i = 0; i < 6; i++)
        {
            if (swarm.transform.Find("Mote" + i) != null)
            {
                found++;
            }
        }

        Assert.AreEqual(6, found, "six motes");
        Assert.AreEqual(6, swarm.transform.childCount);
        Assert.IsNull(root.transform.Find("Body"), "no Body");
        Assert.IsNull(root.transform.Find("PickupBurst"), "no PickupBurst");
        Assert.IsNotNull(root.transform.Find("Glow").GetComponent<Light>(), "Glow light");
        Assert.IsNotNull(root.GetComponent(GameType("Firefly")), "Firefly component");
    }

    [Test]
    public void MoteColourIsFireflyGreen()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Assert.IsNotNull(material, "FireflyMote material");
        Assert.AreEqual(LookPalette.FromHex(LookPalette.FireflyGreen), material.GetColor("_Color"));
    }

    [Test]
    public void MoteMaterialInstancedNoShadows()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Assert.IsTrue(material.enableInstancing, "instancing");
        Assert.AreEqual(-1, material.FindPass("ShadowCaster"), "no shadow caster pass");
        Component swarm = Swarm();
        for (int i = 0; i < 6; i++)
        {
            MeshRenderer renderer = swarm.transform.Find("Mote" + i).GetComponent<MeshRenderer>();
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, "mote " + i);
            Assert.AreSame(material, renderer.sharedMaterial, "mote " + i);
        }
    }

    [Test]
    public void SwarmPushesTheDriftValuesDriftUses()
    {
        Component swarm = Swarm();
        const float seed = 0.37f;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = swarm.GetType();
        type.GetField("seed01", flags).SetValue(swarm, seed);
        type.GetField("block", flags).SetValue(swarm, new MaterialPropertyBlock());
        type.GetMethod("Apply", flags).Invoke(swarm, null);
        MaterialPropertyBlock read = new MaterialPropertyBlock();
        for (int i = 0; i < 6; i++)
        {
            swarm.transform.Find("Mote" + i).GetComponent<Renderer>().GetPropertyBlock(read);
            Vector4 freq = read.GetVector("_DriftFreq");
            Vector4 phase = read.GetVector("_DriftPhase");
            Vector3 expectedFreq = FireflyCurve.DriftFreq(seed, i);
            Vector3 expectedPhase = FireflyCurve.DriftPhase(seed, i);
            Assert.AreEqual(expectedFreq.x, freq.x, 1e-6f, "freq x " + i);
            Assert.AreEqual(expectedFreq.y, freq.y, 1e-6f, "freq y " + i);
            Assert.AreEqual(expectedFreq.z, freq.z, 1e-6f, "freq z " + i);
            Assert.AreEqual(expectedPhase.x, phase.x, 1e-6f, "phase x " + i);
            Assert.AreEqual(expectedPhase.y, phase.y, 1e-6f, "phase y " + i);
            Assert.AreEqual(expectedPhase.z, phase.z, 1e-6f, "phase z " + i);
            Vector3 fromBlock = FireflyCurve.Drift(new Vector3(freq.x, freq.y, freq.z), new Vector3(phase.x, phase.y, phase.z), 1.7f, 0f);
            Assert.AreEqual(0f, Vector3.Distance(fromBlock, FireflyCurve.Drift(seed, i, 1.7f, 0f)), 1e-5f, "Drift uses the pushed values " + i);
        }
    }

    [Test]
    public void InstallFireflyIsIdempotent()
    {
        Type installer = Type.GetType("LanternKeeper.CreatureInstaller, Assembly-CSharp-Editor");
        Assert.IsNotNull(installer, "CreatureInstaller type");
        MethodInfo install = installer.GetMethod("InstallFirefly", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(install, "InstallFirefly");
        install.Invoke(null, null);
        byte[] first = File.ReadAllBytes(PrefabPath);
        install.Invoke(null, null);
        byte[] second = File.ReadAllBytes(PrefabPath);
        CollectionAssert.AreEqual(first, second);
    }
}
}
