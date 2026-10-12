using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// The Island 5 lighthouse model (ArtSource/Blender/lighthouse_build.py): its object names (the installer and GreatLamp look them up),
// the triangle budget and the height, the door side, and the stone colour source. Scene-level checks are in LighthouseInstallTests.
public class LighthouseModelTests
{
    const string FbxPath = "Assets/Game/Art/Lighthouse/Lighthouse.fbx";

    static GameObject Model()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        Assert.IsNotNull(model, "Lighthouse FBX at " + FbxPath);
        return model;
    }

    static Type Installer()
    {
        Type type = Type.GetType("LanternKeeper.LighthouseInstaller, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "LighthouseInstaller type");
        return type;
    }


    [Test]
    public void LighthouseFbxHasSlots()
    {
        GameObject model = Model();
        string[] names =
        {
            "LighthouseStone", "LighthouseIron", "BeaconGlass", "LighthouseStone_L1", "LighthouseIron_L1", "FlameRoot", "LighthouseDoor", "LighthouseDoorGlow"
        };
        for (int i = 0; i < names.Length; i++)
        {
            Assert.IsNotNull(Find(model.transform, names[i]), "child " + names[i]);
        }

        int triangles = 0;
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name.EndsWith("_L1"))
            {
                continue;
            }

            triangles += filter.sharedMesh.triangles.Length / 3;
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

        Assert.LessOrEqual(triangles, 6000, "LOD0 triangles");
        Assert.AreEqual(14f, bounds.size.y, 0.5f, "height");
        Assert.AreEqual(0f, bounds.min.y, 0.05f, "the origin is the base on the ground");

        int lod1 = 0;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name.EndsWith("_L1") || filter.name == "BeaconGlass" || filter.name.StartsWith("LighthouseDoor"))
            {
                lod1 += filter.sharedMesh.triangles.Length / 3;
            }
        }

        Assert.LessOrEqual(lod1, 1500, "LOD1 triangles");
        Assert.AreEqual(11.9f, Find(model.transform, "FlameRoot").localPosition.y, 0.05f, "FlameRoot height");
    }

    // The installer turns the lighthouse so the model +Z (the door) looks at the end of the summit span.
    [Test]
    public void DoorRecessFacesModelPlusZ()
    {
        Vector3 centre = Find(Model().transform, "LighthouseDoor").GetComponent<MeshFilter>().sharedMesh.bounds.center;
        Assert.Greater(centre.z, 1.5f, "the door planks sit on the +Z side of the tower");
        Assert.Less(Mathf.Abs(centre.x), 0.3f);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}
}
