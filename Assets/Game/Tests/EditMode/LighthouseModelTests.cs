using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
// The Island 5 lighthouse model (ArtSource/Blender/lighthouse_build.py): its object names (the installer and GreatLamp look them up),
// the triangle budget and the height; and that the lamp prompt is reachable from the summit plateau.
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

    static float Const(string name)
    {
        FieldInfo field = Installer().GetField(name, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "LighthouseInstaller." + name);
        return (float)field.GetRawConstantValue();
    }

    [Test]
    public void LighthouseFbxHasSlots()
    {
        GameObject model = Model();
        string[] names =
        {
            "LighthouseStone", "LighthouseIron", "BeaconGlass", "LighthouseStone_L1", "LighthouseIron_L1", "FlameRoot"
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
            if (filter.name.EndsWith("_L1") || filter.name == "BeaconGlass")
            {
                lod1 += filter.sharedMesh.triangles.Length / 3;
            }
        }

        Assert.LessOrEqual(lod1, 1500, "LOD1 triangles");
        Assert.AreEqual(11.9f, Find(model.transform, "FlameRoot").localPosition.y, 0.05f, "FlameRoot height");
    }

    [Test]
    public void LampPromptReachableFromPlateau()
    {
        float approach = Const("ApproachDistance");
        float range = Const("InteractRange");
        Assert.AreEqual(4.0f, approach, 1e-6f);
        Assert.AreEqual(4.5f, range, 1e-6f);
        Assert.LessOrEqual(approach, range, "the 4 m approach point is inside the prompt range");

        // The stone collider is the tower mesh: nothing of it reaches the approach point (the keeper's capsule radius is below 0.5 m).
        GameObject model = Model();
        Mesh stone = Find(model.transform, "LighthouseStone").GetComponent<MeshFilter>().sharedMesh;
        float reach = 0f;
        foreach (Vector3 v in stone.vertices)
        {
            reach = Mathf.Max(reach, new Vector2(v.x, v.z).magnitude);
        }

        Assert.Less(reach, approach - 0.5f, "tower radius at its widest");
        // The walkable ring on the 6 m plateau is at least 3.5 m wide.
        Assert.Greater(SpireLayout.SummitPlateauRadius - reach, 3.5f);
    }

    // The installer turns the lighthouse so the model +Z (the door) looks at the end of the summit span.
    [Test]
    public void DoorRecessFacesModelPlusZ()
    {
        Mesh iron = Find(Model().transform, "LighthouseIron").GetComponent<MeshFilter>().sharedMesh;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Vector3 v in iron.vertices)
        {
            if (v.y < 2.5f)
            {
                sum += v;
                count++;
            }
        }

        Assert.Greater(count, 0, "door vertices");
        Vector3 centre = sum / count;
        Assert.Greater(centre.z, 1.5f, "the door slab sits on the +Z side of the tower");
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
