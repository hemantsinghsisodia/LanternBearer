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

    static string MeshPath(int island, string part)
    {
        return (string)Installer().GetMethod("IslandMeshPath").Invoke(null, new object[] { island, part });
    }

    static readonly string[] Parts = { "Terrain", "TerrainLow", "Props", "Base", "Glass", "Beams", "Flame" };

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
    public void IslandMeshesAreBakedFromTheTerrain()
    {
        Vector2[] expectedSize = { new Vector2(56f, 9f), new Vector2(80f, 12f), new Vector2(92f, 6f), new Vector2(84f, 14f) };
        for (int island = 1; island <= 4; island++)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Art/Finale/Silhouette_Island" + island + ".asset"), "the old 48x48 silhouette is gone");
            int totalTriangles = 0;
            foreach (string part in Parts)
            {
                Mesh part1 = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, part));
                Assert.IsNotNull(part1, part + " of island " + island);
                Assert.Greater(part1.vertexCount, 0, part + " of island " + island);
                if (part != "TerrainLow")
                {
                    totalTriangles += part1.triangles.Length / 3;
                }
            }

            Assert.LessOrEqual(totalTriangles, 25000, "island " + island + " stays within its triangle budget");

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "Terrain"));
            Mesh low = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "TerrainLow"));
            Assert.LessOrEqual(mesh.vertexCount, 129 * 129, "a 128x128 grid at most");
            Assert.Greater(mesh.vertexCount, 4000, "island " + island + " is a 128x128 terrain");
            Assert.Greater(mesh.vertexCount, low.vertexCount * 2, "the Low terrain is the coarser 64x64 one");
            Assert.AreEqual(mesh.vertexCount, mesh.colors32.Length, "terrain vertex colours");
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
    public void IslandsKeepTheirOwnColoursAndProps()
    {
        Color[] average = new Color[5];
        for (int island = 1; island <= 4; island++)
        {
            Mesh terrain = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "Terrain"));
            Color32[] colors = terrain.colors32;
            double r = 0, g = 0, bl = 0;
            for (int i = 0; i < colors.Length; i++) { r += colors[i].r; g += colors[i].g; bl += colors[i].b; }
            average[island] = new Color((float)(r / colors.Length / 255), (float)(g / colors.Length / 255), (float)(bl / colors.Length / 255));
            Assert.Greater(average[island].maxColorComponent, 0.05f, "island " + island + " has colour");

            Mesh props = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "Props"));
            Assert.Greater(props.triangles.Length / 3, MinPropTriangles, "island " + island + " has trees and rocks");
        }

        float spread = 0f;
        for (int a = 1; a <= 4; a++)
        {
            for (int c = a + 1; c <= 4; c++)
            {
                spread = Mathf.Max(spread, Mathf.Abs(average[a].r - average[c].r) + Mathf.Abs(average[a].g - average[c].g) + Mathf.Abs(average[a].b - average[c].b));
            }
        }

        Assert.Greater(spread, 0.04f, "the islands differ in palette");
    }

    // At least 40 props of about 24 triangles each.
    const int MinPropTriangles = 40 * 20;

    [Test]
    public void BeaconPartsMatchTheBeaconCount()
    {
        for (int island = 1; island <= 4; island++)
        {
            Mesh glass = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "Glass"));
            Mesh beams = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "Beams"));
            Mesh flame = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(island, "Flame"));
            int beacons = flame.triangles.Length / 12;
            Assert.GreaterOrEqual(beacons, 3, "island " + island + " beacons");
            Assert.AreEqual(beacons * 8, glass.triangles.Length / 3, "one lantern glass per beacon");
            Assert.AreEqual(beacons * 8, beams.triangles.Length / 3, "one set of light beams per beacon");
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
            Component clouds = root.GetComponentInChildren(Type.GetType("LanternKeeper.FinaleClouds, Assembly-CSharp"), true);
            Assert.IsNotNull(clouds, "FinaleClouds");
            SerializedObject cloudData = new SerializedObject(clouds);
            SerializedProperty layers = cloudData.FindProperty("layers");
            Assert.AreEqual(3, layers.arraySize, "near, mid and far banks");
            System.Collections.Generic.HashSet<UnityEngine.Object> materials = new System.Collections.Generic.HashSet<UnityEngine.Object>();
            for (int i = 0; i < 3; i++)
            {
                MeshRenderer layer = layers.GetArrayElementAtIndex(i).objectReferenceValue as MeshRenderer;
                Assert.IsNotNull(layer, "cloud layer " + i);
                Assert.IsNotNull(layer.sharedMaterial, "cloud layer " + i + " material");
                materials.Add(layer.sharedMaterial);
            }

            MeshRenderer rays = cloudData.FindProperty("rays").objectReferenceValue as MeshRenderer;
            Assert.IsNotNull(rays, "god rays");
            materials.Add(rays.sharedMaterial);
            Assert.AreEqual(4, materials.Count, "one material per layer plus one for the rays");
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
            string[] firstHash = new string[28];
            for (int i = 0; i < 4; i++)
            {
                for (int p = 0; p < Parts.Length; p++)
                {
                    firstHash[i * 7 + p] = FileHash(MeshPath(i + 1, Parts[p]));
                }
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
                for (int p = 0; p < Parts.Length; p++)
                {
                    Assert.AreEqual(firstHash[i * 7 + p], FileHash(MeshPath(i + 1, Parts[p])), Parts[p] + " of island " + (i + 1) + " is byte-identical");
                }
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
