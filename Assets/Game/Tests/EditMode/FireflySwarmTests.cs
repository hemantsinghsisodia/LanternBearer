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
