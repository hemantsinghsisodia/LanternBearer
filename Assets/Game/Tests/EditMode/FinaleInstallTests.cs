using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.Tests
{
// The Island 5 finale installer: the baked silhouette meshes, and the installed Finale objects. Assembly-CSharp is not referenced
// from this assembly, so the runtime and editor types are reached through reflection.
public class FinaleInstallTests
{
    const string ScenePath = "Assets/Game/Scenes/Island5.unity";

    static Type Installer()
    {
        Type type = Type.GetType("LanternKeeper.FinaleInstaller, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "FinaleInstaller type");
        return type;
    }

    static string SilhouettePath(int island)
    {
        return (string)Installer().GetMethod("SilhouettePath").Invoke(null, new object[] { island });
    }

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

    static string FileHash(string assetPath)
    {
        using (MD5 md5 = MD5.Create())
        {
            return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(assetPath)));
        }
    }

    static GameObject FinaleRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Finale")
            {
                return root;
            }
        }

        return null;
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

                MeshFilter f = c as MeshFilter;
                if (f != null && f.sharedMesh != null)
                {
                    sb.Append('#').Append(f.sharedMesh.name).Append(f.sharedMesh.vertexCount);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    [Test]
    public void SilhouetteMeshesAreBakedFromTheTerrain()
    {
        Vector2[] expectedSize = { new Vector2(56f, 9f), new Vector2(80f, 12f), new Vector2(92f, 6f), new Vector2(84f, 14f) };
        for (int island = 1; island <= 4; island++)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SilhouettePath(island));
            Assert.IsNotNull(mesh, "Silhouette_Island" + island);
            Assert.LessOrEqual(mesh.vertexCount, 49 * 49, "a 48x48 grid at most");
            Assert.Greater(mesh.vertexCount, 200, "island " + island + " has land");
            Bounds b = mesh.bounds;
            Assert.LessOrEqual(b.size.x, expectedSize[island - 1].x + 0.01f, "island " + island + " width is the terrain width at most");
            Assert.Greater(b.size.x, expectedSize[island - 1].x * 0.3f, "island " + island + " is not a speck");
            Assert.LessOrEqual(b.max.y, expectedSize[island - 1].y + 0.01f, "island " + island + " is not taller than its terrain");
            Assert.Greater(b.max.y, 1f, "island " + island + " rises above the sea");
            // The sea skirt is cut: every triangle has a corner at or above the sea.
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                float top = Mathf.Max(v[t[i]].y, Mathf.Max(v[t[i + 1]].y, v[t[i + 2]].y));
                Assert.GreaterOrEqual(top, 0f, "island " + island + " triangle " + (i / 3) + " is wholly below the sea");
            }
        }
    }

    [Test]
    public void FinaleIsWiredIntoIsland5()
    {
        Scene scene = Open(out bool opened);
        try
        {
            GameObject root = FinaleRoot(scene);
            Assert.IsNotNull(root, "Finale root in Island5");
            Assert.IsNotNull(root.GetComponent(Type.GetType("LanternKeeper.FinaleSequence, Assembly-CSharp")), "FinaleSequence");
            Assert.IsNotNull(root.GetComponent(Type.GetType("LanternKeeper.FinaleCamera, Assembly-CSharp")), "FinaleCamera");
            Assert.IsNotNull(root.GetComponentInChildren(Type.GetType("LanternKeeper.StormRing, Assembly-CSharp"), true), "StormRing");
            Assert.IsNotNull(root.GetComponentInChildren(Type.GetType("LanternKeeper.FinaleIslands, Assembly-CSharp"), true), "FinaleIslands");
            Component manager = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Systems")
                {
                    manager = go.GetComponent(Type.GetType("LanternKeeper.GameManager, Assembly-CSharp"));
                }
            }

            Assert.IsNotNull(manager, "GameManager");
            Assert.IsNotNull(new SerializedObject(manager).FindProperty("finale").objectReferenceValue, "GameManager.finale is wired");
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [Test]
    public void FinaleInstallIsIdempotent()
    {
        Scene scene = Open(out bool opened);
        try
        {
            if (!opened && scene.isDirty)
            {
                Assert.Ignore("Island5 has unsaved changes in the editor; not rebuilding it in a test.");
            }

            MethodInfo rebuild = Installer().GetMethod("Rebuild");
            Assert.IsTrue((bool)rebuild.Invoke(null, new object[] { scene }));
            AssetDatabase.SaveAssets();
            string first = Snapshot(FinaleRoot(scene));
            int firstCount = FinaleRoot(scene).GetComponentsInChildren<Transform>(true).Length;
            string[] firstHash = new string[4];
            for (int i = 0; i < 4; i++)
            {
                firstHash[i] = FileHash(SilhouettePath(i + 1));
            }

            Assert.IsTrue((bool)rebuild.Invoke(null, new object[] { scene }));
            AssetDatabase.SaveAssets();
            string second = Snapshot(FinaleRoot(scene));
            Assert.AreEqual(first, second, "same hierarchy");
            Assert.AreEqual(firstCount, FinaleRoot(scene).GetComponentsInChildren<Transform>(true).Length, "same object count");
            int roots = 0;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Finale")
                {
                    roots++;
                }
            }

            Assert.AreEqual(1, roots, "one Finale root");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(firstHash[i], FileHash(SilhouettePath(i + 1)), "silhouette " + (i + 1) + " asset is byte-identical");
            }
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
}
}
