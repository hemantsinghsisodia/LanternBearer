using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.Tests
{
// The installed Island 5 lighthouse (the saved scene) and GreatLampBeam. Assembly-CSharp is not referenced from this assembly, so the
// runtime types are used through reflection.
public class LighthouseInstallTests
{
    const string ScenePath = "Assets/Game/Scenes/Island5.unity";
    const string GraphicsKey = "LanternKeeperGraphics";

    static Type Installer()
    {
        Type type = Type.GetType("LanternKeeper.LighthouseInstaller, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "LighthouseInstaller type");
        return type;
    }

    static Type Runtime(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type");
        return type;
    }

    // The Island 5 scene: the loaded one, or opened additively for the test (opened tells which).
    static Scene Open(out bool opened)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == ScenePath)
            {
                opened = false;
                return loaded;
            }
        }

        opened = true;
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    static void Close(Scene scene, bool opened)
    {
        if (opened)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static Component Lamp(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "GreatLamp")
            {
                Component lamp = root.GetComponent(Runtime("GreatLamp"));
                Assert.IsNotNull(lamp, "GreatLamp component");
                return lamp;
            }
        }

        Assert.Fail("no GreatLamp in Island5");
        return null;
    }

    static Vector3 SpanDirection()
    {
        Vector2 d = SpireLayout.PathPoint(1f) - SpireLayout.LampXZ();
        return new Vector3(d.x, 0f, d.y).normalized;
    }

    [Test]
    public void LampPromptReachableFromPlateau()
    {
        Scene scene = Open(out bool opened);
        try
        {
            Component lamp = Lamp(scene);
            float range = new SerializedObject(lamp).FindProperty("interactRange").floatValue;
            float approach = (float)Installer().GetField("ApproachDistance").GetRawConstantValue();
            Assert.AreEqual(4.0f, approach, 1e-6f);

            Vector3 axis = lamp.transform.position;
            Vector3 point = axis + SpanDirection() * approach;
            Assert.LessOrEqual(Vector3.Distance(axis, point), range, "the 4 m approach point is inside the real interactRange");

            MeshCollider collider = lamp.GetComponentInChildren<MeshCollider>(true);
            Assert.IsNotNull(collider, "the stone mesh collider");
            Assert.AreEqual("LighthouseStone", collider.name);
            Bounds bounds = collider.sharedMesh.bounds;
            Assert.Less(Mathf.Max(bounds.extents.x, bounds.extents.z), approach - 0.5f, "the tower stays 0.5 m clear of the approach point");
            Assert.IsFalse(collider.bounds.Contains(new Vector3(point.x, collider.bounds.center.y, point.z)), "the approach point is outside the stone collider");
        }
        finally
        {
            Close(scene, opened);
        }
    }

    [Test]
    public void InstalledDoorFacesTheSpanEnd()
    {
        Scene scene = Open(out bool opened);
        try
        {
            Component lamp = Lamp(scene);
            Vector3 door = lamp.transform.Find("LighthouseDoor").GetComponent<MeshFilter>().sharedMesh.bounds.center;
            Vector3 doorWorld = lamp.transform.TransformDirection(new Vector3(door.x, 0f, door.z)).normalized;
            Assert.Greater(Vector3.Dot(doorWorld, SpanDirection()), 0.99f, "the door planks sit on the span-end side of the tower");
            Quaternion rotation = (Quaternion)Installer().GetMethod("LampRotation").Invoke(null, null);
            Assert.Greater(Vector3.Dot(rotation * Vector3.forward, SpanDirection()), 0.999f, "installer rotation");
            Assert.Greater(Mathf.Abs(Quaternion.Dot(rotation, lamp.transform.rotation)), 0.999f, "the installed lamp uses it");
        }
        finally
        {
            Close(scene, opened);
        }
    }

    [Test]
    public void StoneUsesItsOwnIsland5Material()
    {
        Material stone = AssetDatabase.LoadAssetAtPath<Material>((string)Installer().GetField("StoneMaterialPath").GetRawConstantValue());
        Material cliff = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Environment/Cliffs/island5/CliffSkin_island5.mat");
        Material cairn = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Beacons/BeaconCairn.mat");
        Assert.IsNotNull(stone);
        Assert.AreNotSame(cairn, stone);
        Assert.AreEqual(cliff.GetColor("_BaseColor"), stone.GetColor("_BaseColor"));
        Assert.AreEqual(cliff.shader, stone.shader);
    }

    // Two more runs of the installer give the same hierarchy (names, transforms, components, materials). Nothing is saved; the scene is reloaded after.
    [Test]
    public void InstallerIsIdempotent()
    {
        Scene scene = Open(out bool opened);
        try
        {
            if (!opened && scene.isDirty)
            {
                Assert.Ignore("Island5 has unsaved changes in the editor; not rebuilding it in a test.");
            }

            MethodInfo rebuild = Installer().GetMethod("Rebuild");
            string first = Snapshot(Lamp(scene).gameObject);
            Assert.IsTrue((bool)rebuild.Invoke(null, new object[] { scene }));
            string second = Snapshot(Lamp(scene).gameObject);
            Assert.IsTrue((bool)rebuild.Invoke(null, new object[] { scene }));
            string third = Snapshot(Lamp(scene).gameObject);
            Assert.AreEqual(first, second);
            Assert.AreEqual(second, third);
        }
        finally
        {
            if (!opened)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static string Snapshot(GameObject root)
    {
        StringBuilder sb = new StringBuilder();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            sb.Append(t.name).Append(t.localPosition.ToString("F3")).Append(t.localScale.ToString("F3")).Append(t.localRotation.ToString("F3"));
            foreach (Component c in t.GetComponents<Component>())
            {
                sb.Append(',').Append(c.GetType().Name);
                Renderer r = c as Renderer;
                if (r != null && r.sharedMaterial != null)
                {
                    sb.Append('=').Append(r.sharedMaterial.name);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ---- GreatLampBeam ----

    static Component NewBeam(out GameObject go)
    {
        go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        return go.AddComponent(Runtime("GreatLampBeam"));
    }

    static void Call(Component beam, string method, params object[] args)
    {
        beam.GetType().GetMethod(method).Invoke(beam, args);
    }

    static object Get(Component beam, string property)
    {
        return beam.GetType().GetProperty(property).GetValue(beam);
    }

    [Test]
    public void BeamRampReachesOneAfterRampSeconds()
    {
        Component beam = NewBeam(out GameObject go);
        try
        {
            Call(beam, "Begin", 2f);
            Assert.AreEqual(0f, (float)Get(beam, "Intensity01"), 1e-6f);
            for (int i = 0; i < 10; i++)
            {
                Call(beam, "Step", 0.1f);
            }

            Assert.AreEqual(0.5f, (float)Get(beam, "Intensity01"), 1e-4f, "half way after 1 s of 2");
            for (int i = 0; i < 10; i++)
            {
                Call(beam, "Step", 0.1f);
            }

            Assert.AreEqual(1f, (float)Get(beam, "Intensity01"), 1e-4f);
            Call(beam, "Step", 0.1f);
            Assert.AreEqual(1f, (float)Get(beam, "Intensity01"), 1e-4f, "stays at 1");
            Assert.AreEqual(24f * 2.1f, (float)Get(beam, "Angle"), 1e-2f, "24 degrees per second");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BeamStepIsClampedOnAHitch()
    {
        Component beam = NewBeam(out GameObject go);
        try
        {
            Call(beam, "Begin", 2f);
            Call(beam, "Step", 5f);
            Assert.AreEqual(0.05f, (float)Get(beam, "Intensity01"), 1e-4f, "a 5 s hitch counts as 0.1 s");
            Assert.AreEqual(2.4f, (float)Get(beam, "Angle"), 1e-3f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BeamAppliesTheLowScalesOnLow()
    {
        int original = PlayerPrefs.GetInt(GraphicsKey, 0);
        Component beam = NewBeam(out GameObject go);
        try
        {
            Call(beam, "Begin", 0f);
            PlayerPrefs.SetInt(GraphicsKey, 0);
            Call(beam, "Step", 0.05f);
            Assert.AreEqual(0.5f, (float)Get(beam, "AppliedLengthScale"), 1e-6f, "Low length scale");
            float low = (float)Get(beam, "AppliedIntensity");
            PlayerPrefs.SetInt(GraphicsKey, 3);
            Call(beam, "Step", 0.05f);
            Assert.AreEqual(1f, (float)Get(beam, "AppliedLengthScale"), 1e-6f, "Ultra length scale");
            float high = (float)Get(beam, "AppliedIntensity");
            Assert.Less(low, 2.2f * 0.6f + 1e-4f, "Low intensity is scaled by 0.6");
            Assert.Greater(high, low, "full intensity is brighter than Low");
        }
        finally
        {
            PlayerPrefs.SetInt(GraphicsKey, original);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
}
